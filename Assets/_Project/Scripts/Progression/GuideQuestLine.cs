using Onikiri.Core;

namespace Onikiri.Progression
{
    /**
     * @brief 가이드 카드가 지금 말해야 하는 것.
     *
     * 네 가지뿐이고, 넷이 서로 배타적이다. 화면은 이 값 하나만 보고 그린다 -
     * 조건을 화면 쪽에서 다시 조합하면 "진행 중인데 금빛"같은 상태가 만들어진다.
     */
    public enum GuideQuestState
    {
        /** 보여줄 칸이 없다. 진행선을 다 받았다는 뜻이다 */
        None,

        /** 진행 중. 아직 목표치에 못 닿았다 */
        InProgress,

        /** 완료 가능. 조건은 채웠고 보상이 퀘스트 화면에 열려 있다 */
        Claimable,

        /** 보상 수령 완료. 다음 칸으로 넘어가기 직전의 짧은 상태다 */
        Claimed
    }

    /** 카드 한 장을 그리는 데 필요한 값 전부. 문자열을 새로 만들지 않는다 */
    public struct GuideQuestView
    {
        public GuideQuestState State;

        /** 진행선에서 몇 번째 칸인가 (0부터). 보여줄 것이 없으면 -1 */
        public int Step;

        public int StepCount;

        /** 지금 해야 할 행동 */
        public string Action;

        /** 완료 조건. 가리키는 퀘스트의 제목이 그대로 조건이다 */
        public string Title;

        public double Progress;
        public double Target;

        /** 받을 보상 */
        public int Gems;
        public bool HasGold;

        /** 진행 바가 채울 비율 (0~1) */
        public float Ratio;
    }

    /**
     * @brief 진행선에서 **지금 보여줄 칸 하나**를 고른다.
     *
     * ## 왜 MonoBehaviour가 아닌가
     *
     * 고르는 규칙에 씬이 필요 없다. QuestSystem 하나만 넘기면 답이 나오므로
     * 정적 함수로 두면 테스트가 씬 없이 경계를 검사할 수 있다 -
     * QuestSystem.QuestDayOf가 정적인 이유(QuestTests)와 같다.
     *
     * ## 고르는 규칙
     *
     *   1. 아직 안 받은 칸 중 **받을 수 있는 것**이 있으면 그것
     *   2. 없으면 아직 안 받은 **첫 칸**
     *   3. 그것도 없으면 없음(None)
     *
     * 1이 2보다 먼저인 이유: "가서 받아라"는 진행 중인 어떤 목표보다 급하다
     * (퀘스트 아이콘 배지가 켜지는 것과 같은 우선순위 규칙이다). 이것이
     * 없으면 3번 칸이 다 찼는데도 카드가 2번 칸을 계속 가리키고, 플레이어는
     * 받을 것이 있다는 사실을 모른다.
     *
     * 2에서 **순서를 지키는 것**이 이 표의 존재 이유다. 진행률이 가장 높은
     * 칸을 고르면 목록의 요약이 되고, 그것은 이미 진행 줄이 하는 일이다.
     * 가이드는 요약이 아니라 **차례**다.
     */
    public static class GuideQuestLine
    {
        public static GuideQuestView Resolve(QuestSystem quests)
        {
            return Resolve(quests, int.MaxValue, false);
        }

        /**
         * @brief 최전선과 온보딩 수령 여부까지 보고 고른다 (15종 재설계).
         *
         * 인자가 둘 는 이유는 st14 온보딩 칸 때문이다. 그 칸은 퀘스트를 안
         * 가리키므로 완료를 QuestSystem에 물을 수가 없고(GuideGate 주석),
         * 상점이 열리기 전에는 아예 안 보여야 한다.
         *
         * @param frontierStage 최전선. 게이트 칸의 MinStage를 재는 값
         * @param introClaimed  온보딩 무료 10연을 받았는가
         */
        public static GuideQuestView Resolve(QuestSystem quests, int frontierStage,
                                             bool introClaimed)
        {
            var view = Empty();
            if (quests == null) return view;

            int firstOpen = -1;
            int firstReady = -1;

            var steps = GuideQuestCatalog.Steps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Gate == GuideGate.SkillGachaIntro)
                {
                    // 상점이 열리기 전에는 건너뛴다 - 못 깨는 칸을 가리키는
                    // 카드는 안내가 아니라 벽이다
                    if (frontierStage < steps[i].MinStage) continue;
                    if (introClaimed) continue;

                    // 이 칸은 **절대 Claimable 이 안 된다.** 받을 것이 퀘스트
                    // 화면에 없고 뽑기 버튼이 곧 수령이라, "받기"를 띄우면
                    // 플레이어를 없는 버튼으로 보낸다
                    if (firstOpen < 0) firstOpen = i;
                    continue;
                }

