using Onikiri.Core;

namespace Onikiri.Progression
{
    /** 퀘스트가 세는 것 */
    public enum QuestMetric
    {
        /** 잡몹 처치 수 */
        MobKills,

        /** 보스 처치 수 */
        BossKills,

        /** 획득한 골드 (누적) */
        GoldEarned,

        /** 오의 시전 횟수 */
        SkillCasts,

        /** 강화 구매 횟수 */
        UpgradePurchases,

        // -------- 아래는 "지금 상태"를 읽는다. 누적 카운터가 아니다 --------

        /** 도달한 스테이지 */
        StageReached,

        /** 캐릭터 레벨 */
        LevelReached,

        /** 강화 레벨 총합 (일곱 축) */
        UpgradeLevelTotal,

        /** 오의 레벨 총합 (셋) */
        SkillLevelTotal
    }

    /**
     * @brief 퀘스트의 종류. **보상의 성격이 여기서 갈린다.**
     *
     *   Daily        매일 리셋. 오늘치 카운터를 읽는다. 보상 = 보석 소량
     *   Repeat       무한 누적. 티어가 계속 갱신된다. 보상 = 보석
     *   Achievement  일회성 마일스톤. 보상 = 골드 + 경험치 + 보석
     *
     * 골드/경험치를 업적에만 두는 것이 이 단계의 밸런스 원칙이다 - 그 둘만이
     * 진행 속도를 바꾸고, 따라서 보스 여유 밴드를 건드린다. 일일과 반복은
     * **매일 반복해서 들어오므로** 골드를 주면 파밍 곡선 자체가 이동한다.
     */
    public enum QuestKind
    {
        Daily,
        Repeat,
        Achievement
    }

    /** 퀘스트 하나의 정의 */
    public struct QuestSpec
    {
        public string Id;
        public QuestKind Kind;
        public QuestMetric Metric;
        public string Title;

        /**
         * @brief 목표치.
         *
         * Daily/Achievement는 이 값에 닿으면 완료다.
         * Repeat은 **한 티어의 폭**이다 - 100마리마다 하나씩 계속 열린다.
         */
        public double Target;

        /** 보석 보상. 세 종류 모두 준다 */
        public int Gems;

        /**
         * @brief 골드 보상을 **잡몹 몇 마리분**으로 적는다. 업적만 0이 아니다.
         *
         * 절대값이 아니라 배수인 이유는 스테이지마다 화폐 단위가 다르기 때문이다.
         * StageCurve.ClearGoldForStage가 같은 방식을 쓴다 - "그 구간의 파밍
         * 몇 초분"이 유일하게 의미가 통하는 단위다.
         */
        public double GoldMobs;

        /** 경험치 보상을 **보스 몇 마리분**으로. 업적만 0이 아니다 */
        public double ExpBosses;
    }

    /**
     * @brief 퀘스트 표. 밸런스 표와 같은 자리다.
     *
     * ## 왜 ScriptableObject가 아닌가
     *
     * 강화 축(UpgradeSystem)·오의(SkillCatalog)와 같은 이유다. 이 값들은 아트가
     * 아니라 **밸런스**이고, 밸런스는 시뮬레이션이 읽어야 한다. 에셋에 두면
     * StageSimulation이 씬 없이 돌지 못하고, 그러면 테스트가 실제 값을 검사할 수
     * 없다(StageSimulation 파일 머리 주석의 9단계 사고).
     *
     * ## 일일 다섯 개를 전부 노출한다
     *
     * 사양이 "3~5개 노출"이라 다섯을 만들고 전부 보여준다. 무작위로 셋을 뽑는
     * 방식도 있지만 그러면 **뽑기 상태를 저장해야 하고**, 리셋 경계에서 뽑기와
     * 리셋의 순서가 어긋나면 "어제 것을 오늘 클리어" 같은 상태가 생긴다.
     * 다섯 개 고정은 그 상태 자체가 없다.
     *
     * 다섯 개가 서로 다른 행동을 가리키는 것이 요점이다 - 파밍(처치)·진행(보스)·
     * 소비(강화)·오의(시전)·수입(골드). 신규 플레이어가 목록만 읽어도 이 게임에서
     * 할 수 있는 일이 무엇인지 알게 된다.
     */
    public static class QuestCatalog
    {
        // ---------------------------------------------------------------- 일일

