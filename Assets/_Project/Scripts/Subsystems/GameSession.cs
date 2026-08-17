using System;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 세이브 적용과 저장 시점을 관장한다.
     *
     * 저장을 여러 곳에 흩어두면 어떤 경로로 나갔을 때 무엇이 저장되는지 아무도 모르게
     * 된다. 진입점을 하나로 모아둔다.
     *
     * 모바일에서 중요한 것은 **OnApplicationQuit이 오지 않는다는 사실**이다. 안드로이드와
     * iOS 모두 홈으로 나가면 OnApplicationPause(true)까지만 오고, 그 뒤 프로세스는
     * 예고 없이 회수된다. Quit만 믿으면 대부분의 실제 종료에서 진행이 날아간다.
     */
    [DefaultExecutionOrder(-50)]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private SkillSystem skills;
        [SerializeField] private CharacterLevel character;
        [SerializeField] private StageProgress stage;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private QuestSystem quests;
        [SerializeField] private EquipmentSystem equipment;
        [SerializeField] private EvolutionSystem evolution;
        [SerializeField] private PetSystem petSystem;
        [SerializeField] private YodoSystem yodo;
        [SerializeField] private GachaSystem gacha;

        /** 오의 뽑기 (50단계). 요도 뽑기와 갈라 둔 이유는 SkillGachaSystem 머리 주석 */
        [SerializeField] private SkillGachaSystem skillGacha;
        [SerializeField] private Onikiri.UI.OfflineRewardPopup offlinePopup;

        [Tooltip("자동 저장 간격 (초). 프로세스가 예고 없이 사라져도 잃는 양을 " +
                 "이 정도로 묶어둔다")]
        [SerializeField] private float autoSaveInterval = 30f;

        private float autoSaveTimer;
        private bool loaded;

        /**
         * @brief 세이브 적용이 끝났는가. 인트로의 로딩 게이트가 읽는다.
         *
         * 쓰기는 이 클래스만 한다 - 게이트는 관측이지 조종이 아니다.
         */
        public bool IsLoaded { get { return loaded; } }

        private void Start()
        {
            Load();
        }

        private void Update()
        {
            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer < autoSaveInterval) return;

            autoSaveTimer = 0f;
            Save();
        }

        /**
         * @brief 모바일의 실제 종료는 여기로 온다.
         *
         * 앱이 다시 앞으로 나올 때(paused=false)도 저장한다. 복귀 시각을 기록해두면
         * 짧게 다른 앱을 다녀온 것이 방치 시간으로 계산되지 않는다.
         */
        private void OnApplicationPause(bool paused)
        {
            Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        // ---------------------------------------------------------------- 불러오기

        private void Load()
        {
            Apply(SaveSystem.Load());
        }

        /**
         * @brief 세이브 한 벌을 **시스템에 얹는다.** 파일을 읽지 않는다.
         *
         * ## 왜 갈라 뒀는가 - 검사가 실사용 세이브를 건드릴 수 없기 때문이다
         *
         * `Load()`는 `SaveSystem.Load()`를 지나고, 에디터에서 그 경로는 **실사용
         * 세이브**를 가리킨다. 승급 5.0단계의 프리셋 동등성 검사는 "밴드 빌드를
         * 담은 세이브를 게임이 읽으면 시뮬레이션과 같은 화력이 나오는가"를 재야
         * 하는데, 그것을 위해 실사용 파일을 덮어쓰는 것은 허용되지 않는다.
         *
         * 그래서 **읽기와 적용을 나눈다.** 검사는 프리셋 JSON을 스스로 파싱해
         * 이 함수에 넘기고, 그러면 복원 순서·`ApplyAll`·퀘스트 판정까지 실제
         * 경로를 그대로 지난다 - 유일하게 빠지는 것이 디스크 읽기 한 줄이고,
         * 그 한 줄이 정확히 위험한 부분이다.
         *
         * 순서에는 손대지 않았다. 아래 주석들이 그 순서의 이유를 하나하나 적고
         * 있고, 이 갈라내기는 **한 줄을 앞으로 뺀 것 말고는 아무것도 바꾸지 않는다.**
         */
        public void Apply(SaveData data)
        {
            if (data == null) return;

            var wallet = PlayerWallet.Instance;
            if (wallet != null) wallet.SetBalance(data.gold, data.lifetimeGold);

            if (stage != null)
                stage.SetProgress(data.stage, data.killsThisStage, data.bossKillCount,
                    data.maxStageReached);

            // 레벨은 강화보다 **먼저** 복원한다. 스탯 포인트 증폭이 강화 값에
            // 곱해지므로(UpgradeSystem.Apply), 순서가 뒤바뀌면 강화가 증폭 없는
            // 값으로 한 번 적용되고 그 상태가 다음 구매까지 남는다
            if (character != null)
                character.Restore(data.characterLevel, data.exp, data.attackPoints, data.healthPoints);

            // 강화는 스테이지 다음에 적용한다. 스탯이 곧바로 전투에 반영되므로
            // 순서가 뒤바뀌면 한 프레임 동안 어긋난 값으로 싸운다
            if (upgrades != null) upgrades.RestoreLevels(data.upgradeIds, data.upgradeLevels);

            // 오의는 강화 다음이다. 오의 배율이 공격력에 곱해지므로(PlayerCombat.
            // CastSkill), 강화가 먼저 적용돼 있어야 한 프레임이라도 어긋난 값으로
            // 시전하지 않는다
            if (skills != null)
            {
                skills.RestoreLevels(data.skillIds, data.skillLevels, data.skillAutoCast);

                // **보유는 레벨과 장착 사이다**(50단계). 장착 복원이 빈 자리를
                // 기준 구성으로 메우는데 그 기준이 해금 상태를 읽으므로, 뽑기로
                // 얻은 오의가 그 전에 열려 있어야 한다 - 순서가 뒤바뀌면 가챠
                // 몫을 끼워 둔 플레이어의 자리가 한 프레임 비었다가 다른 오의로
                // 메워진다
                skills.RestoreGacha(data.gachaSkillIds, data.skillXp);

                // **레벨 다음에 장착이다.** 구성 복원이 빈 자리를 기준 구성으로
                // 메우는데(FillEmptySlots), 그 기준이 해금 상태를 읽으므로 레벨과
                // 최전선이 먼저 제자리에 있어야 한다
                skills.RestoreEquipped(data.skillEquipped);
            }

            // 장비는 강화 **다음**이다. 장비 배수가 강화 값에 곱해지므로
            // (UpgradeSystem.Apply), 순서가 뒤바뀌면 장비가 배수 없는 값에 한 번
            // 적용되고 그 상태가 다음 구매까지 남는다 - 레벨을 강화보다 먼저
            // 복원하는 것과 정확히 같은 이유이고, 복원 끝에 ApplyAll을 다시
            // 부르는 것도 같다
            if (equipment != null)
                equipment.Restore(data.equipmentIds, data.equipmentGrades, data.equipmentLevels);

            // 전직도 장비와 같은 자리, 같은 이유다 - 배수가 강화 값에 곱해진다
            if (evolution != null) evolution.Restore(data.evolutionTier);

            // 요도도 같은 자리, 같은 이유다(44단계). 배수가 강화 값에
            // 곱해지므로 강화 뒤여야 하고, 복원 끝에 ApplyAll을 다시 부른다
            // 47단계에 인자 셋이 붙었다(혼격·전설 id·전설 사본). 전부
            // 기본값이 있어 v15 세이브는 예전 그대로 복원된다
            if (yodo != null)
                yodo.Restore(data.yodoIds, data.yodoSouls, data.yodoTiers,
                             data.yodoDiscovered, data.yodoShards,
                             data.yodoRarities, data.legendaryYodoIds, data.legendaryYodoCopies);

            // 뽑기는 요도 **다음**이다(46단계). 순서가 있는 이유는 복원
            // 자체가 아니라 계약이다 - 뽑기가 요도의 상태를 읽어 상한을
            // 판정하므로(YodoSystem.TryTakeEssence), 요도가 아직 0인
            // 프레임에 뽑기가 도는 경로를 만들지 않는다
            if (gacha != null)
                gacha.Restore(data.gachaPity, data.gachaTotalPulls, data.gachaFreePullDayTicks);

            // 오의 뽑기는 오의 **다음**이다(50단계). 같은 계약이고 같은 이유다 -
            // 이쪽은 오의의 상태를 읽어 재고를 판정하므로(SkillSystem.HasStock),
            // 오의가 아직 비어 있는 프레임에 배너가 "재고 없음"으로 서지 않게 한다
            if (skillGacha != null)
                skillGacha.Restore(data.skillGachaPity, data.skillGachaTotalPulls,
                                   data.skillGachaFreePullDayTicks,
                                   data.skillGachaAwakenPity,
                                   data.skillGachaIntroClaimed,
                                   data.skillGachaIntroEquipDone);

            // 펫은 순서 제약이 느슨하다 - 스탯에 곱해지지 않고 PetCombat이
            // 매 타마다 현재 값을 읽는다. 그래도 퀘스트보다 앞에 두는 것은
            // 다른 축과 같은 결이다(상태 먼저, 그 상태를 읽는 것은 나중)
            if (petSystem != null)
                petSystem.Restore(data.petIds, data.petUnlocked, data.petLevels, data.activePetId);

            // 이름은 순서 제약이 없다 - 게임의 어떤 값도 이것을 읽지 않는다
            // (54단계). 리더보드 제출과 랭킹표만 본다
            PlayerProfile.Restore(data.playerName);

            // 퀘스트는 **맨 마지막**이다. 업적이 스테이지·레벨·강화 총합을 읽으므로
            // 그 셋이 이미 복원돼 있어야 한다 - 먼저 돌면 전부 초기값으로 읽혀서
            // 30스테이지 플레이어에게 "5스테이지 도달"이 미달성으로 뜬다
            if (quests != null) quests.Restore(data);

            loaded = true;

            // 방치 보상은 퀘스트 복원 뒤다. 지급이 wallet.Add를 지나 골드 카운터를
            // 올리는데, 복원이 나중이면 그 값을 세이브의 옛 값이 덮어쓴다
            GrantOfflineReward(data);
        }

        private void GrantOfflineReward(SaveData data)
        {
            var lastQuit = data.LastQuitUtc;
            if (lastQuit == null) return;

            var now = DateTime.UtcNow;
            var accrued = IdleIncome.AccruedTime(lastQuit.Value, now);
            if (accrued <= TimeSpan.Zero) return;

            // 나갈 때 적어둔 초당 수입을 쓴다. 지금 다시 계산하면 그동안 오른 것이
            // 아니라 그때 벌던 것을 줘야 한다는 원칙이 깨진다
            var reward = IdleIncome.Reward(data.goldPerSecond, accrued);
            var expReward = IdleIncome.Reward(data.expPerSecond, accrued);

            // 경험치만 있고 골드가 없는 경우는 실제로 생긴다 - 스폰이 병목인
            // 구간에서 반올림이 갈린다. 골드 기준으로 조기 반환하면 그 구간의
            // 방치 경험치가 통째로 사라진다
            if (reward <= BigDouble.Zero && expReward <= BigDouble.Zero) return;

            var wallet = PlayerWallet.Instance;
            if (wallet != null) wallet.Add(reward);

            if (character != null) character.AddExp(expReward);

            if (offlinePopup != null)
                offlinePopup.Show(reward, accrued, IdleIncome.IsCapped(lastQuit.Value, now));

            Debug.Log(string.Format("[Onikiri] Offline reward: {0} gold, {1} exp for {2} away "
                + "(rates {3:F2}/s, {4:F2}/s).",
                NumberFormatter.Format(reward), NumberFormatter.Format(expReward),
                NumberFormatter.FormatDuration(accrued), data.goldPerSecond, data.expPerSecond));
        }

        // ---------------------------------------------------------------- 저장

        /**
         * @brief 지금 상태를 디스크에 쓴다.
         *
         * 불러오기 전에는 저장하지 않는다. 씬이 뜨자마자 종료되면 아직 비어 있는
         * 기본값이 기존 세이브를 덮어써 진행이 통째로 사라진다.
         */
        public void Save()
        {
            if (!loaded) return;

            var data = new SaveData();

            var wallet = PlayerWallet.Instance;
            if (wallet != null)
            {
                data.gold = wallet.Gold;
                data.lifetimeGold = wallet.LifetimeGold;
            }

            if (upgrades != null)
            {
                data.upgradeIds = upgrades.CollectIds();
                data.upgradeLevels = upgrades.CollectLevels();
            }

            if (stage != null)
            {
                data.stage = stage.Stage;
                data.killsThisStage = stage.KillsThisStage;
                data.bossKillCount = stage.BossKillCount;
                data.maxStageReached = stage.MaxStageReached;
            }

            if (character != null)
            {
                data.characterLevel = character.Level;
                data.exp = character.Exp;
                data.attackPoints = character.AttackPoints;
                data.healthPoints = character.HealthPoints;
            }

            if (skills != null)
            {
                data.skillIds = skills.CollectIds();
                data.skillLevels = skills.CollectLevels();
                data.skillEquipped = skills.CollectEquipped();
                data.skillAutoCast = skills.AutoCast;

                data.gachaSkillIds = skills.CollectGachaSkillIds();
                data.skillXp = skills.CollectSkillXp();
            }

            if (equipment != null)
            {
                data.equipmentIds = equipment.CollectIds();
                data.equipmentGrades = equipment.CollectGrades();
                data.equipmentLevels = equipment.CollectLevels();
            }

            if (evolution != null) data.evolutionTier = evolution.CollectTier();

            if (yodo != null)
            {
                data.yodoIds = yodo.CollectIds();
                data.yodoSouls = yodo.CollectSouls();
                data.yodoTiers = yodo.CollectTiers();
                data.yodoDiscovered = yodo.CollectDiscovered();
                data.yodoShards = yodo.CollectShards();

                data.yodoRarities = yodo.CollectRarities();
                data.legendaryYodoIds = yodo.CollectLegendaryIds();
                data.legendaryYodoCopies = yodo.CollectLegendaryCopies();
            }

            if (gacha != null)
            {
                data.gachaPity = gacha.CollectPity();
                data.gachaTotalPulls = gacha.CollectTotalPulls();
                data.gachaFreePullDayTicks = gacha.CollectFreePullDay();
            }

            if (skillGacha != null)
            {
                data.skillGachaPity = skillGacha.CollectPity();
                data.skillGachaTotalPulls = skillGacha.CollectTotalPulls();
                data.skillGachaFreePullDayTicks = skillGacha.CollectFreePullDay();
                data.skillGachaAwakenPity = skillGacha.CollectAwakenPity();
                data.skillGachaIntroClaimed = skillGacha.CollectIntroClaimed();
                data.skillGachaIntroEquipDone = skillGacha.CollectIntroEquipDone();
            }

            if (petSystem != null)
            {
                data.petIds = petSystem.CollectIds();
                data.petUnlocked = petSystem.CollectUnlocked();
                data.petLevels = petSystem.CollectLevels();
                // activePetId는 레거시(단일 출전 시절)라 더 적지 않는다 -
                // 남아 있는 값은 그대로 실려 다니고 아무도 읽지 않는다
            }

            // 안 정했으면 빈 문자열이 그대로 적힌다(SaveData.playerName 주석)
            data.playerName = PlayerProfile.Collect();

            // 퀘스트가 보석 잔액까지 함께 적는다. 지갑을 따로 읽지 않는 이유는
            // 둘이 한 시스템이기 때문이다 - 보석은 퀘스트 말고 들어올 곳이 없다
            if (quests != null) quests.Write(data);

            data.lastQuitUtcTicks = DateTime.UtcNow.Ticks;
            data.goldPerSecond = EstimateGoldPerSecond();
            data.expPerSecond = EstimateExpPerSecond(data.goldPerSecond);

            SaveSystem.Save(data);
        }

        /** 지금 스탯과 스테이지에서 기대되는 초당 골드 */
        public double EstimateGoldPerSecond()
        {
            if (combat == null || spawner == null) return 0d;

            // 체력은 온보딩 완화(st1~5 잡몹 전용)까지 지난 실제 값이다. 방치는
            // 파밍의 축소판이라, 파밍만 빨라지고 방치 계산이 옛 체력을 쓰면
            // 온보딩 구간에서 두 벌이 갈린다
            var mobHealth = stage != null
                ? StageCurve.MobHealth(spawner.AverageBaseHealth, stage.Stage)
                : spawner.AverageBaseHealth;
            var multiplierGold = stage != null ? stage.GoldMultiplier : BigDouble.One;

            // 획득 축(20단계)이 방치 보상에도 들어온다. 방치는 파밍의 축소판이라
            // (IdleIncome 주석) 파밍에 곱해지는 것이 여기에만 안 곱해지면 두 벌이
            // 갈린다 - 그 순간부터 "켜두면 손해"가 성립한다.
            //
            // **곱하는 곳은 여기 하나뿐이다.** 이 값이 세이브에 적히고 복귀 시
            // 그대로 지급되므로(GrantOfflineReward), 지급 쪽에서 또 곱하면 배수가
            // 제곱된다
            var goldGain = BigDouble.FromDouble(UpgradeSystem.CurrentGoldGain);

            // 오의를 포함한 초당 환산 공격 횟수를 넘긴다. 방치는 파밍의 축소판인데
            // 오의는 자동 시전이라 자리를 비운 동안에도 나간다 - 여기서 빼면
            // 방치 수입이 실제 파밍보다 가난하게 계산되고, 그러면 "켜두는 것이
            // 이득"이라는 관계가 26단계에 조용히 강해진다
            return IdleIncome.GoldPerSecond(
                combat.Damage,
                combat.EffectiveAttacksPerSecond,
                mobHealth,
                spawner.AverageBaseGold * multiplierGold * goldGain,
                spawner.SpawnInterval);
        }

        /**
         * @brief 지금 스테이지에서 기대되는 초당 경험치.
         *
         * 처치 속도를 다시 구하지 않고 초당 골드에서 환산한다. 둘 다 "초당 처치 수 x
         * 처치당 보상"이고 처치 속도는 같은 값이므로, 골드를 골드/처치로 나누면
         * 초당 처치 수가 그대로 나온다.
         *
         * 이렇게 하는 이유는 처치 속도 계산이 IdleIncome 안에 두 상한(화력/공급)으로
         * 들어 있기 때문이다. 여기서 다시 세우면 그 두 상한을 복제하게 되고, 한쪽만
         * 고쳐지는 날 방치 골드와 방치 경험치가 서로 다른 처치 속도를 쓰게 된다.
         */
        public double EstimateExpPerSecond(double goldPerSecond)
        {
            if (goldPerSecond <= 0d || spawner == null) return 0d;

            int stageNumber = stage != null ? stage.Stage : 1;
            var goldMultiplier = stage != null ? stage.GoldMultiplier : BigDouble.One;

            // 처치당 골드에도 획득 배수를 곱한다. **초당 골드를 이것으로 나눠
            // 처치 속도를 되찾는 식이라 두 값의 배수가 같아야 한다.**
            //
            // 한쪽만 곱하면 나눗셈이 배수를 그대로 남기고, 그 결과 방치 경험치가
            // 골드 배수만큼 부풀려진다 - 화면에서는 "자리를 비웠더니 레벨만
            // 이상하게 올랐다"로 나타나서, 원인이 경험치 쪽이 아니라 골드 축에
            // 있다는 것을 알아채기 어렵다
            var goldGain = BigDouble.FromDouble(UpgradeSystem.CurrentGoldGain);

            double goldPerKill = (spawner.AverageBaseGold * goldMultiplier * goldGain).ToDouble();
            if (goldPerKill <= 0d) return 0d;

            double killsPerSecond = goldPerSecond / goldPerKill;

            // 최전선 아래에서 나가면 방치 경험치도 0이다. 파밍이 안 주는 것을
            // 방치가 주면 "낮은 데서 꺼두는 것"이 경험치 최적이 된다
            bool atFrontier = stage == null || stage.IsAtFrontier;
            return killsPerSecond * ExpCurve.MobExp(stageNumber, atFrontier).ToDouble();
        }

        /** 테스트 패널이 쓰는 초기화 */
        public void DeleteSaveAndReload()
        {
            SaveSystem.Delete();
            loaded = false;
            Load();
        }

        /**
         * @brief 디스크의 세이브를 다시 적용한다.
         *
         * 방치 보상을 확인하려면 실제로 몇 시간을 기다리는 수밖에 없다. 테스트 패널이
         * 저장된 시각을 과거로 돌린 뒤 이것을 불러서, 앱을 껐다 켠 것과 같은 경로를
         * 그대로 태운다.
         */
        public void ReloadFromDisk()
        {
            loaded = false;
            Load();
        }
    }
}
