using System;
using System.Collections.Generic;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 오의 뽑기. **요괴 봉인 뽑기의 쌍둥이이고, 그 사실이 설계다.**
     *
     * ## 왜 GachaSystem 안이 아닌가
     *
     * 확률표도 천장도 가격도 같은 곳에서 오므로(SkillGachaCurve) 한 시스템에
     * 배너 두 개를 두고 싶어진다. 그런데 **천장 카운터가 갈려야 한다** -
     * 요도 뽑기 스물아홉 번이 오의 해금을 앞당기면 두 배너가 한 지갑이 아니라
     * 한 상품이 되고, 그러면 배너를 둘로 나눈 이유가 사라진다.
     *
     * 카운터가 갈리면 무료 뽑기 쿨도 갈려야 하고(하루에 요도 하나 · 오의
     * 하나), 누적 횟수도 갈려야 한다. 남는 공유가 확률표뿐이라면 그것은
     * **곡선이 나누는 것**이지 시스템이 나누는 것이 아니다 - 44단계가
     * YodoSystem을 EquipmentSystem에서 떼어낸 것과 같은 기준이다.
     *
     * ## 결과를 이 시스템이 적용하지 않는다
     *
     * GachaSystem이 요도에 넘기기만 하는 것과 똑같다. 뽑은 것은 XP거나
     * 해금이거나 개안인데 셋 다 **오의의 상태**이고, 여기서 직접 레벨을
     * 만지면 상한 판정(SkillCurve.MaxLevel)이 두 곳에 살게 된다. 그 둘은
     * 반드시 갈리고, 갈린 날 오의 몫 계약이 깨진다 - 이 스텝의 안전선이
     * 정확히 그 한 줄이므로 두 곳에 두면 안 된다.
     *
     * 그래서 이 시스템은 표를 굴리고 SkillSystem에 넘기기만 한다. 넘길 곳이
     * 없으면 그쪽이 거절하고, 거절당한 결과는 **사다리를 한 칸 미끄러진다**
     * (SkillGachaCurve.SlideFor).
     */
    public sealed class SkillGachaSystem : MonoBehaviour
    {
        /** 한 번의 뽑기 결과. 화면의 연출이 이 줄을 그린다 */
        public struct PullResult
        {
            /** 실제로 적용된 결과. 미끄러졌으면 도착한 칸이다 */
            public SkillGachaCurve.Outcome Outcome;

            /**
             * @brief 표가(또는 천장이) **낸** 결과. 미끄러지기 전의 칸이다.
             *
             * 47단계의 Downgraded는 bool 하나였고 그것으로 충분했다 - ★4가
             * 미끄러지는 곳이 ★3 하나뿐이라 도착점에서 출발점이 되짚어졌다.
             * 이쪽은 ★5가 **두 칸** 내려갈 수 있어서 ★3에 도착한 줄이
             * ★4에서 왔는지 ★5에서 왔는지 구분되지 않는다. 화면이 그 차이를
             * 적어야 하므로(200회에 한 번의 결과가 무엇이었는지) 출발점을 담는다.
             */
            public SkillGachaCurve.Outcome Rolled;

            /** 표시 등급. 색·별·연출이 이것을 읽는다 - 요도 배너와 같은 눈금이다 */
            public GachaCurve.Grade Grade;

            /** 실제로 들어온 스킬 XP. 미끄러져 XP가 된 경우도 여기 들어온다 */
            public int Xp;

            /** 그 XP가 올린 레벨 수. 0이면 게이지만 찼다 */
            public int LevelsGained;

            /** 해금된 오의. -1이면 미해당이거나 둘 다 이미 열려 있었다 */
            public int UnlockedIndex;

            /** 개안한 오의. -1이면 미해당이거나 장착이 전부 상한이었다 */
            public int AwakenedIndex;

            /** 천장이 준 결과인가 */
            public bool FromPity;

            /**
             * @brief 사다리를 미끄러졌는가.
             *
             * 47단계의 Downgraded와 같은 자리, 같은 이유다 - 결과를 도착한
             * 곳의 이름으로만 적으면 플레이어는 등급이 내려간 것을 모르고,
             * 출발한 곳의 이름으로만 적으면 화면과 실제가 갈린다. 두 이름을
             * 다 적는 것이 44단계의 "버려지는 드랍 0"을 문구로 지키는 방법이다.
             */
            public bool Downgraded;
        }

        [SerializeField] private GemWallet gems;
        [SerializeField] private SkillSystem skills;
        [SerializeField] private StageProgress stage;

        /**
         * @brief 마지막 ★4+ 뒤로 돌린 뽑기 수. **이 배너만의 카운터다.**
         *
         * GachaSystem.pityCounter와 갈라 두는 이유는 머리 주석에 있다.
         * 저장하는 이유는 같다 - 플레이어가 지불한 것이라, 29회에서 껐다
         * 켰더니 0으로 돌아가면 그것은 리셋이 아니라 몰수다.
         */
        [SerializeField] private int pityCounter;

        [SerializeField] private int totalPulls;

        /** 마지막으로 무료 뽑기를 쓴 **퀘스트일** (UTC ticks). 시각이 아니라 날짜다 */
        [SerializeField] private long lastFreePullDayTicks;

        public event Action Changed;
        public event Action<List<PullResult>> Pulled;

        public static SkillGachaSystem Instance { get; private set; }

        public int PityCounter { get { return pityCounter; } }
        public int TotalPulls { get { return totalPulls; } }
        public int PullsUntilPity { get { return SkillGachaCurve.PullsUntilPity(pityCounter); } }

        /** 결과를 담아 넘기는 목록. 듣는 쪽이 보관하지 않는다 - GachaSystem과 같은 계약 */
        private readonly List<PullResult> results = new List<PullResult>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second SkillGachaSystem appeared; keeping the first.");
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
            if (skills == null) skills = SkillSystem.Instance;

            // 재고는 오의의 상태에서 유도된다. 장착을 바꾸거나 레벨을 사면
            // 배너가 열리고 닫히므로 구독하지 않으면 화면이 한 박자 늦는다
            if (skills != null) skills.Changed += Raise;

            Raise();
        }

        private void OnDisable()
        {
            if (skills != null) skills.Changed -= Raise;
        }

        // ---------------------------------------------------------------- 해금 · 재고

        private int StageNow
        {
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        public bool IsUnlocked { get { return SkillGachaCurve.IsUnlockedAt(StageNow); } }

        /**
         * @brief 아직 팔 것이 남아 있는가. **다 팔리면 배너가 닫힌다.**
         *
         * 판정은 SkillSystem이 한다(HasStock) - 재고의 정의가 오의의 상태이고,
         * 그 상태를 아는 것은 그쪽이다. 여기서 다시 세면 두 곳이 갈린다.
         */
        public bool HasStock { get { return skills == null || skills.HasStock; } }

        /** 지금 뽑을 수 있는가. 해금 · 재고 · 지갑을 다 본다 */
        public bool CanPull(int count)
        {
            if (!IsUnlocked || !HasStock) return false;
            if (gems == null) gems = GemWallet.Instance;
            return gems != null && gems.CanAfford(CostFor(count));
        }

        public int CostFor(int count)
        {
            if (count >= SkillGachaCurve.TenPullCount) return SkillGachaCurve.TenPullCostGems;
            return SkillGachaCurve.PullCostGems * Mathf.Max(1, count);
        }

        // ---------------------------------------------------------------- 일일 무료

        /**
         * @brief 오늘의 무료 뽑기가 남아 있는가.
         *
         * **재고가 없으면 무료도 없다.** 아무것도 안 주는 무료 뽑기는 선물이
         * 아니라 매일 확인해야 하는 빈 우편함이다 - 41단계의 "안 눌리는 편이
         * 정직하다"가 여기서도 그대로다.
         *
         * ⚠️ 기기 시간 조작은 막지 않는다. 서버가 없고, 그 판단은 일일
         * 퀘스트에서 이미 내렸다(QuestSystem.RollDailyIfNeeded).
         */
        public bool HasFreePull { get { return HasFreePullAt(DateTime.UtcNow); } }

        public bool HasFreePullAt(DateTime utcNow)
        {
            if (!IsUnlocked || !HasStock) return false;
            if (lastFreePullDayTicks <= 0L) return true;

            var last = new DateTime(lastFreePullDayTicks, DateTimeKind.Utc);
            return SkillGachaCurve.DayOf(utcNow) > SkillGachaCurve.DayOf(last);
        }

        public TimeSpan UntilFreePull(DateTime utcNow)
        {
            if (HasFreePullAt(utcNow)) return TimeSpan.Zero;

            var left = QuestSystem.NextResetUtc(utcNow) - utcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /**
         * @brief 무료 뽑기 한 번. **f2p가 이 배너에 닿는 경로다.**
         *
         * 무과금의 보석은 44단계 실측대로 코어 진행에 다 배정돼 있고, 이제
         * 그 보석을 노리는 배너가 둘이다 - 나눠 쓰면 한 번 더 쪼개진다.
         * 그래서 이 버튼이 있고, 천장이 ★4(해금)를 보장하므로 하루 한 번이면
         * **한 달에 오의 하나**다(SkillGachaCurve.FreePullsPerDay 주석).
         */
        public bool TryFreePull() { return TryFreePullAt(DateTime.UtcNow); }

        public bool TryFreePullAt(DateTime utcNow)
        {
            if (!HasFreePullAt(utcNow)) return false;

            lastFreePullDayTicks = SkillGachaCurve.DayOf(utcNow).Ticks;
            RunPulls(SkillGachaCurve.FreePullsPerDay);
            return true;
        }

        // ---------------------------------------------------------------- 뽑기

        public bool TryPull(int count)
        {
            if (!CanPull(count)) return false;
            if (!gems.TrySpend(CostFor(count))) return false;

            RunPulls(count);
            return true;
        }

        private void RunPulls(int count)
        {
            results.Clear();

            for (int i = 0; i < count; i++) results.Add(RollOnce());

            totalPulls += count;

            Raise();

            var handler = Pulled;
            if (handler != null) handler(results);
        }

        /**
         * @brief 한 번 굴린다. **천장은 표 위에 얹힌다** - 47단계와 같은 자리.
         *
         * 표(SkillGachaCurve.Roll)는 순수하게 확률만 보고, 천장은 카운터를
         * 아는 여기서 결과를 덮어쓴다. 나누는 이유도 같다 - 시뮬레이션은
         * 기댓값으로 세고 실제는 굴리는데, 둘이 **같은 표**를 지나야 두 값이
         * 같은 것을 뜻한다.
         */
        private PullResult RollOnce()
        {
            var outcome = SkillGachaCurve.Roll(UnityEngine.Random.value);

            bool pity = false;
            if (SkillGachaCurve.GradeFor(outcome) < GachaCurve.Grade.Epic
                && pityCounter + 1 >= SkillGachaCurve.PityPulls)
            {
                outcome = SkillGachaCurve.Outcome.SkillUnlock;
                pity = true;
            }

            if (SkillGachaCurve.GradeFor(outcome) >= GachaCurve.Grade.Epic) pityCounter = 0;
            else pityCounter++;

            return Grant(outcome, pity, outcome);
        }

        /**
         * @brief 결과 하나를 적용한다. **막히면 스스로를 한 칸 아래로 다시 부른다.**
         *
         * 재귀로 적은 이유는 미끄러짐이 사다리의 정의 그대로이기 때문이다 -
         * `SlideFor`가 한 칸을 말하고 여기가 그 칸을 다시 시도한다. 47단계는
         * 같은 일을 GrantRarity 안에 손으로 두 번 적었는데(★4 -> ★3 -> 파편),
         * 이쪽은 미끄러짐이 두 칸이라 그 방식이면 세 갈래가 된다.
         *
         * 바닥이 XP라 재귀가 반드시 끝난다 - XP는 갈 곳이 없어도 풀에 남으므로
         * 거절되지 않는다.
         */
        private PullResult Grant(SkillGachaCurve.Outcome outcome, bool pity,
                                 SkillGachaCurve.Outcome rolled)
        {
            switch (outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:
                {
                    int index = skills != null ? skills.AwakenEquipped() : -1;
                    if (index >= 0) return Result(outcome, 0, 0, -1, index, pity, rolled);
                    break;
                }

                case SkillGachaCurve.Outcome.SkillUnlock:
                {
                    int index = skills != null
                        ? SkillGachaCurve.UnlockTargetFor(skills.GachaOwnedMask) : -1;
                    if (index >= 0 && skills.GrantGachaSkill(index))
                        return Result(outcome, 0, 0, index, -1, pity, rolled);
                    break;
                }

                default:
                {
                    int xp = SkillGachaCurve.XpFor(outcome);
                    int gained = skills != null ? skills.GrantXp(xp) : 0;
                    return Result(outcome, xp, gained, -1, -1, pity, rolled);
                }
            }

            return Grant(SkillGachaCurve.SlideFor(outcome), pity, rolled);
        }

        private static PullResult Result(SkillGachaCurve.Outcome outcome, int xp, int levels,
                                         int unlocked, int awakened, bool pity,
                                         SkillGachaCurve.Outcome rolled)
        {
            return new PullResult
            {
                Outcome = outcome,
                Rolled = rolled,
                Grade = SkillGachaCurve.GradeFor(outcome),
                Xp = xp,
                LevelsGained = levels,
                UnlockedIndex = unlocked,
                AwakenedIndex = awakened,
                FromPity = pity,

                // 등급은 **도착한 칸**의 것이다. 화면의 색이 실제로 받은
                // 것을 말해야 하고, 출발점은 Rolled가 따로 적는다
                Downgraded = rolled != outcome
            };
        }

        // ---------------------------------------------------------------- 세이브

        public int CollectPity() { return pityCounter; }
        public int CollectTotalPulls() { return totalPulls; }
        public long CollectFreePullDay() { return lastFreePullDayTicks; }

        /**
         * @brief 세이브 복원.
         *
         * **무료 뽑기 날짜가 0이면 곧바로 쓸 수 있다.** 마이그레이션이 그
         * 값을 0으로 넣으므로(SaveData v17 -> v18) 기존 세이브는 접속하는
         * 순간 무료 뽑기 하나를 들고 있다 - v14 -> v15가 요도 뽑기에서 한
         * 것과 같은 처리이고, 새 시스템이 열리는 것이지 소급이 아니다.
         */
        public void Restore(int savedPity, int savedTotal, long savedFreeDay)
        {
            pityCounter = Mathf.Clamp(savedPity, 0, SkillGachaCurve.PityPulls - 1);
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

        public void DebugResetFreePull()
        {
            lastFreePullDayTicks = 0L;
            Raise();
        }

        /** 천장 직전까지 카운터를 민다. 천장 연출을 30번 안 돌리고 보는 경로 */
        public void DebugPushToPity()
        {
            pityCounter = SkillGachaCurve.PityPulls - 1;
            Raise();
        }

        public void DebugReset()
        {
            pityCounter = 0;
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