        /**
         * @brief 일일 목표치는 **초반 플레이어 기준**이다.
         *
         * 잡몹 60마리는 시뮬레이션에서 st1~5 구간 6스테이지분이고 3분이 안 걸린다.
         * 후반 플레이어에게는 순식간이지만 그것이 맞다 - 일일 퀘스트는 난이도가
         * 아니라 **접속 이유**이고, 오래 한 사람에게 더 오래 시키는 것은 리텐션이
         * 아니라 벌이다.
         *
         * 온보딩(1~5스테이지 175초)을 왜곡하지 않게 보상은 보석뿐이다.
         */
        public static readonly QuestSpec[] Daily =
        {
            new QuestSpec { Id = "daily_kill",    Kind = QuestKind.Daily, Metric = QuestMetric.MobKills,
                            Title = "요괴 60마리 처치",   Target = 60d,   Gems = 10 },
            new QuestSpec { Id = "daily_boss",    Kind = QuestKind.Daily, Metric = QuestMetric.BossKills,
                            Title = "보스 3회 처치",      Target = 3d,    Gems = 15 },
            new QuestSpec { Id = "daily_gold",    Kind = QuestKind.Daily, Metric = QuestMetric.GoldEarned,
                            Title = "골드 2,000 획득",    Target = 2000d, Gems = 10 },
            new QuestSpec { Id = "daily_skill",   Kind = QuestKind.Daily, Metric = QuestMetric.SkillCasts,
                            Title = "오의 20회 시전",     Target = 20d,   Gems = 10 },
            new QuestSpec { Id = "daily_upgrade", Kind = QuestKind.Daily, Metric = QuestMetric.UpgradePurchases,
                            Title = "강화 15회 구매",     Target = 15d,   Gems = 10 }
        };

        // ---------------------------------------------------------------- 반복

        /**
         * @brief 반복은 **티어가 무한히 갱신된다.**
         *
         * 누적 카운터를 목표치로 나눈 몫이 열린 티어 수다. 100마리를 잡으면 1티어,
         * 200마리면 2티어가 열린다. 받지 않고 쌓아둘 수 있고, 받으면 다음 티어가
         * 곧바로 목표가 된다.
         *
         * 상한을 두지 않는 것이 요점이다. 상한이 있으면 언젠가 목록이 전부 회색이
         * 되고, 그 순간 "반복"이라는 이름이 거짓말이 된다.
         */
        public static readonly QuestSpec[] Repeat =
        {
            new QuestSpec { Id = "repeat_kill",  Kind = QuestKind.Repeat, Metric = QuestMetric.MobKills,
                            Title = "누적 요괴 100마리",  Target = 100d, Gems = 5 },
            new QuestSpec { Id = "repeat_boss",  Kind = QuestKind.Repeat, Metric = QuestMetric.BossKills,
                            Title = "누적 보스 10회",     Target = 10d,  Gems = 8 },
            new QuestSpec { Id = "repeat_skill", Kind = QuestKind.Repeat, Metric = QuestMetric.SkillCasts,
                            Title = "누적 오의 50회",     Target = 50d,  Gems = 5 }
        };

        // ---------------------------------------------------------------- 업적

