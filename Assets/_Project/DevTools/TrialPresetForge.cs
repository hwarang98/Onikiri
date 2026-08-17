#if UNITY_EDITOR
using System.Collections.Generic;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.DevTools
{
    /**
     * @brief `StageSimulation`의 밴드 빌드 한 줄을 **실제 세이브 한 벌**로 찍어낸다.
     *
     * ## 왜 이것이 있어야 하는가 - 3단계 실기가 k를 고정하지 못한 이유가 이것이다
     *
     * 3단계 Android 실측은 공격력 강화 레벨만 손으로 바꾼 **합성 빌드**로 쟀다.
     * 그 빌드는 `StageSimulation`이 정의한 하한·곡선추종이 아니므로, 그 숫자로 k를
     * 고정하면 근거가 바뀐 것을 고정값으로 승격하는 셈이다(Step3 §9.6이 스스로
     * 적어 둔 결론). 실기가 k를 고정할 자격을 갖는 유일한 길은 **밴드 빌드를
     * 그대로 세이브로 찍어내는 것**이고, 그것이 이 파일이다.
     *
     * ## 왜 프로덕션 어셈블리가 아니라 여기인가 - 어셈블리 경계 때문이다
     *
     *   `Assets/_Project/Editor`   asmdef이 없어 `Assembly-CSharp-Editor`다.
     *                              **asmdef 어셈블리가 이것을 참조할 수 없다** -
     *                              즉 EditMode 테스트가 못 본다
     *   `Onikiri.Runtime`          프로덕션이다. 이 스텝은 프로덕션에 한 줄도
     *                              넣지 않는다
     *
     * 그래서 어셈블리를 하나 새로 만들고 파일 전체를 `#if UNITY_EDITOR`로 감쌌다.
     * 플레이어 빌드에서는 **빈 어셈블리**가 되므로 APK에 코드가 한 줄도 안 들어가고
     * (§2의 "Release 빌드에 프리셋 메뉴 미포함"), 에디터에서는 테스트·PlayMode·
     * Editor 메뉴 셋이 **같은 변환 한 벌**을 본다. 변환을 두 벌 두면 그 둘이
     * 갈리는 날 "도구가 만든 세이브"와 "테스트가 검사한 세이브"가 다른 물건이 된다.
     *
     * ## 무엇을 만들지 않는가
     *
     * 골드·경험치·퀘스트 진행은 0이다. 셋 다 `TrialPowerScore`의 입력이 아니고,
     * 프리셋의 용도가 "앱을 켜서 곧바로 그 귀문에 들어간다"이기 때문이다. 대신
     * `Audit`이 그 사실을 표로 남긴다 - 모델링하지 않은 것을 조용히 두면 다음에
     * 읽는 사람이 그것을 "0인 빌드"로 읽는다.
     */
    public static class TrialPresetForge
    {
        /** 프리셋 파일이 놓이는 곳. 프로젝트 안이다 - **사용자 세이브 폴더가 아니다** */
        public const string OutputFolder = "Builds/Step5/presets";

        /**
         * @brief 이 프리셋이 노리는 것. 이름이 곧 파일 이름이 된다.
         *
         * `Middle`이 없다. 중간 프로필은 `PromotionTrialTests.MidAt`이 만드는
         * **기하평균 계산 대리값**이고 그 DPS를 내는 강화 레벨 조합은 어디에도
         * 정의돼 있지 않다 - 실재하지 않는 빌드를 세이브로 찍으면 그것이 바로
         * 3단계가 폐기한 합성 빌드다.
         */
        public enum Profile
        {
            /** `Policy { GemsFromQuestsOnly = true }` - 일일 보석을 한 번도 안 받은 플레이어 */
            Floor,

            /** `Policy.Default` - 곡선을 따라가는 플레이어. 밸런스의 기준선 */
            CurveFollower
        }

        public static string NameOf(Profile profile, int gate)
        {
            return (profile == Profile.Floor ? "floor" : "curve") + "-gate" + gate;
        }

        // ---------------------------------------------------------------- 변환

        /**
         * @brief 시뮬레이션 한 줄 -> 세이브 한 벌.
         *
         * `gate`를 따로 받는 이유는 **문 대기 상태**를 함께 써야 하기 때문이다.
         * 스테이지만 맞춰 두면 앱이 켜졌을 때 문이 열려 있지 않고, 그러면 실기
         * 측정이 잡몹 열 마리와 일반 보스를 다시 잡는 것부터 시작한다.
         *
         * 대기 조건은 `PromotionTrialCatalog.PendingGate`의 다섯 가지다.
         * 여기서 그 판정을 복제하지 않고 **그 함수가 참을 내는 값을 쓴다** -
         * `Audit`이 실제로 그 함수를 불러 확인한다.
         */
        public static SaveData Build(StageSimulation.StageResult row, int gate)
        {
            var data = SaveData.NewGame();

            // ---- 진행. bossKillCount == stage가 "보스는 벴고 문은 아직"이다 (D-4)
            data.version = SaveData.CurrentVersion;
            data.stage = row.Stage;
            data.maxStageReached = row.Stage;
            data.killsThisStage = StageCurve.KillsPerStage;
            data.bossKillCount = row.Stage;

            // ---- 경지. 문 N에 서는 플레이어의 경지는 N-1이다
            data.evolutionTier = row.EvolutionTier;

            // ---- 캐릭터. 남은 포인트는 저장하지 않는다(SaveData.attackPoints 주석)
            data.characterLevel = row.CharacterLevel;
            data.attackPoints = row.AttackPoints;
            data.healthPoints = row.HealthPoints;
            data.exp = BigDouble.Zero;

            // ---- 강화 아홉 축. 시뮬레이션이 `Levels`에서 쓰는 것과 같은 순서다
            data.upgradeIds = new[]
            {
                UpgradeSystem.AttackPowerId, UpgradeSystem.AttackSpeedId,
                UpgradeSystem.CritRateId, UpgradeSystem.CritDamageId,
                UpgradeSystem.HealthId, UpgradeSystem.HealthRegenId,
                UpgradeSystem.GoldGainId,
                UpgradeSystem.TranscendId, UpgradeSystem.ComboId
            };
            data.upgradeLevels = new[]
            {
                row.AttackPowerLevel, row.AttackSpeedLevel,
                row.CritRateLevel, row.CritDamageLevel,
                row.HealthLevel, row.RegenLevel,
                row.GoldGainLevel,
                row.TranscendLevel, row.ComboLevel
            };

            // ---- 장비 두 슬롯
            data.equipmentIds = new[] { EquipmentCatalog.WeaponId, EquipmentCatalog.ArmorId };
            data.equipmentGrades = new[] { row.WeaponGrade, row.ArmorGrade };
            data.equipmentLevels = new[] { row.WeaponLevel, row.ArmorLevel };

            // ---- 오의. 보유(레벨)와 장착을 따로 담는다(SaveData.skillEquipped 주석)
            var skills = SkillCatalog.Skills;
            data.skillIds = new string[skills.Length];
            data.skillLevels = new int[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                data.skillIds[i] = skills[i].Id;
                data.skillLevels[i] = i < row.SkillLevels.Length ? row.SkillLevels[i] : 1;
            }

            data.skillEquipped = new string[SkillCurve.MaxSlots];
            for (int slot = 0; slot < data.skillEquipped.Length; slot++)
            {
                bool filled = row.SkillEquipped != null && slot < row.SkillEquipped.Length
                              && row.SkillEquipped[slot] >= 0 && row.SkillEquipped[slot] < skills.Length;
                data.skillEquipped[slot] = filled ? skills[row.SkillEquipped[slot]].Id : string.Empty;
            }
            data.skillAutoCast = true;

            // 뽑기로 열린 오의. 비트마스크를 id 목록으로 푼다 - 세이브는 **보유한
            // 것만** 적는다(SaveData.gachaSkillIds 주석)
            var owned = new List<string>();
            for (int i = 0; i < skills.Length; i++)
                if ((row.SkillGachaOwned & (1 << i)) != 0) owned.Add(skills[i].Id);
            data.gachaSkillIds = owned.ToArray();
            data.skillXp = (long)row.SkillXp;
            data.skillGachaTotalPulls = (int)row.SkillGachaPulls;

            // 온보딩 10연을 **받은 것으로** 둔다. 안 그러면 앱을 켠 직후 무료
            // 10연이 터져 프리셋의 화력이 측정 전에 움직인다 - 시뮬레이션에는
            // 없는 항이므로 이쪽이 "그 항이 없는 세계"에 더 가깝다
            data.skillGachaIntroClaimed = true;
            data.skillGachaIntroEquipDone = true;

            // ---- 동료. 레벨 0이 미보유다(StageResult.PetLevels 주석)
            var pets = PetCatalog.Pets;
            data.petIds = new string[pets.Length];
            data.petUnlocked = new int[pets.Length];
            data.petLevels = new int[pets.Length];
            for (int i = 0; i < pets.Length; i++)
            {
                int level = row.PetLevels != null && i < row.PetLevels.Length ? row.PetLevels[i] : 0;
                data.petIds[i] = pets[i].Id;
                data.petUnlocked[i] = level > 0 ? 1 : 0;
                data.petLevels[i] = level > 0 ? level : 1;
            }
            data.activePetId = string.Empty;

            // ---- 요도. 혼 잔량은 첫 자루에 얹는다 - 종류별 잔량이 StageResult에
            // 없고(합계 하나다), 혼은 티어를 올리기 전까지 화력에 기여하지 않는다
            var blades = YodoCatalog.Blades;
            data.yodoIds = new string[blades.Length];
            data.yodoTiers = new int[blades.Length];
            data.yodoRarities = new int[blades.Length];
            data.yodoDiscovered = new int[blades.Length];
            data.yodoSouls = new long[blades.Length];
            for (int i = 0; i < blades.Length; i++)
            {
                int tier = row.YodoTiers != null && i < row.YodoTiers.Length ? row.YodoTiers[i] : 0;
                data.yodoIds[i] = blades[i].Id;
                data.yodoTiers[i] = tier;
                data.yodoRarities[i] = row.YodoRarities != null && i < row.YodoRarities.Length
                    ? row.YodoRarities[i] : 0;
                data.yodoDiscovered[i] = tier > 0 ? 1 : 0;
                data.yodoSouls[i] = i == 0 ? row.SoulsHeld : 0L;
            }
            data.yodoShards = row.Shards;
            data.gachaTotalPulls = (int)row.GachaPulls;

            var legend = LegendaryYodoCatalog.Blades;
            data.legendaryYodoIds = new string[legend.Length];
            data.legendaryYodoCopies = new int[legend.Length];
            for (int i = 0; i < legend.Length; i++)
            {
                data.legendaryYodoIds[i] = legend[i].Id;
                data.legendaryYodoCopies[i] = row.LegendaryCopies != null && i < row.LegendaryCopies.Length
                    ? row.LegendaryCopies[i] : 0;
            }

            // ---- 재화. 골드는 0이다 - 프리셋은 "다 쓴 뒤"의 상태이고, 남겨 두면
            // 측정 직전에 강화를 더 살 수 있어 빌드가 밴드 빌드가 아니게 된다
            data.gems = row.GemsEarned - row.GemsSpent;
            data.gold = BigDouble.Zero;
            data.lifetimeGold = BigDouble.Zero;
            data.goldPerSecond = 0d;
            data.expPerSecond = 0d;

            return data;
        }

        // ---------------------------------------------------------------- 직렬화

        /** 게임과 **같은 직렬화기**를 쓴다. 다른 것으로 쓰면 읽는 쪽이 다른 파일을 본다 */
        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static SaveData FromJson(string json)
        {
            return JsonUtility.FromJson<SaveData>(json);
        }

        // ---------------------------------------------------------------- 감사

        public struct Mismatch
        {
            public string Field;
            public string Expected;
            public string Actual;

            public override string ToString()
            {
                return Field + ": 기대 " + Expected + " / 실제 " + Actual;
            }
        }

        /**
         * @brief 세이브가 그 시뮬레이션 줄과 **같은 빌드인가.** 어긋난 축만 낸다.
         *
         * ## 두 종류를 검사한다
         *
         *   정수     세이브가 그 축을 **담고 있는가**. 안 담으면 여기서 갈린다
         *   배수     세이브의 값에서 곡선을 지나 나온 배수가 그 줄의 배수와 같은가
         *
         * 앞의 것만으로는 부족하다 - 담기는 했는데 다른 슬롯에 담으면(무기 등급을
         * 방어구 자리에) 정수는 다 맞고 배수만 갈린다. 뒤의 것만으로도 부족하다 -
         * 요도처럼 배수가 여러 입력의 합성이면 한 입력이 빠져도 다른 입력이
         * 우연히 같은 배수를 낼 수 있다.
         *
         * ## 여기서 재지 않는 것 - **런타임 DPS 그 자체다**
         *
         * 실제 게임의 DPS는 `PlayerCombat`이 MonoBehaviour 다섯을 지나 만든다.
         * 그 경로를 EditMode에서 재현하면 그것 자체가 세 번째 조립 규칙이 되고,
         * 그러면 검사가 게임이 아니라 자기 자신을 잰다. 그 등식은 이미 두 곳이
         * 지킨다 - `PromotionTrialDamageTests.TrialPowerMatchesExpectedDps`(런타임
         * `ReadTrialPower` == `ExpectedDps`)와 §5의 실기 로그다.
         */
        public static List<Mismatch> Audit(SaveData save, StageSimulation.StageResult row, int gate)
        {
            var bad = new List<Mismatch>();

            // ---- 진행과 문 대기
            Int(bad, "stage", row.Stage, save.stage);
            Int(bad, "maxStageReached", row.Stage, save.maxStageReached);
            Int(bad, "killsThisStage", StageCurve.KillsPerStage, save.killsThisStage);
            Int(bad, "bossKillCount", row.Stage, save.bossKillCount);
            Int(bad, "evolutionTier", gate - 1, save.evolutionTier);
            Int(bad, "version", SaveData.CurrentVersion, save.version);

            int pending = PromotionTrialCatalog.PendingGate(
                save.stage, save.maxStageReached, save.killsThisStage, save.bossKillCount, save.evolutionTier);
            Int(bad, "PendingGate(세이브에서 유도)", gate, pending);

            // ---- 캐릭터
            Int(bad, "characterLevel", row.CharacterLevel, save.characterLevel);
            Int(bad, "attackPoints", row.AttackPoints, save.attackPoints);
            Int(bad, "healthPoints", row.HealthPoints, save.healthPoints);

            // ---- 강화 아홉
            Int(bad, "강화:" + UpgradeSystem.AttackPowerId, row.AttackPowerLevel, Track(save, UpgradeSystem.AttackPowerId));
            Int(bad, "강화:" + UpgradeSystem.AttackSpeedId, row.AttackSpeedLevel, Track(save, UpgradeSystem.AttackSpeedId));
            Int(bad, "강화:" + UpgradeSystem.CritRateId, row.CritRateLevel, Track(save, UpgradeSystem.CritRateId));
            Int(bad, "강화:" + UpgradeSystem.CritDamageId, row.CritDamageLevel, Track(save, UpgradeSystem.CritDamageId));
            Int(bad, "강화:" + UpgradeSystem.HealthId, row.HealthLevel, Track(save, UpgradeSystem.HealthId));
            Int(bad, "강화:" + UpgradeSystem.HealthRegenId, row.RegenLevel, Track(save, UpgradeSystem.HealthRegenId));
            Int(bad, "강화:" + UpgradeSystem.GoldGainId, row.GoldGainLevel, Track(save, UpgradeSystem.GoldGainId));
            Int(bad, "강화:" + UpgradeSystem.TranscendId, row.TranscendLevel, Track(save, UpgradeSystem.TranscendId));
            Int(bad, "강화:" + UpgradeSystem.ComboId, row.ComboLevel, Track(save, UpgradeSystem.ComboId));

            // ---- 장비
            Int(bad, "무기 등급", row.WeaponGrade, Grade(save, EquipmentCatalog.WeaponId));
            Int(bad, "무기 단련", row.WeaponLevel, Level(save, EquipmentCatalog.WeaponId));
            Int(bad, "방어구 등급", row.ArmorGrade, Grade(save, EquipmentCatalog.ArmorId));
            Int(bad, "방어구 단련", row.ArmorLevel, Level(save, EquipmentCatalog.ArmorId));

            // ---- 오의
            var skills = SkillCatalog.Skills;
            for (int i = 0; i < skills.Length; i++)
                Int(bad, "오의 레벨:" + skills[i].Id,
                    i < row.SkillLevels.Length ? row.SkillLevels[i] : 1, SkillLevel(save, skills[i].Id));

            int slots = row.SkillEquipped != null ? row.SkillEquipped.Length : 0;
            for (int slot = 0; slot < slots; slot++)
            {
                string want = skills[row.SkillEquipped[slot]].Id;
                string got = save.skillEquipped != null && slot < save.skillEquipped.Length
                    ? save.skillEquipped[slot] : "(없음)";
                if (want != got) bad.Add(new Mismatch { Field = "장착 슬롯 " + slot, Expected = want, Actual = got });
            }

            // ---- 동료
            var pets = PetCatalog.Pets;
            for (int i = 0; i < pets.Length; i++)
            {
                int want = row.PetLevels != null && i < row.PetLevels.Length ? row.PetLevels[i] : 0;
                int got = PetLevel(save, pets[i].Id);
                Int(bad, "동료 레벨:" + pets[i].Id, want, got);
            }

            // ---- 요도
            var blades = YodoCatalog.Blades;
            for (int i = 0; i < blades.Length; i++)
            {
                Int(bad, "요도 티어:" + blades[i].Id,
                    row.YodoTiers != null && i < row.YodoTiers.Length ? row.YodoTiers[i] : 0,
                    save.yodoTiers != null && i < save.yodoTiers.Length ? save.yodoTiers[i] : 0);
                Int(bad, "요도 혼격:" + blades[i].Id,
                    row.YodoRarities != null && i < row.YodoRarities.Length ? row.YodoRarities[i] : 0,
                    save.yodoRarities != null && i < save.yodoRarities.Length ? save.yodoRarities[i] : 0);
            }
            var legend = LegendaryYodoCatalog.Blades;
            for (int i = 0; i < legend.Length; i++)
                Int(bad, "전설 사본:" + legend[i].Id,
                    row.LegendaryCopies != null && i < row.LegendaryCopies.Length ? row.LegendaryCopies[i] : 0,
                    save.legendaryYodoCopies != null && i < save.legendaryYodoCopies.Length
                        ? save.legendaryYodoCopies[i] : 0);

            Int(bad, "파편", row.Shards, (int)save.yodoShards);
            Int(bad, "보석 잔액", row.GemsEarned - row.GemsSpent, (int)save.gems);

            /**
             * @brief 배수 비교. **`StageResult`가 두 시점을 섞어 담고 있다.**
             *
             * 5.0단계에 실측으로 드러난 사실이고, 이 감사 함수의 모양이 그 사실을
             * 그대로 담는다. `StageSimulation.Run`의 순서가 이렇다:
             *
             * ```
             *   잡몹 10마리 + 구매          <- 이 상태로 보스와 싸운다
             *   var stats = levels.Stats;   <- ExpectedDps / Damage / CritRate ...
             *   보스 골드 · 클리어 보너스 · 업적 · 요도 · 오의 뽑기
             *   Buy(stage + 1, ...)         <- **여기서 또 산다**
             *   results.Add(... levels.*)   <- 강화 레벨은 이 시점의 값
             * ```
             *
             * 그래서 `levels`에서 나온 항(무기·방어구·전직·초월·연격·최대 체력·
             * 재생)은 세이브와 **비트 단위로** 같아야 하고, `stats`에서 나온 항
             * (공격력·치명타·공격속도·ExpectedDps)은 **같을 수 없다** - 세이브는
             * 구매 뒤의 레벨을 담기 때문이다.
             *
             * 그 차이를 여기서 등호로 강제하면 검사가 거짓이 된다. 대신 `Measure`가
             * 크기를 재고, 그 크기가 5.0단계의 가장 중요한 발견이다(문별 x1.70~1.88).
             */
            Num(bad, "무기 배수", row.WeaponMultiplier, WeaponMultiplierOf(save));
            Num(bad, "방어구 배수", row.ArmorMultiplier, ArmorMultiplierOf(save));
            Num(bad, "전직 공격 배수", row.EvolutionAttack, EvolutionCurve.AttackMultiplierAt(save.evolutionTier));
            Num(bad, "전직 체력 배수", row.EvolutionHealth, EvolutionCurve.HealthMultiplierAt(save.evolutionTier));
            Num(bad, "초월 배수", row.TranscendMultiplier,
                TranscendCurve.MultiplierAtLevel(Track(save, UpgradeSystem.TranscendId)));
            Num(bad, "연격 확률", row.ComboChance,
                ComboCurve.ChanceAtLevel(Track(save, UpgradeSystem.ComboId)));
            Num(bad, "최대 체력(합성)", row.MaxHealth, MaxHealthOf(save));
            Num(bad, "초당 재생(합성)", row.RegenPerSecond, RegenOf(save));

            return bad;
        }

        // ---------------------------------------------------------------- 두 시점

        /**
         * @brief 밴드가 쓴 화력과 **프리셋이 실제로 갖는 화력.** 5.0단계의 핵심 지표다.
         *
         * ## 왜 두 값이 다른가 - 게이트 보스의 보상이 문 앞에서 이미 들어와 있다
         *
         * `BossFight.OnBossKilled`은 게이트 스테이지에서도 **보상을 평소대로 정확히
         * 한 번 준다**(그 함수 주석: "멈추는 것은 진행뿐이다"). 그러니 「귀문 도전」
         * 화면에 선 플레이어는 이미 그 보스의 골드·클리어 보너스·요도를 갖고 있고,
         * 강화 화면이 열려 있으므로 **문에 들어가기 전에 그것을 쓸 수 있다**
         * (입장 후에는 잠긴다 - `TrialPowerScore` 머리 주석).
         *
         * 밴드는 그 보상을 쓰기 **전**의 화력(`stats`)에 앵커돼 있다. 따라서
         * 실제 플레이어는 두 지점 사이 어디든 설 수 있다:
         *
         *   즉시 도전    P = BandP     밴드가 잰 그 플레이어
         *   쇼핑 후 도전  P = PresetP   같은 플레이어가 보상을 쓴 뒤
         *
         * **계약은 그 구간 전체에서 성립해야 한다.** 한쪽 끝만 재면 다른 쪽 끝의
         * 플레이어의 게임이 검사되지 않는다 - `Policy.GemsFromQuestsOnly`가 밴드의
         * 두 끝을 다 재는 것과 같은 규칙이다.
         *
         * ## PresetDps는 **하한이다**
         *
         * `SkillRate`·`SpiritRate`는 `stats`에서 온 값이라 구매 뒤의 값이 아니다
         * (그 둘만 구매 뒤 스냅샷이 `StageResult`에 없다). 그것을 그대로 쓰므로
         * 여기 나오는 화력은 실제보다 **작다** - 즉 아래 시간은 실제보다 **길다**.
         */
        public struct Power
        {
            /** 밴드가 앵커로 쓴 화력 (`row.ExpectedDps`) */
            public double BandDps;

            /** 프리셋 세이브가 실제로 갖는 화력 (구매 뒤 레벨에서 유도) */
            public double PresetDps;

            public double Ratio;
            public double BandP;
            public double PresetP;
        }

        // ---------------------------------------------------------------- 기대값 표

        /**
         * @brief PlayMode 검사가 읽는 **기대값 한 줄.** `JsonUtility`가 담는다.
         *
         * ## 왜 파일로 넘기는가
         *
         * 이 어셈블리는 `includePlatforms: ["Editor"]`라 플레이어 빌드에 안 들어간다.
         * 그 대가로 **PlayMode 검사가 이 타입을 참조할 수 없다**(그쪽 asmdef는 전
         * 플랫폼이다). 그래서 값을 JSON으로 내보내고, 검사는 같은 필드 이름을 가진
         * 자기 타입으로 읽는다 - 이름이 갈리면 검사가 0을 읽고 즉시 실패하므로
         * 조용히 어긋날 수 없다.
         *
         * ## 무엇이 정확하고 무엇이 하한인가
         *
         *   `autoAttack`         **정확하다.** 전부 세이브의 레벨에서 유도된다
         *   `damage` … `petBonus` **정확하다**
         *   `skillRateLowerBound` / `spiritRateLowerBound`  **하한이다** -
         *       그 둘만 `StageResult`에 구매 뒤 값이 없다(§2.5)
         *   `presetDps`          그래서 **하한이다**
         */
        [System.Serializable]
        public struct Expected
        {
            public string name;
            public int gate;
            public int stage;

            public double bandDps;
            public double presetDps;

            public double damage;
            public double attacksPerSecond;
            public double critRate;
            public double critMultiplier;
            public double transcendMultiplier;
            public double comboChance;
            public double petBonus;

            public double skillRateLowerBound;

            /**
             * @brief 영체 시전율. **정확하다** - 프리셋의 요도 상태에서 바로 나온다.
             *
             * `row.SpiritRate`를 쓰지 않는다. 그쪽은 구매 전 스냅샷이라 프리셋이
             * 담은 티어와 다른 세계의 값이고, 5.0단계의 PlayMode 동등성 검사가
             * 실제로 그것에 물렸다 - `curve-gate4`에서 구매 전 1.5352 / 프리셋
             * 상태 1.3234로 **구매 뒤가 더 작았다**(요도를 벼리면 소환 순번이
             * 바뀌어 초당 환산이 내려갈 수 있다).
             *
             * 대신 런타임이 부르는 것과 **같은 함수**를 부른다
             * (`YodoSystem.SpiritRate` -> `YodoSpiritCurve.RateFor`). 두 번째
             * 계산기를 만드는 것이 아니라 같은 계산기를 같은 입력으로 부르는 것이다.
             */
            public double spiritRate;

            /** `damage x 치명타 기대배수 x 초월 x (1+연격) x 공격속도` */
            public double autoAttack;

            /**
             * @brief PlayMode 검사가 총 화력을 재는 기준.
             *
             * `unit x (공격속도 + 시전율 하한 + 영체 시전율) x (1 + 동료 보너스)`.
             * 오의 시전율만 하한이므로 **런타임은 이 값 이상**이어야 하고, 실측
             * 초과폭은 1~4%다(오의 레벨이 구매 뒤에 조금 더 올라 있다).
             */
            public double expectedTotal;
        }

        [System.Serializable]
        public class ExpectedTable
        {
            public Expected[] presets = new Expected[0];
        }

        /** 이 표가 놓이는 파일. PlayMode 검사가 이 경로를 읽는다 */
        public const string ExpectedFileName = "expected.json";

        public static Expected ExpectedOf(StageSimulation.StageResult row, int gate,
                                          string name, double referencePower)
        {
            var power = Measure(row, referencePower);

            double damage = AttackPowerCurve.ValueAtLevel(row.AttackPowerLevel)
                          * StatPointCurve.Multiplier(row.AttackPoints)
                          * row.WeaponMultiplier * row.EvolutionAttack * row.YodoMultiplier;
            double aps = AttackSpeedCurve.CappedValueAtLevel(row.AttackSpeedLevel);
            double critRate = CritRateCurve.CappedValueAtLevel(row.CritRateLevel);
            double critMultiplier = CritDamageCurve.ValueAtLevel(row.CritDamageLevel);

            double factor = 1d + critRate * (critMultiplier - 1d);
            double unit = damage * factor * row.TranscendMultiplier * (1d + row.ComboChance);

            // 런타임과 같은 함수·같은 입력. 프리셋이 담는 배열이 그대로 들어간다
            double spiritRate = YodoSpiritCurve.RateFor(
                row.YodoTiers, row.YodoRarities, row.LegendaryCopies);

            return new Expected
            {
                name = name,
                gate = gate,
                stage = row.Stage,

                bandDps = power.BandDps,
                presetDps = power.PresetDps,

                damage = damage,
                attacksPerSecond = aps,
                critRate = critRate,
                critMultiplier = critMultiplier,
                transcendMultiplier = row.TranscendMultiplier,
                comboChance = row.ComboChance,
                petBonus = row.PetBonus,

                skillRateLowerBound = row.SkillRate,
                spiritRate = spiritRate,

                autoAttack = unit * aps,
                expectedTotal = unit * (aps + row.SkillRate + spiritRate) * (1d + row.PetBonus)
            };
        }

        public static Power Measure(StageSimulation.StageResult row, double referencePower)
        {
            var post = new CombatStats
            {
                Damage = AttackPowerCurve.ValueAtLevel(row.AttackPowerLevel)
                       * StatPointCurve.Multiplier(row.AttackPoints)
                       * row.WeaponMultiplier * row.EvolutionAttack * row.YodoMultiplier,
                AttacksPerSecond = AttackSpeedCurve.CappedValueAtLevel(row.AttackSpeedLevel),
                CritRate = CritRateCurve.CappedValueAtLevel(row.CritRateLevel),
                CritMultiplier = CritDamageCurve.ValueAtLevel(row.CritDamageLevel),
                SkillRate = row.SkillRate,
                SpiritRate = row.SpiritRate,
                PetBonus = row.PetBonus,
                TranscendMultiplier = row.TranscendMultiplier,
                ComboChance = row.ComboChance
            };

            double presetDps = post.ExpectedDps;

            return new Power
            {
                BandDps = row.ExpectedDps,
                PresetDps = presetDps,
                Ratio = row.ExpectedDps > 0d ? presetDps / row.ExpectedDps : 0d,
                BandP = referencePower > 0d ? row.ExpectedDps / referencePower : 0d,
                PresetP = referencePower > 0d ? presetDps / referencePower : 0d
            };
        }

        /**
         * @brief 모델링하지 않은 것의 목록. **보고서가 이 표를 그대로 싣는다.**
         *
         * 빈 값을 조용히 두면 다음에 읽는 사람이 그것을 "0인 빌드"로 읽는다.
         */
        public static string[] NotModelled()
        {
            return new[]
            {
                "gold / lifetimeGold - 0. 프리셋은 다 쓴 뒤의 상태다 (남기면 측정 직전에 강화를 더 산다)",
                "exp - 0. 그 레벨의 시작점",
                "퀘스트 진행·수령(questIds/questClaims/카운터) - 빈 상태. 화력의 입력이 아니다",
                "요도 혼 잔량 - 종류별 값이 StageResult에 없어 첫 자루에 합계를 얹었다",
                "뽑기 천장(gachaPity/skillGachaPity/AwakenPity) - 0. 재고 상태이지 화력이 아니다",
                "일일 무료 뽑기 쿨(…FreePullDayTicks) - 0 = 미사용",
                "온보딩 10연 - 받은 것으로 뒀다. 안 그러면 앱 실행 직후 화력이 움직인다",
                "playerName - 빈 문자열. 리더보드 이름 입력이 뜨는 상태 그대로",
                "lastQuitUtcTicks / lastDailyResetUtcTicks - 0. 방치 보상이 0이 되는 값"
            };
        }

        public static string Describe(SaveData save)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("stage=" + save.stage + " max=" + save.maxStageReached
                          + " kills=" + save.killsThisStage + " bossKills=" + save.bossKillCount
                          + " tier=" + save.evolutionTier + " v=" + save.version);
            sb.AppendLine("charLv=" + save.characterLevel + " ap=" + save.attackPoints
                          + " hp=" + save.healthPoints + " gems=" + save.gems);
            for (int i = 0; i < save.upgradeIds.Length; i++)
                sb.AppendLine("강화 " + save.upgradeIds[i] + " = " + save.upgradeLevels[i]);
            for (int i = 0; i < save.equipmentIds.Length; i++)
                sb.AppendLine("장비 " + save.equipmentIds[i] + " = " + save.equipmentGrades[i]
                              + "등급 Lv." + save.equipmentLevels[i]);
            for (int i = 0; i < save.skillIds.Length; i++)
                sb.AppendLine("오의 " + save.skillIds[i] + " = Lv." + save.skillLevels[i]);
            sb.AppendLine("장착 = " + string.Join(" | ", save.skillEquipped));
            for (int i = 0; i < save.petIds.Length; i++)
                sb.AppendLine("동료 " + save.petIds[i] + " = " + (save.petUnlocked[i] == 1 ? "보유" : "미보유")
                              + " Lv." + save.petLevels[i]);
            for (int i = 0; i < save.yodoIds.Length; i++)
                sb.AppendLine("요도 " + save.yodoIds[i] + " = 티어 " + save.yodoTiers[i]
                              + " 혼격 " + save.yodoRarities[i] + " 혼 " + save.yodoSouls[i]);
            for (int i = 0; i < save.legendaryYodoIds.Length; i++)
                sb.AppendLine("전설 " + save.legendaryYodoIds[i] + " = 사본 " + save.legendaryYodoCopies[i]);
            sb.AppendLine("파편=" + save.yodoShards + " skillXp=" + save.skillXp
                          + " 가챠오의=" + save.gachaSkillIds.Length + "종");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 조립

        static double WeaponMultiplierOf(SaveData save)
        {
            var spec = EquipmentCatalog.Slots[EquipmentCatalog.IndexOf(EquipmentCatalog.WeaponId)];
            return EquipmentCurve.ValueAt(spec.GradeStep, spec.TemperStep,
                Grade(save, EquipmentCatalog.WeaponId), Level(save, EquipmentCatalog.WeaponId));
        }

        static double ArmorMultiplierOf(SaveData save)
        {
            var spec = EquipmentCatalog.Slots[EquipmentCatalog.IndexOf(EquipmentCatalog.ArmorId)];
            return EquipmentCurve.ValueAt(spec.GradeStep, spec.TemperStep,
                Grade(save, EquipmentCatalog.ArmorId), Level(save, EquipmentCatalog.ArmorId));
        }

        static double MaxHealthOf(SaveData save)
        {
            return HealthCurve.ValueAtLevel(Track(save, UpgradeSystem.HealthId))
                 * StatPointCurve.Multiplier(save.healthPoints)
                 * ArmorMultiplierOf(save)
                 * EvolutionCurve.HealthMultiplierAt(save.evolutionTier);
        }

        static double RegenOf(SaveData save)
        {
            return MaxHealthOf(save)
                 * HealthRegenCurve.CappedValueAtLevel(Track(save, UpgradeSystem.HealthRegenId));
        }

        // ---------------------------------------------------------------- 읽기

        static int Track(SaveData save, string id)
        {
            if (save.upgradeIds == null) return 0;
            for (int i = 0; i < save.upgradeIds.Length; i++)
                if (save.upgradeIds[i] == id) return save.upgradeLevels[i];
            return 0;
        }

        static int Grade(SaveData save, string id)
        {
            if (save.equipmentIds == null) return 0;
            for (int i = 0; i < save.equipmentIds.Length; i++)
                if (save.equipmentIds[i] == id) return save.equipmentGrades[i];
            return 0;
        }

        static int Level(SaveData save, string id)
        {
            if (save.equipmentIds == null) return 0;
            for (int i = 0; i < save.equipmentIds.Length; i++)
                if (save.equipmentIds[i] == id) return save.equipmentLevels[i];
            return 0;
        }

        static int SkillLevel(SaveData save, string id)
        {
            if (save.skillIds == null) return 0;
            for (int i = 0; i < save.skillIds.Length; i++)
                if (save.skillIds[i] == id) return save.skillLevels[i];
            return 0;
        }

        static int PetLevel(SaveData save, string id)
        {
            if (save.petIds == null) return 0;
            for (int i = 0; i < save.petIds.Length; i++)
                if (save.petIds[i] == id) return save.petUnlocked[i] == 1 ? save.petLevels[i] : 0;
            return 0;
        }

        static void Int(List<Mismatch> bad, string field, int expected, int actual)
        {
            if (expected != actual)
                bad.Add(new Mismatch
                {
                    Field = field,
                    Expected = expected.ToString(),
                    Actual = actual.ToString()
                });
        }

        /** 배수 비교. 상대 오차 1e-9 - 같은 곡선을 지나므로 비트 단위로 같아야 한다 */
        static void Num(List<Mismatch> bad, string field, double expected, double actual)
        {
            double scale = System.Math.Max(1e-300, System.Math.Abs(expected));
            if (System.Math.Abs(expected - actual) / scale > 1e-9d)
                bad.Add(new Mismatch
                {
                    Field = field,
                    Expected = expected.ToString("E10"),
                    Actual = actual.ToString("E10")
                });
        }
    }
}
#endif
