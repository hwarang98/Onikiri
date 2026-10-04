using System;
using System.Collections.Generic;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 요괴 봉인 뽑기. **보석과 하루가 이 시스템의 두 입구다.**
     *
     * ## 왜 YodoSystem 안이 아닌가
     *
     * 파는 것이 요도의 재료뿐이라 한 시스템으로 접고 싶어진다. 그런데 둘이
     * 듣는 것이 다르다 - 요도는 **전투 사건**(보스 처치)에 반응하고, 뽑기는
     * **지갑과 달력**에 반응한다. 44단계가 요도를 EquipmentSystem에서
     * 떼어낸 것과 같은 기준이고, 여기서는 한 가지가 더 있다: 뽑기는 다음
     * 스텝에 현금과 광고가 붙는 자리다. 결제 콜백이 YodoSystem 안으로
     * 들어오면 "요괴 봉인 검"이 결제 모듈이 된다.
     *
     * ## 결과를 이 시스템이 적용하지 않는다
     *
     * 뽑은 것은 파편이거나 혼 정수인데, 둘 다 **요도의 상태**다. 여기서
     * 직접 배열을 만지면 상한 판정(GachaCurve.EssenceSoulCap)이 두 곳에
     * 살게 되고, 그 둘은 반드시 갈린다. 그래서 이 시스템은 표를 굴리고
     * YodoSystem에 넘기기만 한다 - 넘길 곳이 없으면(미봉인·상한) 그쪽이
     * 거절하고, 거절당한 정수는 파편이 된다(44단계의 넘침 규칙 그대로).
     */
    public sealed class GachaSystem : MonoBehaviour
    {
        /**
         * @brief 한 번의 뽑기 결과. 화면의 연출이 이 줄을 그린다.
         */
        public struct PullResult
        {
            public GachaCurve.Outcome Outcome;

            /** 표시 등급. 색·별·연출이 이것을 읽는다 (47단계) */
            public GachaCurve.Grade Grade;

            /** 실제로 들어온 파편. 넘친 정수가 파편이 된 경우도 여기 들어온다 */
            public int Shards;

            /** 혼 정수·상위 혼이 들어간 요도. -1이면 갈 곳이 없어 파편이 됐다 */
            public int BladeIndex;

            /** 전설이 들어간 자루. -1이면 미해당이거나 상한이라 파편이 됐다 */
            public int LegendaryIndex;

            /**
             * @brief 상위 혼(★4)이 혼 정수(★3)로 미끄러졌는가.
             *
             * 혼격 상한이 바퀴에 묶여 있으므로(YodoRarityCurve.CapAt)
             * ★4가 갈 곳이 없는 순간이 실제로 자주 온다. 그때 결과를
             * "혼 정수"라고만 적으면 플레이어는 등급이 내려간 것을 모르고,
             * 반대로 "상위 혼"이라고만 적으면 대장간에 갔다가 혼격이 안
             * 오른 것을 본다 - 44단계의 "버려지는 드랍 0"을 문구로 지키는
             * 자리이고, 46단계가 넘침에 대해 한 것과 같은 처방이다.
             */
            public bool Downgraded;
        }

        [SerializeField] private GemWallet gems;
        [SerializeField] private YodoSystem yodo;
        [SerializeField] private StageProgress stage;

        /**
         * @brief 이 배너의 소환 경험치 = 지금까지 돌린 횟수 (68단계).
         *
         * 저장한다(세이브 v23). 47단계 천장 카운터가 서던 자리이고 저장하는
         * 이유도 같다 - **플레이어가 지불한 것**이다. 다른 점은 이 값이 0으로
         * 돌아가지 않는다는 것이다. 천장은 터질 때마다 비워졌지만 경험치는
         * 쌓이기만 하고, 레벨(SummonLevel)은 이 값에서 유도된다.
         */
        [SerializeField] private long summonXp;

        /** 지금까지 돌린 총 횟수. 상점이 "지금까지 N회"로 적는다 */
        [SerializeField] private int totalPulls;

        /**
         * @brief 마지막으로 무료 뽑기를 쓴 **퀘스트일** (UTC ticks).
         *
         * 시각이 아니라 날짜다. 시각을 저장하고 24시간을 재면 매일 조금씩
         * 늦어지는 리셋이 되고(어제 23시에 뽑았으면 오늘 23시까지 못 뽑는다),
         * 그것은 리텐션 장치가 아니라 벌이다. 일일 퀘스트가 같은 이유로 같은
         * 방식을 쓴다(QuestSystem.lastDailyReset).
         */
        [SerializeField] private long lastFreePullDayTicks;

        /** 잔액·소환 레벨·무료 상태 중 무엇이든 바뀌면 발생. 상점이 듣는다 */
        public event Action Changed;

        /** 방금 뽑았다. 연출이 듣는다 - 결과 목록이 함께 온다 */
        public event Action<List<PullResult>> Pulled;

        /**
         * @brief 소환 레벨이 올랐다. 새 레벨이 온다 - **오른 레벨마다 한 번.**
         *
         * Pulled보다 먼저 온다. 결과 팝업은 같은 묶음의 LastBatchLevelUp을
         * 읽어 제목 줄에 한 줄을 붙인다. 연출은 69단계(UI 개편) 몫이다.
         */
        public event Action<int> SummonLevelUp;

        public static GachaSystem Instance { get; private set; }

        public int TotalPulls { get { return totalPulls; } }

        /** 소환 경험치 (누적 뽑기 수) */
        public long SummonXp { get { return summonXp; } }

        /** 소환 레벨. 경험치에서 유도된다 - 저장하지 않는다 */
        public int SummonLevel { get { return SummonLevelCurve.LevelFor(summonXp); } }

        /** 마지막 뽑기 묶음에서 닿은 레벨. 0이면 안 올랐다 - 결과 팝업이 읽는다 */
        public int LastBatchLevelUp { get; private set; }

        /**
         * @brief 결과를 담아 넘기는 목록. **매 뽑기마다 새로 만들지 않는다.**
         *
         * 10연이 프레임마다 도는 것은 아니지만, 듣는 쪽이 목록을 보관하지
         * 않는다는 계약을 코드로 적어 두는 편이 낫다 - 보관하면 다음 뽑기가
         * 그 목록을 덮어쓴다. YodoSystem.tierScratch와 같은 규칙이다.
         */
        private readonly List<PullResult> results = new List<PullResult>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second GachaSystem appeared; keeping the first.");
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
            Raise();
        }

        // ---------------------------------------------------------------- 해금

        private int StageNow
        {
            // 현재 스테이지가 아니라 최전선이다 - 요도와 같은 규칙이고 같은
            // 이유다(YodoSystem.StageNow). 상한 판정도 이 값을 쓴다
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        public bool IsUnlocked { get { return GachaCurve.IsUnlockedAt(StageNow); } }

        // ---------------------------------------------------------------- 비용

        public int CostFor(int count)
        {
            if (count >= GachaCurve.TenPullCount) return GachaCurve.TenPullCostGems;
            return GachaCurve.PullCostGems * Mathf.Max(1, count);
        }

        public bool CanPull(int count)
        {
            if (!IsUnlocked) return false;
            if (gems == null) gems = GemWallet.Instance;
            return gems != null && gems.CanAfford(CostFor(count));
        }

        // ---------------------------------------------------------------- 일일 무료

        /**
         * @brief 오늘의 무료 뽑기가 남아 있는가.
         *
         * ⚠️ 기기 시간 조작은 막지 않는다. 서버가 없으므로 UtcNow를 믿는 것
         * 말고 할 수 있는 것이 없고, 그 판단은 일일 퀘스트에서 이미 내렸다
         * (QuestSystem.RollDailyIfNeeded).
         */
        public bool HasFreePull { get { return HasFreePullAt(DateTime.UtcNow); } }

        public bool HasFreePullAt(DateTime utcNow)
        {
            if (!IsUnlocked) return false;
            if (lastFreePullDayTicks <= 0L) return true;

            var last = new DateTime(lastFreePullDayTicks, DateTimeKind.Utc);
            return GachaCurve.DayOf(utcNow) > GachaCurve.DayOf(last);
        }

        /** 다음 무료 뽑기까지 남은 시간. 상점의 타이머가 읽는다 */
        public TimeSpan UntilFreePull(DateTime utcNow)
        {
            if (HasFreePullAt(utcNow)) return TimeSpan.Zero;

            var left = QuestSystem.NextResetUtc(utcNow) - utcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /**
         * @brief 무료 뽑기 한 번. 보석이 들지 않는다.
         *
         * **f2p가 이 시스템에 닿는 유일한 경로다.** 무과금의 보석은 장비
         * 등급·동료 해금·전직에 이미 다 배정돼 있고(44단계 실측), 뽑기가
         * 그것을 가져가면 코어 진행이 무너진다 - 그래서 이 버튼이 있다
         * (GachaCurve.FreePullsPerDay 주석).
         */
        public bool TryFreePull()
        {
            return TryFreePullAt(DateTime.UtcNow);
        }

        public bool TryFreePullAt(DateTime utcNow)
        {
            // 무료 뽑기는 지불이 없어 지갑을 안 지난다. 그래서 여기에도 한 줄 둔다
            // ★ 63단계: 다른 기기가 인수했다. **재화는 여기서 멈춘다** -
            // 올라가지 못하는 진행을 더 쌓는 것은 나중에 버릴 것을 만드는 일이다
            if (Onikiri.Cloud.CloudSavePlayLock.Locked) return false;

            if (!HasFreePullAt(utcNow)) return false;

            lastFreePullDayTicks = GachaCurve.DayOf(utcNow).Ticks;
            RunPulls(GachaCurve.FreePullsPerDay);
            return true;
        }

        // ---------------------------------------------------------------- 뽑기

        /**
         * @brief 보석으로 count번 뽑는다. 10연은 값이 따로 붙는다(CostFor).
         *
         * 지불과 뽑기를 한 함수에 둔 이유는 UpgradeTrack.TryPurchase와 같다 -
         * 호출부에 맡기면 언젠가 한쪽만 실행되는 경로가 생긴다.
         */
        public bool TryPull(int count)
        {
            if (!CanPull(count)) return false;
            if (!gems.TrySpend(CostFor(count))) return false;

            RunPulls(count);
            return true;
        }

        /**
         * @brief count번 굴린다. **회차마다 그 순간의 레벨로 굴리고 1 XP를 쌓는다.**
         *
         * 10연 도중에 레벨이 오르면 남은 회차는 오른 레벨로 굴린다 - 시뮬레이션이
         * 뽑기 한 번을 단위로 세는 것과 같은 규칙이다(StageSimulation.TryGacha).
         */
        private void RunPulls(int count)
        {
            results.Clear();
            LastBatchLevelUp = 0;

            int before = SummonLevel;

            for (int i = 0; i < count; i++)
            {
                results.Add(RollOnce());
                summonXp++;
            }

            totalPulls += count;

            int after = SummonLevel;
            if (after > before)
            {
                LastBatchLevelUp = after;

                var levelUp = SummonLevelUp;
                if (levelUp != null)
                    for (int level = before + 1; level <= after; level++) levelUp(level);
            }

            Raise();

            var handler = Pulled;
            if (handler != null) handler(results);
        }

        /**
         * @brief 한 번 굴린다. **지금의 소환 레벨로 표를 굴리고, 그 값이 결과다.**
         *
         * 68단계에 천장이 사라져 덮어쓰는 단계가 없다. 시뮬레이션은 같은
         * 표(SummonLevelCurve.ChancesAt)의 기댓값으로 센다.
         */
        private PullResult RollOnce()
        {
            var outcome = GachaCurve.Roll(UnityEngine.Random.value, SummonLevel);

            switch (outcome)
            {
                case GachaCurve.Outcome.SoulEssence:  return GrantEssence();
                case GachaCurve.Outcome.SoulRarity:   return GrantRarity();
                case GachaCurve.Outcome.LegendaryBlade: return GrantLegendary();
            }

            int shards = GachaCurve.ShardsFor(outcome);
            if (yodo != null) yodo.GrantShards(shards);

            return Result(outcome, shards, -1, -1, false);
        }

        /** 결과 한 줄. 등급은 언제나 표에서 나온다 - 화면이 따로 판정하지 않게 */
        private static PullResult Result(GachaCurve.Outcome outcome, int shards, int blade,
                                         int legendary, bool downgraded)
        {
            return new PullResult
            {
                Outcome = outcome,
                Grade = GachaCurve.GradeFor(outcome),
                Shards = shards,
                BladeIndex = blade,
                LegendaryIndex = legendary,
                Downgraded = downgraded
            };
        }

        /**
         * @brief 혼 정수(★3)를 요도에 넘긴다. 갈 곳이 없으면 파편이 된다.
         *
         * 넘침 값이 **상한 요도의 혼과 같은 수**인 것이 요점이다
         * (YodoCurve.ShardsPerOverflowSoul). 44단계가 "버려지는 드랍을 0으로
         * 만드는 유일한 경로"라고 적은 규칙을 그대로 잇는다 - 뽑기에서
         * 아무것도 못 받는 결과가 생기면 그것은 확률이 아니라 사고로 읽힌다.
         */
        private PullResult GrantEssence()
        {
            int blade = yodo != null ? yodo.TryTakeEssence(StageNow) : -1;
            if (blade >= 0)
                return Result(GachaCurve.Outcome.SoulEssence, 0, blade, -1, false);

            int shards = YodoCurve.ShardsPerOverflowSoul;
            if (yodo != null) yodo.GrantShards(shards);

            return Result(GachaCurve.Outcome.SoulEssence, shards, -1, -1, false);
        }

        /**
         * @brief 상위 혼(★4)을 넘긴다. **두 번 미끄러진다.**
         *
         *   1. 혼격을 한 칸 올린다
         *   2. 막히면(티어가 못 받는다) **혼 정수로** 내려가 티어를 민다
         *   3. 그것도 막히면 파편 (GachaCurve.ShardsPerOverflowRarity)
         *
         * 사다리의 역순으로 미끄러지는 것이 요점이다. 혼격은 티어를 앞설 수
         * 없으므로(YodoRarityCurve.CapAt) 바퀴가 얕은 초반에는 ★4가
         * 자주 막히는데, 그때 곧바로 파편이 되면 **★4를 뽑고 받은 것이 파편**인
         * 순간이 생긴다 - 사다리 위쪽을 굴린 대가가 파편 80이면 그것은 상품이 아니다.
         *
         * 한 칸 내려가면 그 상황이 없어진다. ★4가 막히는 이유가 "티어가
         * 얕아서"이므로 내려간 자리가 정확히 **그 티어를 미는 물건**이고,
         * 다음 ★4는 받을 수 있게 된다. 막힘이 스스로를 푸는 구조다.
         */
        private PullResult GrantRarity()
        {
            int blade = yodo != null ? yodo.TryTakeRarity() : -1;
            if (blade >= 0)
                return Result(GachaCurve.Outcome.SoulRarity, 0, blade, -1, false);

            int fallback = yodo != null ? yodo.TryTakeEssence(StageNow) : -1;
            if (fallback >= 0)
                return Result(GachaCurve.Outcome.SoulRarity, 0, fallback, -1, true);

            int shards = GachaCurve.ShardsPerOverflowRarity;
            if (yodo != null) yodo.GrantShards(shards);

            return Result(GachaCurve.Outcome.SoulRarity, shards, -1, -1, true);
        }

        /**
         * @brief 전설(★5)을 넘긴다. 두 자루가 다 상한 사본이면 파편 뭉치다.
         *
         * 중복이 돌파이므로 상한 전까지는 언제나 갈 곳이 있다
         * (LegendaryYodoCurve.TargetFor). 미끄러짐이 한 단계뿐인 이유는
         * 전설에 "한 칸 아래"가 없기 때문이다 - 별개 풀이라 사다리에서
         * 내려올 자리가 없고, 그래서 넘침 값이 이 게임에서 가장 크다.
         */
        private PullResult GrantLegendary()
        {
            int blade = yodo != null ? yodo.TryTakeLegendary() : -1;
            if (blade >= 0)
                return Result(GachaCurve.Outcome.LegendaryBlade, 0, -1, blade, false);

            int shards = LegendaryYodoCurve.ShardsPerOverflow;
            if (yodo != null) yodo.GrantShards(shards);

            return Result(GachaCurve.Outcome.LegendaryBlade, shards, -1, -1, true);
        }

        // ---------------------------------------------------------------- 세이브

        public long CollectSummonXp() { return summonXp; }
        public int CollectTotalPulls() { return totalPulls; }
        public long CollectFreePullDay() { return lastFreePullDayTicks; }

        /**
         * @brief 세이브 복원.
         *
         * **무료 뽑기 날짜가 0이면 곧바로 쓸 수 있다.** 마이그레이션이
         * 그 값을 0으로 넣으므로(SaveData v14 -> v15) 기존 세이브는 접속하는
         * 순간 무료 뽑기 하나를 들고 있다 - "새 시스템이 열리는 것"이고
         * 소급이 아니다(v7 -> v8 업적과 같은 결).
         */
        public void Restore(long savedSummonXp, int savedTotal, long savedFreeDay)
        {
            // 음수만 막는다 - 위쪽은 상한이 없다(SummonLevelCurve 머리 주석)
            summonXp = savedSummonXp < 0L ? 0L : savedSummonXp;
            totalPulls = Mathf.Max(0, savedTotal);
            lastFreePullDayTicks = savedFreeDay < 0L ? 0L : savedFreeDay;

            Raise();
        }

        // ---------------------------------------------------------------- 테스트 패널

        /** 보석 없이 한 번 돌린다. 확률표를 눈으로 훑는 경로 */
        public void DebugPull(int count)
        {
            RunPulls(Mathf.Max(1, count));
        }

        /** 무료 뽑기를 다시 쓸 수 있게 한다. 하루를 기다리지 않는 경로 */
        public void DebugResetFreePull()
        {
            lastFreePullDayTicks = 0L;
            Raise();
        }

        /** 다음 레벨 직전까지 경험치를 민다. 레벨업을 수십 번 안 돌리고 보는 경로 */
        public void DebugPushToLevelUp()
        {
            summonXp += SummonLevelCurve.XpToNext(SummonLevel)
                      - SummonLevelCurve.XpIntoLevel(summonXp) - 1L;
            Raise();
        }

        public void DebugReset()
        {
            summonXp = 0L;
            totalPulls = 0;
            lastFreePullDayTicks = 0L;
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
