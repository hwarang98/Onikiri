namespace Onikiri.Progression
{
    /**
     * @brief 가이드 진행선의 한 칸.
     *
     * **자기 목표를 스스로 들고 있지 않는다.** 기존 퀘스트 하나(QuestId)를
     * 가리키고, 목표치·진행도·보상·수령 여부는 전부 그 퀘스트에서 읽는다.
     * 여기 있는 것은 그 퀘스트를 **몇 번째로 보여줄 것인가**와 그때 화면에
     * 적을 **행동 한 줄**뿐이다.
     */
    /**
     * @brief 이 칸의 완료를 **무엇이 판정하는가.**
     *
     * 15종 재설계에 둘째가 생겼다. st14 온보딩("무료 10연을 받아라")은 퀘스트로
     * 만들 수가 없다 - `QuestMetric`에 뽑기 횟수를 세는 항목이 없고, 만들면
     * 카운터·세이브 칸·보상이 함께 늘어난다.
     *
     * 그런데 그 사실은 **이미 세이브에 있다**(skillGachaIntroClaimed, v20).
     * 없는 것을 새로 세는 대신 있는 것을 읽으면 퀘스트 표도 메트릭도 세이브도
     * 한 칸 안 는다.
     */
    public enum GuideGate
    {
        /** 기존 방식. 가리키는 퀘스트의 진행도·수령 상태를 읽는다 */
        Quest,

        /** 온보딩 무료 10연을 받았는가 (SkillGachaSystem.IntroClaimed) */
        SkillGachaIntro
    }

    public struct GuideStep
    {
        /** 가리키는 퀘스트. QuestCatalog에 있는 id다. Gate == Quest 일 때만 쓴다 */
        public string QuestId;

        public QuestKind Kind;

        /**
         * @brief QuestCatalog.Of(Kind) 안에서의 인덱스. **정적 생성자가 채운다.**
         *
         * 표에 손으로 적지 않는 이유는 세이브가 id를 쓰는 이유와 같다
         * (SaveData 주석) - 퀘스트 목록 중간에 하나가 추가되면 손으로 적은
         * 인덱스는 전부 한 칸씩 엉뚱한 곳을 가리키고, 그것은 에러가 아니라
         * **다른 퀘스트의 진행도**로만 나타난다.
         *
         * 못 찾으면 -1이다. 그 칸은 화면에서 조용히 건너뛴다.
         */
        public int Index;

        /**
         * @brief 이 칸의 완료를 판정하는 방식. **기본은 기존 퀘스트다.**
         *
         * 기본값이 Quest(0)라 아래 표의 기존 열넷은 한 글자도 안 바뀐다.
         */
        public GuideGate Gate;

        /**
         * @brief 이 최전선에 닿기 전에는 **칸 자체를 건너뛴다.** 0이면 조건 없음.
         *
         * 온보딩 칸이 이 값을 쓴다. 상점이 st14에 열리므로 그 전에 "상점에서
         * 받아라"를 띄우면 **못 깨는 칸**이 되고, 가이드가 못 깨는 칸을
         * 가리키는 순간 그 카드는 안내가 아니라 벽이 된다.
         */
        public int MinStage;

        /**
         * @brief "지금 해야 할 행동" 한 줄.
         *
         * 퀘스트 제목(= 완료 조건)과 **다른 문장이어야 한다.** "5스테이지
         * 도달"은 무엇이 끝인지를 말하지만 무엇을 누르라는 말은 아니다.
         * 가이드가 답해야 하는 것은 후자다.
         *
         * 카드 폭(540px)에서 한글 10자쯤이 한 줄이다. 넘치면 잘린다(Ellipsis).
         */
        public string Action;
    }

    /**
     * @brief 플레이 화면 가이드 퀘스트의 진행선.
     *
     * ## 왜 새 퀘스트를 만들지 않았는가
     *
     * 기획(플레이 경험 개선안 v2, 3절)이 요구한 것은 "초반 기능 학습용 **단일
     * 진행선**"이다. 그것을 새 퀘스트 표로 만들면 목표치·보상·수령 상태가 한
     * 벌 더 생기고, 그 한 벌이 세이브에 들어가고, 보상이 밴드를 건드린다 -
     * 업적 골드가 시뮬레이션에 들어가 있는 이유 그대로다(QuestCatalog.Achievement
     * 주석). 화면 하나 붙이자고 밸런스 표를 다시 돌릴 일이 아니다.
     *
     * 그래서 이 표는 **순서만 정한다.** 각 칸은 이미 있는 업적을 가리키고,
     * 보상도 수령도 그 업적의 것이다. 가이드는 "퀘스트 화면 어딘가에 있는
     * 그 줄"을 전투 화면으로 한 칸씩 꺼내 보여주는 창이지, 별도의 재화
     * 수도꼭지가 아니다. 그러므로:
     *
     *   - 세이브 필드가 늘지 않는다 (수령 상태는 QuestSystem이 이미 들고 있다)
     *   - 지급 경로가 늘지 않는다 (받는 곳은 여전히 퀘스트 화면의 받기 버튼)
     *   - 보스 여유 밴드가 한 칸도 안 움직인다
     *
     * ## 순서를 고른 기준
     *
     * 기획 4절의 "초반 30스테이지에는 3~4스테이지마다 새 기능 하나만 연다"를
     * 지금 있는 업적으로 옮긴 것이다. 스테이지 - 레벨 - 강화 - 오의가
     * 번갈아 오도록 섞었다: 같은 종류가 연달아 서면 가이드가 "스테이지만
     * 올리는 게임"이라고 말하게 된다.
     *
     * 일일·반복은 넣지 않는다. 그것은 **매일 다시 서는 목표**라 학습 순서가
     * 아니다 - 받을 것이 생기면 퀘스트 아이콘의 빨간 배지가 알리고, 목록은
     * 퀘스트 화면에 있다. 가이드가 그것까지 말하면 둘 다 안 읽힌다.
     */
    public static class GuideQuestCatalog
    {
        /**
         * @brief 행동 문구는 **이미 구워진 글리프로만** 적는다.
         *
         * 이 게임의 폰트 아틀라스는 완성형 11,172자가 아니라 실제로 쓰는
         * 441자만 굽는다(FontCharsetBuilder). 그래서 여기에 새 음절을 쓰면
         * 아틀라스를 다시 굽기 전까지 화면에 □가 뜬다 - "넘·뚫·쌓·끝"이
         * 그랬다. 문구를 고를 때 뜻만이 아니라 **글자가 이미 있는지**도
         * 함께 봐야 하는 것이 이 프로젝트의 제약이다.
         *
         * 문자셋 쪽에도 이 표를 물려 뒀다(FontCharsetBuilder.DisplayNames) -
         * 손으로 옮겨 적는 단계는 언젠가 빠지므로, 다음에 아틀라스를 구울 때
         * 여기 적힌 글자가 자동으로 따라간다. 빌드 검사(VerifyDataAssetNames)가
         * 그 둘이 어긋나면 잡는다.
         */
        private static readonly GuideStep[] steps =
        {
            Step("ach_stage5",     "요괴를 베고 나아가라"),
            Step("ach_level10",    "레벨을 올려라"),
            Step("ach_upgrade50",  "골드로 강화를 사라"),
            Step("ach_stage10",    "지역 1을 돌파하라"),

            // st14 온보딩. **퀘스트를 안 가리키는 유일한 칸이다.**
            //
            // 자리를 여기로 고른 이유는 상점이 열리는 칸이 st14이고 앞뒤가
            // st10(지역 1)과 오의 강화이기 때문이다 - 스테이지·기능·강화가
            // 번갈아 오는 기존 배치가 그대로 유지된다.
            //
            // 보상이 없다. 무료 10연 자체가 보상이라 그 위에 보석을 더 얹으면
            // 같은 사건에 값이 두 번 매겨진다
            Gate("상점에서 무료 스킬 10회를 받아라",
                 GuideGate.SkillGachaIntro, ShopCurve.UnlockStage),

            // 오의는 Lv.10에 열린다(SkillCatalog.PanelUnlockLevel). 레벨 칸
            // 뒤에 두어야 "열린 것을 곧바로 써 본다"가 된다 - 열리기 전에
            // 가이드가 오의를 가리키면 그 칸은 못 깨는 칸이다
            Step("ach_skill12",    "오의를 배우고 올려라"),

            Step("ach_stage20",    "지역 2를 돌파하라"),
            Step("ach_level25",    "레벨을 더 올려라"),
            Step("ach_upgrade150", "강화를 더 올려라"),
            Step("ach_stage30",    "30스테이지에 닿아라"),

            // st31~50 가속 구간. 여기부터는 새 기능이 아니라 깊이라
            // 가이드도 스테이지·레벨만 번갈아 가리킨다
            Step("ach_stage35",    "가속 구간을 돌파하라"),
            Step("ach_stage40",    "지역 4를 돌파하라"),
            Step("ach_level50",    "레벨 50에 닿아라"),
            Step("ach_stage45",    "45스테이지에 닿아라"),
            Step("ach_stage50",    "50스테이지에 닿아라")
        };

        /** 카드가 상태에 따라 적는 문구. 문자셋이 이것도 걷어가야 한다 */
        public static readonly string[] StateWords =
        {
            "가이드",
            "가이드 완료",
            "보상 수령 완료",
            "받기",
            ">"
        };

        public static GuideStep[] Steps { get { return steps; } }

        public static int Count { get { return steps.Length; } }

        /**
         * @brief id를 실제 퀘스트 자리로 푼다. **표를 읽는 시점에 한 번만 돈다.**
         *
         * 정적 생성자에서 도는 이유는 이 계산이 화면 갱신마다 반복될 필요가
         * 없기 때문이다. 가이드 카드는 요괴를 벨 때마다 다시 그려지고, 그때마다
         * 열네 칸 x 열네 줄의 문자열 비교를 돌리는 것은 한 줄짜리 표시가
         * 전투 프레임에 물어야 할 값이 아니다.
         */
        static GuideQuestCatalog()
        {
            var kinds = new[] { QuestKind.Achievement, QuestKind.Daily, QuestKind.Repeat };

            for (int i = 0; i < steps.Length; i++)
            {
                // 퀘스트를 안 가리키는 칸은 표에서 찾을 것이 없다
                if (steps[i].Gate != GuideGate.Quest) continue;

                for (int k = 0; k < kinds.Length && steps[i].Index < 0; k++)
                {
                    var specs = QuestCatalog.Of(kinds[k]);
                    for (int s = 0; s < specs.Length; s++)
                    {
                        if (specs[s].Id != steps[i].QuestId) continue;
                        steps[i].Kind = kinds[k];
                        steps[i].Index = s;
                        break;
                    }
                }
            }
        }

        /**
         * @brief 퀘스트를 안 가리키는 칸. 완료 판정을 밖에서 받는다.
         *
         * `Index`가 -1로 남는 것이 요점이다 - 정적 생성자가 퀘스트 표를 훑을 때
         * 이 칸을 못 찾는 것이 정상이고, `GuideQuestLine`이 Gate를 먼저 보고
         * 그 -1을 "빠진 퀘스트"로 오해하지 않는다.
         */
        private static GuideStep Gate(string action, GuideGate gate, int minStage)
        {
            GuideStep step;
            step.QuestId = string.Empty;
            step.Action = action;
            step.Kind = QuestKind.Achievement;
            step.Index = -1;
            step.Gate = gate;
            step.MinStage = minStage;
            return step;
        }

        private static GuideStep Step(string questId, string action)
        {
            GuideStep step;
            step.QuestId = questId;
            step.Action = action;
            step.Kind = QuestKind.Achievement;

            // -1로 시작한다. 정적 생성자가 표에서 찾아 채우고, 못 찾으면
            // 그대로 남아 화면이 그 칸을 건너뛴다 - 퀘스트를 표에서 빼도
            // 가이드가 예외를 던지지 않는다(QuestSystem.ReadClaims와 같은 규칙)
            step.Index = -1;
            step.Gate = GuideGate.Quest;
            step.MinStage = 0;
            return step;
        }
    }
}