                // 표에서 빠진 퀘스트를 가리키는 칸. 조용히 건너뛴다
                if (steps[i].Index < 0) continue;

                if (quests.IsClaimed(steps[i].Kind, steps[i].Index)) continue;

                if (firstOpen < 0) firstOpen = i;

                if (quests.ClaimableCount(steps[i].Kind, steps[i].Index) > 0)
                {
                    firstReady = i;
                    break;   // 규칙 1이 이겼다. 더 볼 것이 없다
                }
            }

            int chosen = firstReady >= 0 ? firstReady : firstOpen;
            if (chosen < 0) return view;

            view = Describe(quests, chosen);
            view.State = firstReady >= 0 ? GuideQuestState.Claimable : GuideQuestState.InProgress;
            return view;
        }

        /**
         * @brief 특정 칸을 그대로 읽는다. 상태는 채우지 않는다.
         *
         * 수령 직후의 "보상 수령 완료" 표시가 이것을 쓴다 - 그 칸은 이미
         * 진행선에서 빠졌으므로 Resolve로는 다시 찾을 수 없다.
         */
        public static GuideQuestView Describe(QuestSystem quests, int step)
        {
            var view = Empty();

            var steps = GuideQuestCatalog.Steps;
            if (quests == null || step < 0 || step >= steps.Length) return view;

            if (steps[step].Gate == GuideGate.SkillGachaIntro)
            {
                // 목표치가 1이고 진행도가 0이다. 게이지가 비어 있는 것이
                // 맞다 - 열 번을 나눠 받는 것이 아니라 한 번에 받는 사건이다
                view.Step = step;
                view.Action = steps[step].Action;
                view.Title = "무료 10회 뽑기";
                view.Target = 1d;
                view.Progress = 0d;
                view.Gems = 0;
                view.HasGold = false;
                view.Ratio = 0f;
                view.State = GuideQuestState.InProgress;
                return view;
            }

            if (steps[step].Index < 0) return view;

            var spec = QuestCatalog.Of(steps[step].Kind)[steps[step].Index];

            view.Step = step;
            view.Action = steps[step].Action;
            view.Title = spec.Title;
            view.Target = spec.Target;
            view.Progress = quests.ProgressOf(steps[step].Kind, steps[step].Index);
            view.Gems = spec.Gems;
            view.HasGold = spec.GoldMobs > 0d;
            view.Ratio = spec.Target > 0d
                ? UnityEngine.Mathf.Clamp01((float)(view.Progress / spec.Target))
                : 0f;
            view.State = GuideQuestState.InProgress;
            return view;
        }

        /**
         * @brief 보상 한 줄. **보석 수 하나다.**
         *
         * 두 가지를 안 적는다.
         *
         * 골드는 **받는 순간의 스테이지**로 환산되므로
         * (QuestCatalog.AchievementGold) 미리 적을 수 있는 숫자가 아니고,
         * "· 골드"처럼 이름만 붙이면 그 네 글자가 카드 폭의 20%를 먹는다 -
         * 퀘스트 줄이 재화 이름을 통째로 아이콘에 넘긴 이유 그대로다
         * (QuestRow.RewardText, #11). 정확한 값은 받는 자리(퀘스트 화면)에 있다.
         *
         * 카드 폭(보상 칸 140px)에 드는 것이 조건이다. 여기에 무엇을 더하면
         * 왼쪽의 행동 문구가 그만큼 잘린다.
         */
        public static string RewardText(GuideQuestView view)
        {
            return "보석 " + view.Gems;
        }

        /**
         * @brief 진행 숫자. 골드처럼 큰 값만 축약한다.
         *
         * QuestRow.FormatCount와 같은 규칙이다 - 두 화면이 같은 퀘스트를
         * 서로 다른 표기로 적으면 그 둘이 같은 줄인지 알 수 없다.
         */
        public static string FormatCount(double value)
        {
            if (value >= 10000d) return NumberFormatter.Format(BigDouble.FromDouble(value));
            return UnityEngine.Mathf.FloorToInt((float)value).ToString();
        }

        private static GuideQuestView Empty()
        {
            var view = default(GuideQuestView);
            view.State = GuideQuestState.None;
            view.Step = -1;
            view.StepCount = GuideQuestCatalog.Count;
            return view;
        }
    }
}