        /**
         * @brief 업적만 골드를 준다. **그래서 시뮬레이션에 들어간다.**
         *
         * ## 크기는 "달성한 그 스테이지의 몇 마리분"이다
         *
         * 잡몹 1~2마리분이다. 그 구간의 보스 한 마리가 10.8마리분
         * (StageCurve.BossGoldMultiplier)이므로 **보스 한 마리의 10~20%**다.
         * 화면에는 그 스테이지 단위의 큰 숫자로 뜨지만 실제로는 그 정도다.
         *
         * ## 경험치는 결국 0이 됐다
         *
         * 처음에는 보스 0.25~0.5마리분을 줬다. 두 번 깎고도 기존 테스트
         * `SkillAxisTests.Skills_UnlockWhereTheCostAssumes`가 계속 깨졌다 -
         * **일섬 해금(Lv.15)이 st15에서 st14로 한 칸 당겨졌다.**
         *
         * 그 한 칸이 그냥 넘어갈 일이 아니었다. 오의 가격은 해금 스테이지의
         * 골드 배수에 묶여 있고(SkillCatalog.BaseCost) 골드는 스테이지마다
         * x1.72라, 한 칸 일찍 열리면 그 오의가 그 시점 경제에서 1.72배 비싼
         * 물건이 된다.
         *
         * 레벨은 오의 해금과 스탯 포인트를 동시에 좌우하는 축이라 **일회성
         * faucet이 건드릴 자리가 아니다.** 골드는 사면 없어지지만 레벨은
         * 되돌릴 수 없다. 그래서 경험치를 통째로 뺐다.
         *
         * 자리는 남겨 둔다(ExpBosses). 나중에 레벨 곡선과 무관한 보상이
         * 필요해지면 그때 다시 켠다 - 지금 지우면 그 판단의 근거도 함께 사라진다.
         *
         * ## 기준 스테이지를 손으로 적지 않는다
         *
         * 처음에는 각 업적에 `RewardStage`를 적어 뒀다 - "레벨 25는 대략
         * 9스테이지쯤". 실측에서 물렸다: **st11 여유가 2.79에서 18.73으로**
         * 튀었고 일반 밴드 최대가 101.97까지 갔다.
         *
         * 원인이 둘이었다. 하나는 추정이 빗나가 st16 크기의 보상이 st8에
         * 떨어진 것이고, 다른 하나는 그 경험치가 다음 레벨 업적을 같은
         * 스테이지에서 연달아 열어 사슬을 만든 것이다.
         *
         * 그래서 기준을 **달성하는 순간의 스테이지**로 바꿨다. 추정이 사라지므로
         * 빗나갈 수가 없고, 사슬이 생겨도 각 고리가 그 스테이지 크기로 묶인다.
         * 시뮬레이션과 게임이 같은 값을 읽는 것도 저절로 따라온다 - 양쪽 다
         * "지금 스테이지"를 넘긴다.
         */
        public static readonly QuestSpec[] Achievement =
        {
            new QuestSpec { Id = "ach_stage5",   Kind = QuestKind.Achievement, Metric = QuestMetric.StageReached,
                            Title = "5스테이지 도달",  Target = 5d,  Gems = 20, GoldMobs = 1d,    ExpBosses = 0d },
            new QuestSpec { Id = "ach_stage10",  Kind = QuestKind.Achievement, Metric = QuestMetric.StageReached,
                            Title = "지역 1 돌파",     Target = 10d, Gems = 40, GoldMobs = 1.25d, ExpBosses = 0d },
            new QuestSpec { Id = "ach_stage20",  Kind = QuestKind.Achievement, Metric = QuestMetric.StageReached,
                            Title = "지역 2 돌파",     Target = 20d, Gems = 60, GoldMobs = 1.5d,  ExpBosses = 0d },
            new QuestSpec { Id = "ach_stage30",  Kind = QuestKind.Achievement, Metric = QuestMetric.StageReached,
                            Title = "30스테이지 도달", Target = 30d, Gems = 80, GoldMobs = 2d,    ExpBosses = 0d },

            new QuestSpec { Id = "ach_level10",  Kind = QuestKind.Achievement, Metric = QuestMetric.LevelReached,
                            Title = "레벨 10 달성",    Target = 10d, Gems = 20, GoldMobs = 1d,    ExpBosses = 0d },
            new QuestSpec { Id = "ach_level25",  Kind = QuestKind.Achievement, Metric = QuestMetric.LevelReached,
                            Title = "레벨 25 달성",    Target = 25d, Gems = 40, GoldMobs = 1.25d, ExpBosses = 0d },
            new QuestSpec { Id = "ach_level50",  Kind = QuestKind.Achievement, Metric = QuestMetric.LevelReached,
                            Title = "레벨 50 달성",    Target = 50d, Gems = 60, GoldMobs = 1.5d,  ExpBosses = 0d },

            new QuestSpec { Id = "ach_upgrade50",  Kind = QuestKind.Achievement, Metric = QuestMetric.UpgradeLevelTotal,
                            Title = "강화 총합 50",  Target = 50d,  Gems = 25, GoldMobs = 1d,   ExpBosses = 0d },
            new QuestSpec { Id = "ach_upgrade150", Kind = QuestKind.Achievement, Metric = QuestMetric.UpgradeLevelTotal,
                            Title = "강화 총합 150", Target = 150d, Gems = 50, GoldMobs = 1.5d, ExpBosses = 0d },

            new QuestSpec { Id = "ach_skill12", Kind = QuestKind.Achievement, Metric = QuestMetric.SkillLevelTotal,
                            Title = "오의 총 레벨 12", Target = 12d, Gems = 50, GoldMobs = 1.5d, ExpBosses = 0d }
        };

