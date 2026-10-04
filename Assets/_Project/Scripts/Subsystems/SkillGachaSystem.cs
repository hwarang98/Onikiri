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
     * 확률표도 소환 레벨의 식도 가격도 같은 곳에서 오므로(SkillGachaCurve) 한
     * 시스템에 배너 두 개를 두고 싶어진다. 그런데 **소환 경험치가 갈려야 한다** -
     * 요도 뽑기 백 번이 오의 해금 확률을 끌어올리면 두 배너가 한 지갑이 아니라
     * 한 상품이 되고, 그러면 배너를 둘로 나눈 이유가 사라진다. (50단계에는 같은
     * 이유가 천장 카운터에 걸려 있었다.)
     *
     * 경험치가 갈리면 무료 뽑기 쿨도 갈려야 하고(하루에 요도 하나 · 오의
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
             * @brief 표가 **낸** 결과. 미끄러지기 전의 칸이다.
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

            /**
             * @brief 사다리를 미끄러졌는가.
             *
             * 47단계의 Downgraded와 같은 자리, 같은 이유다 - 결과를 도착한
             * 곳의 이름으로만 적으면 플레이어는 등급이 내려간 것을 모르고,
             * 출발한 곳의 이름으로만 적으면 화면과 실제가 갈린다. 두 이름을
             * 다 적는 것이 44단계의 "버려지는 드랍 0"을 문구로 지키는 방법이다.
             *
             * 판정은 사다리가 한다(SlidesTo). 50단계에는 `Rolled != Outcome`이
             * 미끄러짐(위 -> 아래)과 천장(아래 -> 위) 둘 다를 뜻해서 그렇게
             * 재면 안 됐다. 68단계에 천장이 사라져 지금은 같은 답이지만, 판정을
             * 사다리에 두는 규칙은 그대로다.
             */
            public bool Downgraded;
        }

        [SerializeField] private GemWallet gems;
        [SerializeField] private SkillSystem skills;
        [SerializeField] private StageProgress stage;

        /**
         * @brief 이 배너의 소환 경험치 = 지금까지 돌린 횟수 (68단계).
         *
         * 요도 배너(GachaSystem.summonXp)와 **갈라 둔다** - 두 배너는 파는
         * 것이 다르고 지갑을 나눠 쓰므로, 한쪽이 다른 쪽을 키우면 "어느
         * 배너를 돌려야 하는가"가 사라진다. 50단계의 두 천장 카운터
         * (소프트·하드)가 서던 자리이고, 둘이 하던 일을 레벨 하나가 한다.
         */
        [SerializeField] private long summonXp;

        /** 온보딩 무료 10연을 받았는가. 계정당 한 번이다 */
        [SerializeField] private bool introClaimed;

        /**
         * @brief 온보딩으로 받은 오의를 **처음 장착했는가.**
         *
         * 파생 조건(보유 && 미장착)으로는 1회성이 안 된다 - 플레이어가 나중에
         * 그 오의를 빼면 온보딩 안내가 되살아난다. 장착한 순간 이 값이 서고,
         * 그 뒤로는 무엇을 끼우든 다시 안 뜬다.
         */
        [SerializeField] private bool introEquipDone;

        [SerializeField] private int totalPulls;

        /** 마지막으로 무료 뽑기를 쓴 **퀘스트일** (UTC ticks). 시각이 아니라 날짜다 */
        [SerializeField] private long lastFreePullDayTicks;

        public event Action Changed;
        public event Action<List<PullResult>> Pulled;

        /** 소환 레벨이 올랐다. 오른 레벨마다 한 번, Pulled보다 먼저 - GachaSystem과 같은 계약 */
        public event Action<int> SummonLevelUp;

        public static SkillGachaSystem Instance { get; private set; }

        public int TotalPulls { get { return totalPulls; } }

        /** 소환 경험치 (누적 뽑기 수) */
        public long SummonXp { get { return summonXp; } }

        /** 소환 레벨. 경험치에서 유도된다 - 저장하지 않는다 */
        public int SummonLevel { get { return SummonLevelCurve.LevelFor(summonXp); } }

        /** 마지막 뽑기 묶음에서 닿은 레벨. 0이면 안 올랐다 - 결과 팝업이 읽는다 */
        public int LastBatchLevelUp { get; private set; }

        public bool IntroClaimed { get { return introClaimed; } }
        public bool IntroEquipDone { get { return introEquipDone; } }

        /**
         * @brief 온보딩 무료 10연 버튼이 떠 있는가.
         *
         * 재고를 안 보는 것이 일일 무료와 다른 점이다. 온보딩은 **재고가
         * 있을 수밖에 없는 시점**(st14, 아홉 다 미보유)에 뜨고, 만에 하나
         * 재고가 없더라도 XP로 미끄러지므로 빈 우편함이 안 된다.
         */
        public bool CanClaimIntro { get { return IsUnlocked && !introClaimed; } }

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
            if (skills != null) skills.Changed += OnSkillsChanged;

            OnSkillsChanged();
        }

        private void OnDisable()
        {
            if (skills != null) skills.Changed -= OnSkillsChanged;
        }

        /**
         * @brief 오의가 바뀌었다. 재고를 다시 알리기 전에 **온보딩이 끝났는지 본다.**
         *
         * 장착 버튼이 직접 부르게 두지 않는 이유는 장착 경로가 하나가
         * 아니기 때문이다 - 빈 칸이 있으면 `GrantGachaSkill`이 부르는
         * `FillEmptySlots`가 사람 손 없이 끼운다. 그 경로를 놓치면 온보딩
         * 안내가 이미 끝난 일을 계속 가리킨다.
         *
         * 여기서 듣는 것이 맞는 또 하나의 이유는 **이미 듣고 있기 때문이다** -
         * 새 구독을 만들지 않으므로 비용이 정수 비교 몇 개뿐이고, 재고
         * 갱신과 같은 순간에 판정되므로 둘이 갈릴 수가 없다.
         */
        private void OnSkillsChanged()
        {
            if (introClaimed && !introEquipDone && skills != null)
            {
                int intro = SkillCatalog.IndexOf(SkillCatalog.BloodWhipId);
                if (intro >= 0 && skills.IsEquipped(intro))
                {
                    // Raise를 자기가 부르므로 아래에서 또 부르지 않는다
                    MarkIntroEquipDone();
                    return;
                }
            }

            Raise();
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
        /** 보석 구매가 열렸는가. 배너 자체(IsUnlocked)보다 스물일곱 칸 늦다 */
        public bool CanBuy { get { return SkillGachaCurve.CanBuyAt(StageNow); } }

        public bool CanPull(int count)
        {
            if (!IsUnlocked || !CanBuy || !HasStock) return false;
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
         * 그래서 이 버튼이 있고, 하루 한 번이 소환 경험치로 쌓여 ★4(해금)
         * 확률을 끌어올린다(SkillGachaCurve.FreePullsPerDay 주석).
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

        // ---------------------------------------------------------------- 온보딩 10연

        /**
         * @brief st14의 무료 10연. **결과를 덮어쓰지 않는다.**
         *
         * ## 왜 강제 승격이 아닌가
         *
         * 처음 설계는 "10번째에 ★4 미출현이면 강제 승격"이었다 - 기존 천장
         * 코드와 같은 모양이라 싸 보였다. 그런데 그 방식은 **10번째가 ★5였을
         * 때 그 ★5를 빼앗는다.** 첫 뽑기가 플레이어에게 주는 가장 큰 사건을
         * 온보딩 보정이 가져가는 것이고, 그것은 선물이 아니라 몰수다.
         *
         * 그래서 열 번은 그냥 돈다. 소환 경험치에도 정상 반영된다 - 온보딩이
         * 끝난 뒤 소환 게이지가 10칸 차 있는 것이 맞다(68단계 확정 3).
         *
         * ## 보상은 뽑기 결과가 아니다
         *
         * 열 번 안에 표준 해금이 없었을 때만 **별도로** 지급한다. 뽑기 결과가
         * 아니므로 소환 경험치를 주지 않고 확률 정보 표에도 안 들어간다 -
         * 표에 넣으면 공개 확률이 거짓이 된다.
         *
         * 주는 것은 `StandardTargetFor`의 답이다. 신규 플레이어에게 그것은
         * 언제나 혈조이고(StandardUnlockOrder의 머리), 자연 뽑기가 여는 것도
         * 같은 함수를 지나므로 **배너 문구와 실제 결과가 갈릴 수가 없다.**
         *
         * @return 실제로 받았으면 true. 이미 받았거나 잠겨 있으면 false
         */
        public bool ClaimIntro()
        {
            if (!CanClaimIntro) return false;

            introClaimed = true;

            int before = skills != null ? skills.GachaOwnedMask : 0;
            RunPulls(SkillGachaCurve.IntroPullCount);
            int after = skills != null ? skills.GachaOwnedMask : 0;

            if (!OpenedAnyStandard(before, after)) GrantIntroConsolation();

            Raise();
            return true;
        }

        /** 열 번 사이에 표준 풀의 오의가 하나라도 열렸는가 */
        private static bool OpenedAnyStandard(int before, int after)
        {
            int opened = after & ~before;
            if (opened == 0) return false;

            foreach (var id in SkillGachaCurve.StandardUnlockOrder)
            {
                int index = SkillCatalog.IndexOf(id);
                if (index >= 0 && (opened & (1 << index)) != 0) return true;
            }
            return false;
        }

        /**
         * @brief 열 번이 표준을 못 열었을 때의 별도 보상.
         *
         * 표준이 이미 다 팔린 상태(v19 고인물이 업데이트로 받는 경우)에는
         * XP 240을 준다 - ★3과 같은 값이고, 사다리의 바닥이 아무것도 아닌
         * 곳이 아니라는 44단계 규칙 그대로다.
         */
        private void GrantIntroConsolation()
        {
            if (skills == null) return;

            int target = SkillGachaCurve.StandardTargetFor(skills.GachaOwnedMask);
            if (target >= 0 && skills.GrantGachaSkill(target)) return;

            skills.GrantXp(SkillGachaCurve.XpFor(SkillGachaCurve.Outcome.XpSurge));
        }

        /**
         * @brief 온보딩으로 받은 오의를 처음 장착했다. **되돌아오지 않는 표시다.**
         *
         * 화면(SkillButton)이 장착에 성공한 뒤 부른다. 여기서 세워 두면
         * 나중에 그 오의를 빼도 온보딩 안내가 다시 안 뜬다 - 파생 조건으로는
         * 못 하는 일이고, 그래서 세이브 필드가 하나 더 있다.
         */
        public void MarkIntroEquipDone()
        {
            if (introEquipDone) return;

            introEquipDone = true;
            Raise();
        }

        /** count번 굴린다. 회차마다 그 순간의 레벨로 굴리고 1 XP를 쌓는다 - GachaSystem과 같은 규칙 */
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
         * 50단계의 두 천장(★4 소프트 30 · ★5 하드 100)은 68단계에 사라졌다.
         * 막히면 미끄러지는 규칙(Grant)은 천장이 아니라 보유 상태의 문제라 그대로다.
         */
        private PullResult RollOnce()
        {
            var rolled = SkillGachaCurve.Roll(UnityEngine.Random.value, SummonLevel);
            return Grant(rolled, rolled);
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
        private PullResult Grant(SkillGachaCurve.Outcome outcome, SkillGachaCurve.Outcome rolled)
        {
            switch (outcome)
            {
                case SkillGachaCurve.Outcome.Awakening:
                {
                    // **해금이 개안보다 먼저다.** 귀오의 넷이 이 자리의 상품이고,
                    // 개안은 그것이 다 팔린 뒤의 두 번째 값이다. 순서를 뒤집으면
                    // 전설을 받고도 새 오의 대신 XP 가속만 오는 회차가 생긴다
                    int unlock = skills != null
                        ? SkillGachaCurve.OniSecretTargetFor(skills.GachaOwnedMask) : -1;
                    if (unlock >= 0 && skills.GrantGachaSkill(unlock))
                        return Result(outcome, 0, 0, unlock, -1, rolled);

                    int awakened = skills != null ? skills.AwakenEquipped() : -1;
                    if (awakened >= 0) return Result(outcome, 0, 0, -1, awakened, rolled);
                    break;
                }

                case SkillGachaCurve.Outcome.SkillUnlock:
                {
                    int index = skills != null
                        ? SkillGachaCurve.StandardTargetFor(skills.GachaOwnedMask) : -1;
                    if (index >= 0 && skills.GrantGachaSkill(index))
                        return Result(outcome, 0, 0, index, -1, rolled);
                    break;
                }

                default:
                {
                    int xp = SkillGachaCurve.XpFor(outcome);
                    int gained = skills != null ? skills.GrantXp(xp) : 0;
                    return Result(outcome, xp, gained, -1, -1, rolled);
                }
            }

            return Grant(SkillGachaCurve.SlideFor(outcome), rolled);
        }

        private static PullResult Result(SkillGachaCurve.Outcome outcome, int xp, int levels,
                                         int unlocked, int awakened,
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

                // 등급은 **도착한 칸**의 것이다. 화면의 색이 실제로 받은
                // 것을 말해야 하고, 출발점은 Rolled가 따로 적는다
                //
                // 미끄러짐은 **아래로 걸어 닿았을 때만**이다(SlidesTo)
                Downgraded = rolled != outcome && SkillGachaCurve.SlidesTo(rolled, outcome)
            };
        }

        // ---------------------------------------------------------------- 세이브

        public long CollectSummonXp() { return summonXp; }
        public int CollectTotalPulls() { return totalPulls; }
        public long CollectFreePullDay() { return lastFreePullDayTicks; }
        public bool CollectIntroClaimed() { return introClaimed; }
        public bool CollectIntroEquipDone() { return introEquipDone; }

        /**
         * @brief 세이브 복원 (v23).
         *
         * **무료 뽑기 날짜가 0이면 곧바로 쓸 수 있다.** 마이그레이션이 그
         * 값을 0으로 넣으므로(SaveData v17 -> v18) 기존 세이브는 접속하는
         * 순간 무료 뽑기 하나를 들고 있다 - 새 시스템이 열리는 것이지 소급이 아니다.
         *
         * 소환 경험치는 v22 -> v23이 누적 뽑기 수로 채워 온다 - 이미 뽑은
         * 만큼 레벨을 준다(68단계 확정). 천장 카운터 둘은 버렸다.
         */
        public void Restore(long savedSummonXp, int savedTotal, long savedFreeDay,
                            bool savedIntroClaimed, bool savedIntroEquipDone)
        {
            // 음수만 막는다 - 위쪽은 상한이 없다(SummonLevelCurve 머리 주석)
            summonXp = savedSummonXp < 0L ? 0L : savedSummonXp;
            totalPulls = Mathf.Max(0, savedTotal);
            lastFreePullDayTicks = savedFreeDay < 0L ? 0L : savedFreeDay;
            introClaimed = savedIntroClaimed;
            introEquipDone = savedIntroEquipDone;

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

        /** 다음 레벨 직전까지 경험치를 민다. 레벨업을 수십 번 안 돌리고 보는 경로 */
        public void DebugPushToLevelUp()
        {
            summonXp += SummonLevelCurve.XpToNext(SummonLevel)
                      - SummonLevelCurve.XpIntoLevel(summonXp) - 1L;
            Raise();
        }

        /** 온보딩 10연을 다시 받을 수 있게 되돌린다 */
        public void DebugResetIntro()
        {
            introClaimed = false;
            introEquipDone = false;
            Raise();
        }

        public void DebugReset()
        {
            summonXp = 0L;
            totalPulls = 0;
            lastFreePullDayTicks = 0L;
            introClaimed = false;
            introEquipDone = false;
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
