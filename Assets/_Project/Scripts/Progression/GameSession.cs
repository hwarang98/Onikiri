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
        [SerializeField] private CharacterLevel character;
        [SerializeField] private StageProgress stage;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Onikiri.UI.OfflineRewardPopup offlinePopup;

        [Tooltip("자동 저장 간격 (초). 프로세스가 예고 없이 사라져도 잃는 양을 " +
                 "이 정도로 묶어둔다")]
        [SerializeField] private float autoSaveInterval = 30f;

        private float autoSaveTimer;
        private bool loaded;

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
            var data = SaveSystem.Load();

            var wallet = PlayerWallet.Instance;
            if (wallet != null) wallet.SetBalance(data.gold, data.lifetimeGold);

            if (stage != null) stage.SetProgress(data.stage, data.killsThisStage, data.bossKillCount);

            // 레벨은 강화보다 **먼저** 복원한다. 스탯 포인트 증폭이 강화 값에
            // 곱해지므로(UpgradeSystem.Apply), 순서가 뒤바뀌면 강화가 증폭 없는
            // 값으로 한 번 적용되고 그 상태가 다음 구매까지 남는다
            if (character != null)
                character.Restore(data.characterLevel, data.exp, data.attackPoints, data.healthPoints);

            // 강화는 스테이지 다음에 적용한다. 스탯이 곧바로 전투에 반영되므로
            // 순서가 뒤바뀌면 한 프레임 동안 어긋난 값으로 싸운다
            if (upgrades != null) upgrades.RestoreLevels(data.upgradeIds, data.upgradeLevels);

            loaded = true;
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
            }

            if (character != null)
            {
                data.characterLevel = character.Level;
                data.exp = character.Exp;
                data.attackPoints = character.AttackPoints;
                data.healthPoints = character.HealthPoints;
            }

            data.lastQuitUtcTicks = DateTime.UtcNow.Ticks;
            data.goldPerSecond = EstimateGoldPerSecond();
            data.expPerSecond = EstimateExpPerSecond(data.goldPerSecond);

            SaveSystem.Save(data);
        }

        /** 지금 스탯과 스테이지에서 기대되는 초당 골드 */
        public double EstimateGoldPerSecond()
        {
            if (combat == null || spawner == null) return 0d;

            var multiplierHealth = stage != null ? stage.HealthMultiplier : BigDouble.One;
            var multiplierGold = stage != null ? stage.GoldMultiplier : BigDouble.One;

            // 획득 축(20단계)이 방치 보상에도 들어온다. 방치는 파밍의 축소판이라
            // (IdleIncome 주석) 파밍에 곱해지는 것이 여기에만 안 곱해지면 두 벌이
            // 갈린다 - 그 순간부터 "켜두면 손해"가 성립한다.
            //
            // **곱하는 곳은 여기 하나뿐이다.** 이 값이 세이브에 적히고 복귀 시
            // 그대로 지급되므로(GrantOfflineReward), 지급 쪽에서 또 곱하면 배수가
            // 제곱된다
            var goldGain = BigDouble.FromDouble(UpgradeSystem.CurrentGoldGain);

            return IdleIncome.GoldPerSecond(
                combat.Damage,
                combat.AttacksPerSecond,
                spawner.AverageBaseHealth * multiplierHealth,
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
            return killsPerSecond * ExpCurve.MobExp(stageNumber).ToDouble();
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