        public static int DailyCount { get { return Daily.Length; } }
        public static int RepeatCount { get { return Repeat.Length; } }
        public static int AchievementCount { get { return Achievement.Length; } }

        /** 세 종류를 한 줄로 이어 본 전체 수. 세이브 배열의 길이다 */
        public static int TotalCount
        {
            get { return Daily.Length + Repeat.Length + Achievement.Length; }
        }

        public static QuestSpec[] Of(QuestKind kind)
        {
            switch (kind)
            {
                case QuestKind.Daily: return Daily;
                case QuestKind.Repeat: return Repeat;
                default: return Achievement;
            }
        }

        /** id로 찾는다. 없으면 Id가 null인 기본값 */
        public static QuestSpec Find(string id)
        {
            foreach (var kind in new[] { QuestKind.Daily, QuestKind.Repeat, QuestKind.Achievement })
                foreach (var spec in Of(kind))
                    if (spec.Id == id) return spec;

            return default(QuestSpec);
        }

        /**
         * @brief 업적 하나의 골드 보상. **잡몹 골드 x 지금 스테이지 배수 x 마리 수**다.
         *
         * StageCurve.ClearGoldForStage와 같은 식이다. 그 함수가 클리어 축하를
         * 그 구간의 파밍 몇 초분으로 환산하듯, 여기도 같은 단위를 쓴다 - 두 보상이
         * 서로 다른 단위로 적히면 어느 쪽이 큰지 표를 봐도 알 수 없다.
         *
         * @param atStage 받는 순간의 스테이지. 게임은 StageProgress.Stage,
         *                시뮬레이션은 루프의 stage를 넘긴다. 위 표 주석 참고
         */
        public static BigDouble AchievementGold(QuestSpec spec, BigDouble averageMobGold, int atStage)
        {
            if (spec.GoldMobs <= 0d) return BigDouble.Zero;

            return averageMobGold
                   * StageCurve.GoldMultiplier(atStage)
                   * BigDouble.FromDouble(spec.GoldMobs);
        }

        /** 업적 하나의 경험치 보상. **받는 순간** 스테이지의 보스 몇 마리분 */
        public static BigDouble AchievementExp(QuestSpec spec, int atStage)
        {
            if (spec.ExpBosses <= 0d) return BigDouble.Zero;

            return ExpCurve.BossExp(atStage) * BigDouble.FromDouble(spec.ExpBosses);
        }
    }
}
