using System;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 요괴 봉인 검(妖刀). 혼을 모으고, 칼에 봉인하고, 합성해 티어를 올린다.
     *
     * ## 왜 EquipmentSystem에 넣지 않았는가
     *
     * 화면은 같은 대장간이고 탭 하나 차이다. 그런데도 별개인 이유는
     * EquipmentSystem이 별개였던 이유와 같은 종류이면서 한 단계 더 나아간다 -
     * 저쪽은 **재화가 둘**이라 갈렸고(골드/보석), 이쪽은 **재화가 셋인데 그중
     * 둘이 이 스텝에서 새로 생긴 것**이다(혼/파편). 슬롯 배열에 "혼 개수"를
     * 끼워 넣는 순간 EquipmentSystem의 한 줄 설명("두 슬롯의 등급과 단련
     * 레벨을 들고 있다")이 깨진다.
     *
     * 더 실질적인 이유가 하나 더 있다. 장비는 **골드가 속도를 정하는** 축이라
     * 지갑 이벤트에 반응해야 하고, 요도는 **보스 처치가 속도를 정하는** 축이라
     * 전투 사건에 반응한다. 두 시스템이 듣는 것이 다르다.
     *
     * ## 상태 셋 - 혼, 파편, 티어
     *
     *   혼(종류별)  대요괴가 남긴다. 봉인·합성에 하나씩 쓰인다
     *   파편        정예가 남긴다 + 상한 요도의 남는 혼 + 보석 묶음. 합성의 재료
     *   티어(자루별) 0 = 미봉인. 1 = 봉인됨. 상한은 YodoCurve.MaxTier
     *
     * 셋 다 long이다. BigDouble이 아닌 이유는 GemWallet과 같다 - **파밍으로
     * 늘지 않는다.** 혼은 40스테이지에 넷, 파편은 10스테이지에 한 뭉치라
     * st10000에 가도 만 단위다. 그래도 손상된 세이브가 long 끝을 들고 오는
     * 경로는 막아 둔다(GemWallet.Add와 같은 규칙).
     */
    public sealed class YodoSystem : MonoBehaviour
    {
        /**
         * @brief 요도 한 자루. 값은 빌더가 카탈로그에서 옮겨 적는다.
         *
         * EquipmentSystem.Slot과 같은 규칙이다 - 컴포넌트가 이미 씬에 있으면
         * 스크립트 기본값을 고쳐도 반영되지 않으므로, 빌더가 단일 출처로서
         * 씬에 명시적으로 기록하고 테스트가 둘이 같은지 검사한다.
         */
        [Serializable]
        public sealed class Blade
        {
            public string id;

            [Tooltip("\"등롱의 혼\". 카운터와 도감이 쓴다")]
            public string soulName;

            [Tooltip("\"등롱도\". 봉인 뒤의 이름")]
            public string bladeName;

            [Tooltip("\"외눈 등롱\". 잠긴 도감이 \"○○ 처치 시 해금\"으로 쓴다")]
            public string bossName;

            [Tooltip("아직 봉인·합성에 쓰지 않은 혼")]
            public long souls;

            [Tooltip("0 = 미봉인, 1 = 봉인됨. 상한은 YodoCurve.MaxTier")]
            public int tier;

            /**
             * @brief 혼격 (47단계). 0 = 격이 없는 보통 혼.
             *
             * 티어와 나란히 서는 두 번째 숫자다. 상한이 티어에서 나오므로
             * (YodoRarityCurve.CapAt) 이 값만 따로 오를 수 없고,
             * **자연 출처가 없다** - 뽑기의 ★4에서만 들어온다.
             */
            public int rarity;

            /** 지금까지 이 혼을 한 번이라도 받았는가. 도감의 "본 적 있다" */
            public bool discovered;

            /**
             * @brief 이 자루가 공격력에 곱하는 값. **티어 x 혼격이다.**
             *
             * 세트 보너스는 여기 없다 - 그것은 자루의 값이 아니라 도감의
             * 값이라 YodoCurve.AttackFactorFor가 밖에서 곱한다.
             */
            public double Multiplier
            {
                get { return YodoCurve.TierValue(tier) * YodoRarityCurve.ValueAt(rarity); }
            }

            public bool Sealed { get { return tier >= 1; } }
        }

        /**
         * @brief 전설 妖刀 한 자루의 보유 상태. **사본 수 하나가 전부다.**
         *
         * Blade와 나눈 이유는 들고 있는 것이 다르기 때문이다 - 저쪽은 혼과
         * 파편으로 벼려지고 이쪽은 뽑기에서만 나온다. 한 배열에 섞으면
         * YodoCatalog.Count가 세계 순환의 길이(YodoCurve.CycleLength)를
         * 유도하는 값이라 **한 바퀴가 40에서 60스테이지가 된다.**
         */
        [Serializable]
        public sealed class LegendaryBlade
        {
            public string id;

            [Tooltip("\"백면도\". 도감이 쓴다")]
            public string bladeName;

            [Tooltip("0 = 미보유. 1 = 획득. 그 위는 돌파 (LegendaryYodoCurve.MaxCopies)")]
            public int copies;

            public bool Owned { get { return copies >= 1; } }
            public int Breakthrough { get { return LegendaryYodoCurve.BreakthroughOf(copies); } }
            public double Multiplier { get { return LegendaryYodoCurve.PowerAt(copies); } }
        }

        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private GemWallet gems;
        [SerializeField] private StageProgress stage;

        [SerializeField] private Blade[] blades;

        [Tooltip("가챠 전용 전설 妖刀 (47단계). 보스 혼 넷과 별개 풀이다")]
        [SerializeField] private LegendaryBlade[] legendaries;

        [Tooltip("합성의 재료. 종류가 없다 - 어느 요도에나 들어간다")]
        [SerializeField] private long shards;

        /**
         * @brief 혼·파편·티어 중 무엇이든 바뀌면 발생. 화면과 배지가 듣는다.
         *
         * 드랍도 여기로 온다. 전투 중에 도감이 열려 있는 일은 없지만, 열어
         * 두고 보스를 잡는 경로(테스트 패널)가 실제로 있다.
         */
        public event Action Changed;

        /**
         * @brief 방금 봉인·합성이 일어났다. 연출이 듣는다.
         *
         * Changed와 나누는 이유는 Changed가 **상태가 달라졌다**는 뜻이라
         * 파편 하나가 들어와도 발생하기 때문이다. 연출은 그때마다 터지면
         * 안 되고, 봉인/티어업이라는 사건에만 반응해야 한다.
         */
        public event Action<int, bool> Forged;

        public static YodoSystem Instance { get; private set; }

        public int BladeCount { get { return blades != null ? blades.Length : 0; } }

        public long Shards { get { return shards; } }

        public Blade GetBlade(int index)
        {
            if (blades == null || index < 0 || index >= blades.Length) return null;
            return blades[index];
        }

        public int LegendaryCount { get { return legendaries != null ? legendaries.Length : 0; } }

        public LegendaryBlade GetLegendary(int index)
        {
            if (legendaries == null || index < 0 || index >= legendaries.Length) return null;
            return legendaries[index];
        }

        /** 보유한 전설 자루 수 (0~2). 도감 머리글이 읽는다 */
        public int LegendaryOwnedCount
        {
            get
            {
                if (legendaries == null) return 0;

                int owned = 0;
                foreach (var blade in legendaries)
                    if (blade != null && blade.Owned) owned++;
                return owned;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second YodoSystem appeared; keeping the first.");
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // 미봉인의 배수가 1배라 여기서 아무것도 바뀌지 않는다. 그래도
            // 부르는 이유는 세이브가 먼저 복원된 경우(GameSession) 그 값이
            // 스탯에 도달해 있어야 하기 때문이다 - EquipmentSystem.Start와
            // 같은 처리다
            ApplyToStats();
            Raise();
        }

        // ---------------------------------------------------------------- 해금

        /**
         * @brief 지금 대장간의 요도 탭이 열려 있는가.
         *
         * **스테이지로 잠근다. 레벨이 아니다.** 장비(대장간)와 같은 규칙이고
         * 이유는 한 단계 더 강하다 - 조건이 "세계를 한 바퀴 돌았는가"이고,
         * 그것은 레벨로 표현할 수 없는 사실이다(YodoCurve.UnlockStage 주석).
         */
        public bool IsUnlocked { get { return YodoCurve.IsUnlockedAt(StageNow); } }

        private int StageNow
        {
            // 현재 스테이지가 아니라 최전선이다(37단계 재선택). "한 바퀴를
            // 돌았는가"는 되돌아가도 참으로 남아야 한다 - EquipmentSystem과
            // 같은 판단이다
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        // ---------------------------------------------------------------- 값

        /** 봉인한 요도 수 (0~4). 세트 보너스와 오니키리 완성이 읽는다 */
        public int SealedCount
        {
            get
            {
                if (blades == null) return 0;

                int count = 0;
                foreach (var blade in blades)
                    if (blade != null && blade.Sealed) count++;
                return count;
            }
        }

        public bool IsOnikiriComplete { get { return YodoCurve.IsComplete(SealedCount); } }

        public double SetBonus { get { return YodoCurve.SetBonusAt(SealedCount); } }

        /**
         * @brief 공격력에 곱해질 총 배수. 요도가 없으면 1.
         *
         * **1로 떨어지는 것이 중요하다.** 전투 전용 테스트 씬은 YodoSystem
         * 없이 전투만 세우는데, 거기서 0이 되면 공격력이 통째로 사라진다.
         * EquipmentSystem.MultiplierFor와 같은 규칙이다.
         */
        public double AttackMultiplier
        {
            get
            {
                if (blades == null) return 1d;

                // 곱의 순서는 곡선이 정한다(YodoCurve.AttackFactorFor) -
                // 47단계에 항이 둘 늘면서 이 곱을 부르는 곳이 셋이 됐고,
                // 각자 곱하면 화면과 데미지가 갈린다
                return YodoCurve.AttackFactorFor(Tiers(), Rarities(), Legends());
            }
        }

        /** 씬에 YodoSystem이 없으면 1. UpgradeSystem.Apply가 참조 없이 읽어 간다 */
        public static double CurrentAttackMultiplier
        {
            get { return Instance != null ? Instance.AttackMultiplier : 1d; }
        }

        // ---------------------------------------------------------------- 45단계

        /**
         * @brief 상성·영체 계산에 넘길 티어 배열. **스크래치를 돌려 쓴다.**
         *
         * SkillSystem이 매 프레임 CastRate를 읽으므로(PlayerCombat.
         * EffectiveAttacksPerSecond) 여기서 배열을 새로 만들면 프레임마다
         * 쓰레기가 생긴다. 값을 채운 즉시 읽고 버리므로 상태가 남지 않는다 -
         * 시뮬레이션 쪽(StageSimulation.YodoScratch)과 같은 처방이고 같은 이유다.
         */
        private int[] tierScratch;
        private int[] rarityScratch;
        private int[] legendScratch;

        private int[] Tiers()
        {
            if (tierScratch == null || tierScratch.Length != BladeCount)
                tierScratch = new int[BladeCount];

            for (int i = 0; i < tierScratch.Length; i++)
                tierScratch[i] = blades[i] != null ? blades[i].tier : 0;
            return tierScratch;
        }

        /** 혼격 배열 (47단계). Tiers()와 같은 스크래치 규칙이다 */
        private int[] Rarities()
        {
            if (rarityScratch == null || rarityScratch.Length != BladeCount)
                rarityScratch = new int[BladeCount];

            for (int i = 0; i < rarityScratch.Length; i++)
                rarityScratch[i] = blades[i] != null ? blades[i].rarity : 0;
            return rarityScratch;
        }

        /** 전설 사본 배열 (47단계). 씬에 전설이 안 세워졌으면 길이 0이다 */
        private int[] Legends()
        {
            if (legendScratch == null || legendScratch.Length != LegendaryCount)
                legendScratch = new int[LegendaryCount];

            for (int i = 0; i < legendScratch.Length; i++)
                legendScratch[i] = legendaries[i] != null ? legendaries[i].copies : 0;
            return legendScratch;
        }

        /**
         * @brief skillIndex번 오의가 지금 받는 상성 배수. 요도가 없으면 1.
         *
         * **1로 떨어지는 것이 중요하다.** 전투 전용 테스트 씬은 YodoSystem
         * 없이 전투만 세우는데, 거기서 0이 되면 오의 데미지가 통째로
         * 사라진다 - AttackMultiplier와 같은 규칙이다.
         */
        public double AffinityForSkill(int skillIndex)
        {
            if (blades == null) return 1d;
            return YodoAffinityCurve.FactorForSkill(skillIndex, Tiers(), Rarities(), Legends());
        }

        /** 씬에 YodoSystem이 없으면 1. SkillSystem이 참조 없이 읽어 간다 */
        public static double CurrentAffinityForSkill(int skillIndex)
        {
            return Instance != null ? Instance.AffinityForSkill(skillIndex) : 1d;
        }

        /**
         * @brief 이 요도의 영체가 한 번 소환됐을 때의 총 배율. 미봉인이면 0.
         *
         * 봉인한 자루만 값을 갖는다 - 영체는 **혼을 봉인한 결과**이지
         * 혼 자체가 아니다.
         */
        public double SpiritMultiplierOf(int index)
        {
            var blade = GetBlade(index);
            if (blade == null) return 0d;

            // 전설 요도는 무대에 서지 않고 서 있는 것을 키운다
            // (LegendaryYodoCurve 머리 주석) - 그래서 한 번의 소환 총량에도
            // 곱해진다. 초당 환산(SpiritRate)과 실제 타격이 같은 값에서
            // 나와야 26단계의 "패널과 데미지가 같은 숫자" 규칙이 선다
            return YodoSpiritCurve.MultiplierAtTier(blade.tier, blade.rarity)
                 * LegendaryYodoCurve.SpiritFactor(Legends());
        }

        /**
         * @brief 소환 순번 turn에 나올 요도. 봉인한 자루가 없으면 -1.
         *
         * 순번 자체는 여기서 세지 않는다. 쿨다운을 돌리는 쪽(SpiritSummon)이
         * 세는 것이 맞고, 그 값은 저장하지 않는다 - 오의 쿨다운과 같은 규칙이다
         * (YodoSpiritCurve.BladeForTurn 주석).
         */
        public int SpiritBladeForTurn(int turn)
        {
            if (blades == null) return -1;
            return YodoSpiritCurve.BladeForTurn(turn, Tiers());
        }

        /** 지금 영체가 만드는 초당 환산 기여. 스탯 화면과 계측이 읽는다 */
        public double SpiritRate
        {
            get
            {
                return blades == null ? 0d
                     : YodoSpiritCurve.RateFor(Tiers(), Rarities(), Legends());
            }
        }

        public static double CurrentSpiritRate
        {
            get { return Instance != null ? Instance.SpiritRate : 0d; }
        }

        /** 이 자루를 한 티어 올렸을 때의 총 배수. 화면의 "전 -> 후"가 쓴다 */
        public double NextMultiplierOf(int index)
        {
            var blade = GetBlade(index);
            if (blade == null) return AttackMultiplier;
            if (blade.tier >= YodoCurve.MaxTier) return AttackMultiplier;

            // 나눗셈으로 되돌리지 않고 **티어 하나만 바꾼 배열로 다시 곱한다.**
            // 47단계에 항이 둘 늘면서 되돌릴 값이 넷이 됐고(티어·세트·혼격·
            // 전설), 그중 혼격은 티어와 함께 움직이지 않으므로 나눗셈으로는
            // 정확히 안 돌아온다 - 곱의 순서를 아는 곳은 곡선 하나여야 한다
            var tiers = Tiers();
            int saved = tiers[index];
            tiers[index] = saved + 1;

            double after = YodoCurve.AttackFactorFor(tiers, Rarities(), Legends());

            tiers[index] = saved;   // 스크래치는 빌려 쓰는 것이다 - 돌려놓는다
            return after;
        }

        // ---------------------------------------------------------------- 드랍

        /**
         * @brief 보스를 벴다. 혼이나 파편을 남기는지 판정한다.
         *
         * ## 왜 스테이지가 아니라 애셋을 받는가
         *
         * 42단계의 세계 순환 때문이다. "st90의 보스는 등롱"이라는 사실은
         * BossRoster.RegionForStage가 접어서 내는 답이고, 여기서 스테이지로
         * 다시 유도하면 같은 산수가 두 곳에 산다 - 로스터에 지역을 하나
         * 더하는 날 둘이 갈리고, 그 증상은 "등롱을 벴는데 흑야의 혼이 나온다"다.
         *
         * 그래서 보스 애셋이 자기 혼을 들고 있고(BossConfig.soulId) 이 함수는
         * 그것을 읽기만 한다. 13단계가 보스 배치를 코드에서 애셋으로 옮긴
         * 규칙 그대로다.
         *
         * ## 해금 전에는 아무것도 떨어지지 않는다
         *
         * 조율 구간(st1~50)의 밴드가 이 한 줄에 걸려 있다. 되살아난 요괴만
         * 혼을 남긴다는 설정이 곧 밸런스 게이트다(YodoCurve.UnlockStage).
         *
         * @param config 방금 벤 보스. null이면 잡몹 확대판이라 아무것도 없다
         * @param clearedStage 그 보스가 서 있던 스테이지
         */
        public void ReportBossDefeated(Onikiri.Battle.BossConfig config, int clearedStage)
        {
            if (!YodoCurve.IsUnlockedAt(clearedStage)) return;

            bool changed = false;

            // 정예(챕터)는 파편이다. 등급은 스테이지가 정한다 - 정예 애셋이
            // 네 지역에서 공유되므로 애셋에 적을 수 있는 사실이 아니다
            if (YodoCurve.DropsShardsAt(clearedStage))
            {
                AddShards(YodoCurve.ShardsPerElite);
                changed = true;
            }

            if (config != null && !string.IsNullOrEmpty(config.soulId))
            {
                int index = IndexOf(config.soulId);
                if (index >= 0 && RollDrop(index))
                {
                    var blade = blades[index];

                    // 상한에 닿은 요도에게 온 혼은 갈 곳이 없다. 곧바로
                    // 파편으로 바꾼다 - 버려지는 드랍을 0으로 만드는 유일한
                    // 경로이고, 화면에서도 "혼 -> 파편"으로 한 번에 읽힌다
                    if (blade.tier >= YodoCurve.MaxTier) AddShards(YodoCurve.ShardsPerOverflowSoul);
                    else blade.souls = Add(blade.souls, 1);

                    blade.discovered = true;
                    changed = true;
                }
            }

            if (changed) Raise();
        }

        /**
         * @brief 이 혼이 실제로 떨어졌는가.
         *
         * 지금은 확률이 넷 다 1이라 언제나 참이다. 그래도 굴리는 이유는
         * YodoSpec.DropChance 주석에 있다 - 다음 스텝이 표만 고치면 되게
         * 경로를 미리 세워 둔다. 확률이 1일 때 Random을 부르지 않는 것은
         * 결정성 때문이다: 시뮬레이션과 실제가 같은 값을 내야 하고, 난수
         * 소비가 다른 뽑기(스폰 가중치)의 수열을 밀면 재현이 어려워진다.
         */
        private bool RollDrop(int index)
        {
            double chance = YodoCatalog.Blades[index].DropChance;
            if (chance >= 1d) return true;
            if (chance <= 0d) return false;
            return UnityEngine.Random.value < chance;
        }

        // ---------------------------------------------------------------- 46단계: 혼 정수

        /**
         * @brief 뽑기가 준 혼 정수를 받는다. 받은 자루의 번호, 없으면 -1.
         *
         * ## 왜 여기가 받는가
         *
         * 상한 판정(GachaCurve.EssenceSoulCap)이 요도의 상태를 읽어야 하기
         * 때문이다. 뽑기 쪽에서 판정하면 티어·혼 배열의 사본을 그쪽이 들고
         * 있어야 하고, 그 사본은 반드시 갈린다 - 44단계가 보스 애셋을 단일
         * 출처로 삼은 것과 같은 규칙이다.
         *
         * ## 거절이 정상이다
         *
         * 미봉인(첫 봉인은 그 요괴를 벤 사람의 것이다), 상한 티어, 그리고
         * **리드 상한**(드랍 일정보다 한 바퀴 이상 앞설 수 없다) 셋 중
         * 하나면 거절한다. 거절당한 정수를 무엇으로 바꿀지는 부른 쪽이
         * 정한다 - 여기서 파편으로 바꿔 버리면 "정수를 받았다"와 "파편을
         * 받았다"가 한 함수 안에서 뒤섞여 화면이 어느 쪽을 그릴지 알 수 없다.
         *
         * @param frontierStage 최전선. 리드 상한이 이 값에서 유도된다
         */
        public int TryTakeEssence(int frontierStage)
        {
            if (blades == null) return -1;

            int target = GachaCurve.EssenceTargetFor(frontierStage, Tiers(), Souls());
            if (target < 0) return -1;

            blades[target].souls = Add(blades[target].souls, 1);
            blades[target].discovered = true;
            Raise();
            return target;
        }

        // ---------------------------------------------------------------- 47단계: ★4·★5

        /**
         * @brief 뽑기가 준 상위 혼(★4)을 받는다. 받은 자루의 번호, 없으면 -1.
         *
         * TryTakeEssence와 같은 자리, 같은 이유다 - 상한 판정
         * (YodoRarityCurve.CapAt)이 요도의 상태와 최전선을 읽어야 하므로 요도가
         * 판정한다. 거절이 정상이고(혼격이 티어를 앞설 수 없다), 거절당한
         * 것을 무엇으로 바꿀지는 부른 쪽이 정한다.
         */
        public int TryTakeRarity()
        {
            if (blades == null) return -1;

            int target = GachaCurve.RarityTargetFor(StageNow, Tiers(), Rarities());
            if (target < 0) return -1;

            blades[target].rarity++;
            blades[target].discovered = true;

            // 혼격은 공격력·상성·영체 셋 다 바꾼다 - 티어업과 같은 크기의
            // 사건이므로 스탯 반영도 같은 자리에서 한다
            ApplyToStats();
            Raise();

            var handler = RarityGained;
            if (handler != null) handler(target);
            return target;
        }

        /**
         * @brief 뽑기가 준 전설(★5)을 받는다. 받은 자루의 번호, 없으면 -1.
         *
         * 두 자루가 다 상한 사본이면 -1이고, 그때만 파편이 된다
         * (LegendaryYodoCurve.ShardsPerOverflow). 그 전까지는 중복이
         * **돌파**라 언제나 갈 곳이 있다 - "★5가 중복이라 아무것도 아니다"가
         * 되지 않는 것이 이 축의 계약이다.
         */
        public int TryTakeLegendary()
        {
            if (legendaries == null) return -1;

            int target = GachaCurve.LegendaryTargetFor(Legends());
            if (target < 0 || target >= legendaries.Length) return -1;
            if (legendaries[target] == null) return -1;

            legendaries[target].copies++;

            ApplyToStats();
            Raise();

            var handler = LegendaryGained;
            if (handler != null) handler(target);
            return target;
        }

        /** 혼격이 올랐다. 연출과 도감이 듣는다 (Forged와 같은 자리) */
        public event Action<int> RarityGained;

        /** 전설이 들어왔다. 획득 연출이 듣는다 */
        public event Action<int> LegendaryGained;

        /**
         * @brief 파편을 넣는다. **뽑기가 쓰는 문이다.**
         *
         * DebugGrantShards와 나눠 두는 이유는 저쪽이 치트이기 때문이다 -
         * 이름이 Debug로 시작하는 함수를 실제 경로가 부르면, 언젠가 치트를
         * 지우면서 게임이 함께 부서진다.
         */
        public void GrantShards(long amount)
        {
            if (amount <= 0L) return;
            AddShards(amount);
            Raise();
        }

        /** 상한 판정에 넘길 혼 배열. Tiers()와 같은 스크래치 규칙이다 */
        private long[] soulScratch;

        private long[] Souls()
        {
            if (soulScratch == null || soulScratch.Length != BladeCount)
                soulScratch = new long[BladeCount];

            for (int i = 0; i < soulScratch.Length; i++)
                soulScratch[i] = blades[i] != null ? blades[i].souls : 0L;
            return soulScratch;
        }

        // ---------------------------------------------------------------- 봉인·합성

        /** 이 자루를 한 티어 올릴 수 있는 재료가 있는가 (해금·상한 포함) */
        public bool CanForge(int index)
        {
            var blade = GetBlade(index);
            if (blade == null || !IsUnlocked) return false;
            if (blade.tier >= YodoCurve.MaxTier) return false;
            if (blade.souls < YodoCurve.SoulsPerTier) return false;

            return shards >= ShardCostOf(index);
        }

        /** 재료 중 혼만 있는가. 화면이 "파편 부족"을 구분해 적는다 */
        public bool HasSoulFor(int index)
        {
            var blade = GetBlade(index);
            return blade != null && blade.tier < YodoCurve.MaxTier
                                 && blade.souls >= YodoCurve.SoulsPerTier;
        }

        /** 봉인(0 -> 1)은 파편이 들지 않는다. 합성부터 재료가 붙는다 */
        public int ShardCostOf(int index)
        {
            var blade = GetBlade(index);
            if (blade == null || blade.tier == 0) return 0;
            return YodoCurve.ShardCostAtTier(blade.tier);
        }

        public bool IsMaxed(int index)
        {
            var blade = GetBlade(index);
            return blade != null && blade.tier >= YodoCurve.MaxTier;
        }

        /**
         * @brief 봉인 또는 합성. **버튼 하나가 둘 다 한다.**
         *
         * 티어 0에서 누르면 봉인이고 그 위에서는 합성이다. 버튼을 나누지 않은
         * 이유는 재료와 결과가 같은 종류이기 때문이다 - 혼 하나가 들어가고
         * 티어가 하나 오른다. 다른 것은 파편 값과 화면의 동사뿐이고, 그
         * 둘은 같은 버튼이 상태에 따라 바꿔 적으면 된다(YodoRow).
         *
         * 장비의 단련/등급업이 버튼 둘인 것과 대비된다. 저쪽은 **재화가
         * 갈려서**(골드 / 보석+골드) 나눈 것이고, 여기는 안 갈린다.
         */
        public bool TryForge(int index)
        {
            if (!CanForge(index)) return false;

            var blade = blades[index];
            bool wasSeal = blade.tier == 0;

            shards -= ShardCostOf(index);
            blade.souls -= YodoCurve.SoulsPerTier;
            blade.tier++;

            ApplyToStats();
            Raise();

            var handler = Forged;
            if (handler != null) handler(index, wasSeal);
            return true;
        }

        // ---------------------------------------------------------------- 촉매

        public bool CanBuyShards
        {
            get
            {
                if (!IsUnlocked) return false;
                if (gems == null) gems = GemWallet.Instance;
                return gems != null && gems.CanAfford(YodoCurve.ShardPackGems);
            }
        }

        /**
         * @brief 보석으로 파편 한 묶음. **이 스텝의 새 보석 소비처다.**
         *
         * 재화가 하나뿐이라 장비 등급업 같은 두 지갑 순서 문제가 없다.
         */
        public bool TryBuyShards()
        {
            if (!CanBuyShards) return false;
            if (!gems.TrySpend(YodoCurve.ShardPackGems)) return false;

            AddShards(YodoCurve.ShardPackShards);
            Raise();
            return true;
        }

        // ---------------------------------------------------------------- 배지

        /**
         * @brief 지금 누를 수 있는 버튼이 하나라도 있는가. **탭 배지가 본다.**
         *
         * 파편 묶음은 세지 않는다. 배지는 "가서 할 일이 있다"는 뜻인데,
         * 보석이 있다는 사실만으로 배지가 켜지면 그 배지는 영원히 켜져
         * 있는다 - EquipmentSystem.AnyAffordable이 같은 기준을 쓴다.
         */
        public int ForgeableCount
        {
            get
            {
                if (!IsUnlocked) return 0;

                int count = 0;
                for (int i = 0; i < BladeCount; i++) if (CanForge(i)) count++;
                return count;
            }
        }

        public bool AnyForgeable { get { return ForgeableCount > 0; } }

        // ---------------------------------------------------------------- 적용

        /**
         * @brief 요도 배수를 전투 스탯에 다시 먹인다.
         *
         * EquipmentSystem.ApplyToStats와 같다 - 곱해지는 값은 자기 자리에
         * 저장되지 않고 강화 값 위에 얹히므로, 반영 경로를 UpgradeSystem
         * 하나로 남긴다.
         */
        private void ApplyToStats()
        {
            if (upgrades == null) upgrades = UpgradeSystem.Instance;
            if (upgrades != null) upgrades.ApplyAll();
        }

        private void AddShards(long amount)
        {
            shards = Add(shards, amount);
        }

        /** 손상된 세이브가 long 끝을 들고 와도 음수로 돌지 않게 (GemWallet.Add 규칙) */
        private static long Add(long current, long amount)
        {
            if (amount <= 0L) return current;
            return current > long.MaxValue - amount ? long.MaxValue : current + amount;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < BladeCount; i++)
                if (blades[i] != null && blades[i].id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- 세이브

        public string[] CollectIds()
        {
            var ids = new string[BladeCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = blades[i] != null ? blades[i].id : string.Empty;
            return ids;
        }

        public long[] CollectSouls()
        {
            var values = new long[BladeCount];
            for (int i = 0; i < values.Length; i++) values[i] = blades[i] != null ? blades[i].souls : 0L;
            return values;
        }

        public int[] CollectTiers()
        {
            var values = new int[BladeCount];
            for (int i = 0; i < values.Length; i++) values[i] = blades[i] != null ? blades[i].tier : 0;
            return values;
        }

        public int[] CollectDiscovered()
        {
            var values = new int[BladeCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = blades[i] != null && blades[i].discovered ? 1 : 0;
            return values;
        }

        public int[] CollectRarities()
        {
            var values = new int[BladeCount];
            for (int i = 0; i < values.Length; i++) values[i] = blades[i] != null ? blades[i].rarity : 0;
            return values;
        }

        public string[] CollectLegendaryIds()
        {
            var ids = new string[LegendaryCount];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = legendaries[i] != null ? legendaries[i].id : string.Empty;
            return ids;
        }

        public int[] CollectLegendaryCopies()
        {
            var values = new int[LegendaryCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = legendaries[i] != null ? legendaries[i].copies : 0;
            return values;
        }

        public long CollectShards() { return shards; }

        /**
         * @brief 세이브 복원.
         *
         * **티어를 자르지 않는다.** UpgradeTrack.SetLevel·EquipmentSystem과
         * 같은 규칙이다 - 상한이 내려간 업데이트에서 플레이어가 모은 티어가
         * 영구히 사라지면 안 된다. 값은 YodoCurve.TierValue가 자르고, 상한이
         * 다시 오르면 잠든 티어가 깨어난다.
         *
         * 세이브에 있지만 지금은 없는 요도는 조용히 건너뛴다 - 강화 축·오의·
         * 장비 복원과 같은 안전장치다.
         */
        public void Restore(string[] ids, long[] souls, int[] tiers, int[] discovered,
                            long savedShards, int[] rarities = null,
                            string[] legendIds = null, int[] legendCopies = null)
        {
            shards = savedShards < 0L ? 0L : savedShards;

            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    int index = IndexOf(ids[i]);
                    if (index < 0) continue;

                    var blade = blades[index];
                    if (souls != null && i < souls.Length) blade.souls = Math.Max(0L, souls[i]);
                    if (tiers != null && i < tiers.Length) blade.tier = Math.Max(0, tiers[i]);

                    // **혼격도 자르지 않는다.** 티어와 같은 규칙이고 같은
                    // 이유다 - 상한(YodoRarityCurve.MaxRarity·CapAt)이
                    // 내려간 업데이트에서 플레이어가 뽑은 격이 영구히
                    // 사라지면 안 된다. 값은 ValueAt이 자르고, 상한이 다시
                    // 오르면 잠든 격이 깨어난다
                    if (rarities != null && i < rarities.Length)
                        blade.rarity = Math.Max(0, rarities[i]);

                    // 봉인한 적이 있으면 본 적도 있다. 발견 플래그가 없는
                    // 세이브(마이그레이션 직후)에서도 도감이 거짓말하지 않게
                    bool seen = discovered != null && i < discovered.Length && discovered[i] != 0;
                    blade.discovered = seen || blade.tier >= 1 || blade.souls > 0L;
                }
            }

            // 세이브에 있지만 지금은 없는 전설은 조용히 건너뛴다 - 위와 같은
            // 안전장치이고, 풀이 늘어나는 스텝(妖刀 확장)에서 실제로 쓰인다
            if (legendIds != null && legendaries != null)
            {
                for (int i = 0; i < legendIds.Length; i++)
                {
                    int index = LegendaryIndexOf(legendIds[i]);
                    if (index < 0) continue;

                    if (legendCopies != null && i < legendCopies.Length)
                        legendaries[index].copies = Math.Max(0, legendCopies[i]);
                }
            }

            ApplyToStats();
            Raise();
        }

        public int LegendaryIndexOf(string id)
        {
            for (int i = 0; i < LegendaryCount; i++)
                if (legendaries[i] != null && legendaries[i].id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- 테스트 패널

        /**
         * @brief 요도를 새 게임 상태로 되돌린다. **테스트 패널 전용.**
         *
         * **보석은 돌려주지 않는다.** 환불은 초기화가 아니라 별개의 치트이고,
         * 패널 위쪽에 이미 자기 버튼이 있다 - EquipmentSystem.DebugResetEquipment와
         * 같은 규칙이다.
         */
        public void DebugReset()
        {
            for (int i = 0; i < BladeCount; i++)
            {
                if (blades[i] == null) continue;
                blades[i].souls = 0L;
                blades[i].tier = 0;
                blades[i].rarity = 0;
                blades[i].discovered = false;
            }
            for (int i = 0; i < LegendaryCount; i++)
                if (legendaries[i] != null) legendaries[i].copies = 0;

            shards = 0L;

            ApplyToStats();
            Raise();
        }

        /** 혼 하나를 재화 없이 넣는다. 드랍을 기다리지 않고 도감을 켜는 경로 */
        public void DebugGrantSoul(int index)
        {
            var blade = GetBlade(index);
            if (blade == null) return;

            blade.souls = Add(blade.souls, 1);
            blade.discovered = true;
            Raise();
        }

        public void DebugGrantShards(long amount)
        {
            AddShards(amount);
            Raise();
        }

        /** 재료를 무시하고 한 티어 올린다. 곡선을 눈으로 훑는 경로 */
        public void DebugForge(int index)
        {
            var blade = GetBlade(index);
            if (blade == null || blade.tier >= YodoCurve.MaxTier) return;

            bool wasSeal = blade.tier == 0;
            blade.tier++;
            blade.discovered = true;

            ApplyToStats();
            Raise();

            var handler = Forged;
            if (handler != null) handler(index, wasSeal);
        }

        /**
         * @brief 상한을 무시하고 혼격을 한 칸 올린다. 곡선을 눈으로 훑는 경로.
         *
         * DebugForge와 같은 규칙이다 - 실제 경로(TryTakeRarity)는 티어
         * 게이트를 지키고 치트는 안 지킨다. 지키면 "혼격을 보려면 티어를
         * 여덟까지 올려라"가 되어 패널이 그 상태에 못 닿는다.
         */
        public void DebugGrantRarity(int index)
        {
            var blade = GetBlade(index);
            if (blade == null || blade.rarity >= YodoRarityCurve.MaxRarity) return;

            blade.rarity++;
            blade.discovered = true;

            ApplyToStats();
            Raise();

            var handler = RarityGained;
            if (handler != null) handler(index);
        }

        /** 전설 사본 하나를 재화 없이 넣는다. 200회를 안 돌리고 보는 경로 */
        public void DebugGrantLegendary(int index)
        {
            var blade = GetLegendary(index);
            if (blade == null || blade.copies >= LegendaryYodoCurve.MaxCopies) return;

            blade.copies++;

            ApplyToStats();
            Raise();

            var handler = LegendaryGained;
            if (handler != null) handler(index);
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
