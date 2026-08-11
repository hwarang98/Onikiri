using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 성장 패널(화면 하단 10~45%)의 강화 UI를 구성한다.
     *
     * 강화 곡선 값도 여기서 기록한다. 스크립트의 기본값에 맡기면 컴포넌트가 이미 씬에
     * 있는 순간부터 코드를 고쳐도 반영되지 않고, 빌더가 단일 출처 역할을 못 하게 된다.
     */
    public static class UpgradePanelBuilder
    {
        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        /** 강화 한 줄의 설계값 */
        private struct TrackSpec
        {
            public string Id;
            public string DisplayName;
            public double BaseCost;
            public double CostGrowth;
            public UpgradeTrack.Curve Curve;
            public double BaseValue;
            public double Step;
            public int MaxLevel;

            /** 0이면 없음. 구매(MaxLevel)가 아니라 효과값을 막는다 */
            public double ValueCeiling;

            /** 효과값의 표시 단위 */
            public UpgradeTrack.Display Display;

            /** 이 스테이지 전에는 잠긴다. 0이면 잠금 없음 */
            public int UnlockStage;

            /**
             * @brief 심화 게이트 (43단계). 치명타 확률이 MASTER(100%)여야 열린다.
             *
             * 스테이지 게이트(UnlockStage)와 종류가 다르다 - 저쪽은 여정의
             * 위치, 이쪽은 다른 축의 완성이 문이다. 조건 판정은
             * TranscendCurve.IsUnlockedAt 한 곳이고 화면·시뮬·시스템이 전부
             * 그것을 읽는다.
             */
            public bool DeepGate;
        }

        private static readonly TrackSpec[] Specs =
        {
            // 공격력: 지수. 요괴 체력도 티어마다 지수로 오르므로 같은 형태여야 따라간다.
            // baseValue 5는 PlayerCombat이 지금 쓰는 데미지와 같은 값이다. 레벨 1의
            // 곡선값과 시작 스탯이 어긋나면 첫 구매에서 수치가 튄다
            new TrackSpec {
                Id = UpgradeSystem.AttackPowerId, DisplayName = "공격력 강화",
                BaseCost = AttackPowerCurve.BaseCost, CostGrowth = AttackPowerCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = AttackPowerCurve.BaseValue, Step = AttackPowerCurve.Step,
                MaxLevel = 0, ValueCeiling = 0d
            },
            // 공격속도: 공격력과 같은 형태의 곱연산 + 상한.
            //
            // 8단계까지는 선형(+0.12/레벨)에 비용만 지수(x1.35)였다. 그 조합은 계수와
            // 무관하게 반드시 죽는다. 값의 증가율은 레벨이 오를수록 0으로 수렴하는데
            // 비용은 계속 지수로 오르기 때문이다. 실측한 시점에 골드당 효율이 공격력의
            // 1/89까지 벌어져 있었고(Lv.31 대 Lv.51), 그 상태에서는 아무도 누르지 않는
            // 버튼이 화면만 차지한다.
            //
            // 이제 둘 다 곱연산이고 비용 증가율도 같다(x1.15). 그러면 같은 레벨에서의
            // 효율 비율이 레벨과 무관하게 일정해지고(여기서는 공격력이 1.2배 우위),
            // 한쪽 레벨이 오르면 그쪽 비용이 올라 자연히 다른 쪽 차례가 온다.
            // UpgradeEfficiencyTests가 이 관계를 못 박는다.
            //
            // 상한은 여기 적지 않고 아트에서 유도한다. AttackSpeedCurve 참고.
            // 지금 클립(7프레임 / 14fps) 기준으로 Lv.32 = 3.88회/초다.
            new TrackSpec {
                Id = UpgradeSystem.AttackSpeedId, DisplayName = "공격속도 강화",
                BaseCost = AttackSpeedCurve.BaseCost, CostGrowth = AttackSpeedCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = AttackSpeedCurve.BaseValue, Step = AttackSpeedCurve.Step,
                // MaxLevel은 "더 팔지 않는다", ValueCeiling은 "더 세지지 않는다"이다.
                // 둘 다 같은 상한에서 나오지만 세이브 복원에서 갈라진다 - 상한을 낮춘
                // 업데이트에서 예전 레벨은 남고 효과만 막힌다. UpgradeTrack 참고
                MaxLevel = AttackSpeedCurve.MaxLevel,
                ValueCeiling = AttackSpeedCurve.Ceiling
            },
            // 치명타 확률: 가산 + 상한 60%.
            //
            // 확률은 곱연산으로 키울 수 없다. 1을 넘는 순간 의미를 잃기 때문이다.
            // 8단계의 공격속도가 가산이라 죽었던 것과 다른 점은 DPS 기여 구조다 -
            // 확률의 기여는 rate x (배수 - 1) 이라, 치명타 피해 축이 함께 자라면
            // 확률 한 칸의 값어치도 함께 자란다. CritRateCurve 참고.
            new TrackSpec {
                Id = UpgradeSystem.CritRateId, DisplayName = "치명타 확률",
                BaseCost = CritRateCurve.BaseCost, CostGrowth = CritRateCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Additive,
                BaseValue = CritRateCurve.BaseValue, Step = CritRateCurve.Step,
                MaxLevel = CritRateCurve.MaxLevel, ValueCeiling = CritRateCurve.Ceiling,
                Display = UpgradeTrack.Display.Percent
            },
            // 치명타 피해: 곱연산 + 상한 없음.
            //
            // 공격력과 함께 후반 DPS를 끝까지 끌고 간다. 이 축만 DPS 기여율이
            // 레벨과 함께 **커진다** - 배수가 커질수록 치명타가 DPS의 대부분을
            // 차지하게 되기 때문이다. CritDamageCurve 참고.
            new TrackSpec {
                Id = UpgradeSystem.CritDamageId, DisplayName = "치명타 피해",
                BaseCost = CritDamageCurve.BaseCost, CostGrowth = CritDamageCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = CritDamageCurve.BaseValue, Step = CritDamageCurve.Step,
                MaxLevel = 0, ValueCeiling = 0d,
                Display = UpgradeTrack.Display.Multiplier
            },
            // 체력: 곱연산 + 상한 없음. 보스 공격력이 스테이지마다 지수로 오르므로
            // 같은 형태여야 따라간다
            new TrackSpec {
                Id = UpgradeSystem.HealthId, DisplayName = "체력 강화",
                BaseCost = HealthCurve.BaseCost, CostGrowth = HealthCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = HealthCurve.BaseValue, Step = HealthCurve.Step,
                MaxLevel = 0, ValueCeiling = 0d,
                Display = UpgradeTrack.Display.Plain
            },
            // 체력 회복: 초당 회복 비율 + **상한**.
            //
            // 가산으로 두면 8단계 공격속도와 같은 이유로 죽으므로 곱연산이다.
            // 16단계에서 상한이 붙었다 - 비율 곱연산에 천장이 없어서 Lv.54에서
            // 156%/s까지 갔고, 그 지점에서 체력 게이트가 통째로 무력화됐다.
            //
            // 공격속도와 같은 처리다: MaxLevel은 "더 팔지 않는다",
            // ValueCeiling은 "더 세지지 않는다". 상한을 낮춘 업데이트에서
            // 예전 세이브의 레벨은 남고 효과만 막힌다
            new TrackSpec {
                Id = UpgradeSystem.HealthRegenId, DisplayName = "체력 회복",
                BaseCost = HealthRegenCurve.BaseCost, CostGrowth = HealthRegenCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = HealthRegenCurve.BaseValue, Step = HealthRegenCurve.Step,
                MaxLevel = HealthRegenCurve.MaxLevel,
                ValueCeiling = HealthRegenCurve.Ceiling,
                Display = UpgradeTrack.Display.PerSecond
            },
            // 골드 획득량: 곱연산 + 상한 x1.25.
            //
            // 20단계에서 생긴 일곱 번째 축이고, 앞의 여섯과 종류가 다르다 -
            // 골드를 화력이나 생존이 아니라 **골드로** 바꾼다. 그래서 효율을
            // 재는 자도 다르다(GoldGainEfficiency의 회수 시간).
            //
            // 배수로 표시한다. 값이 1.00에서 1.25까지만 움직이므로 Plain으로
            // 두면 소수 둘째 자리 변화가 "안 오르는 버튼"으로 읽힌다
            new TrackSpec {
                Id = UpgradeSystem.GoldGainId, DisplayName = "골드 획득량",
                BaseCost = GoldGainCurve.BaseCost, CostGrowth = GoldGainCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = GoldGainCurve.BaseValue, Step = GoldGainCurve.Step,
                MaxLevel = GoldGainCurve.MaxLevel,
                ValueCeiling = GoldGainCurve.Ceiling,
                Display = UpgradeTrack.Display.Multiplier,

                // 온보딩 뒤에 열린다. 그 전에는 회수보다 구간이 먼저 끝나서
                // 지표가 사라고 말하지만 실제로는 손해다(21단계)
                UnlockStage = GoldGainCurve.UnlockStage
            },

            // ---------------------------------------------------- 43단계: 심화
            // 초월 치명타: 전타 치명타(확률 100%) 뒤의 무한 배수. 곱연산 +
            // 상한 없음 - 공격력·치명타 피해와 같은 부류이고, 자기 제한은
            // 비용 증가율 2.0이 한다(TranscendCurve 주석)
            new TrackSpec {
                Id = UpgradeSystem.TranscendId, DisplayName = "초월 치명타",
                BaseCost = TranscendCurve.BaseCost, CostGrowth = TranscendCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = 1d, Step = TranscendCurve.Step,
                MaxLevel = 0, ValueCeiling = 0d,
                Display = UpgradeTrack.Display.Multiplier,
                DeepGate = true
            },
            // 연격: 타격마다 한 번 더 베는 확률. 가산 + 상한 100%(확정
            // 2연격 = MASTER) - 치명타 확률과 같은 생애를 산다
            new TrackSpec {
                Id = UpgradeSystem.ComboId, DisplayName = "연격",
                BaseCost = ComboCurve.BaseCost, CostGrowth = ComboCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Additive,
                BaseValue = 0d, Step = ComboCurve.Step,
                MaxLevel = ComboCurve.MaxLevel, ValueCeiling = ComboCurve.Ceiling,
                Display = UpgradeTrack.Display.Percent,
                DeepGate = true
            }
        };

        /** 스탯 포인트로 사는 증폭 축. 골드 축과 재화만 다르고 하는 일은 같다 */
        private struct StatSpec
        {
            public string AxisId;
            public string DisplayName;
        }

        private static readonly StatSpec[] StatSpecs =
        {
            new StatSpec { AxisId = CharacterLevel.AttackAmpId, DisplayName = "공격력 증폭" },
            new StatSpec { AxisId = CharacterLevel.HealthAmpId, DisplayName = "체력 증폭" }
        };

        /**
         * @brief 남은 포인트 머리글이 차지하는 줄 수.
         *
         * 머리글이 행으로 세어지는 이유는 페이지가 인덱스로 세로 위치를 잡기
         * 때문이다 - 행이 아닌 것을 끼워 넣으면 그 아래가 전부 한 칸씩 어긋난다.
         */
        private const int HeaderRows = 1;

        /**
         * @brief 최상위 페이지. 가르는 기준은 **재화**다.
         *
         * 18단계에서 생겼다. 17단계까지는 강화(골드)와 성장(포인트)이 한 스크롤에
         * 이어져 있어서, 계열 탭을 어느 쪽으로 돌려도 화면 아래쪽에는 포인트 축이
         * 붙어 있었다 - 탭 줄은 골드뿐인데 화면은 두 재화가 섞인 상태였다.
         *
         * 페이지를 오브젝트로 만드는 이유는 **행이 절대 좌표로 놓이기** 때문이다.
         * 페이지 루트를 하나 두면 그 안의 세로 배치가 페이지 안에서 닫히고,
         * 행마다 소속 페이지를 따져 오프셋을 더하는 식이 필요 없다.
         *
         * 켜고 끄는 단위이기도 하다. GrowthPanelTabs는 루트 하나만 SetActive 한다.
         */
        public const string EnhancePageName = "EnhancePage";
        public const string GrowthPageName = "GrowthPage";
        public const string AwakenPageName = "AwakenPage";

        public static readonly string[] PageNames =
        {
            EnhancePageName, GrowthPageName, AwakenPageName
        };

        /**
         * @brief 페이지 하나에 들어가는 행 수. 빌드 검사가 잔재를 세는 기준이다.
         *
         * 11단계에 행의 부모가 바뀌면서 옛 행이 지워지지 않고 남아 유령 텍스트가
         * 됐다. 그 종류의 사고는 눈이 아니라 빌드가 세어야 한다.
         */
        public static int RowsInPage(string pageName)
        {
            // 강화는 축 여섯에 계열 머리글 셋이 섞여 든다(19단계에 서브탭이
            // 머리글로 바뀌었다). 머리글도 자식이므로 함께 세지 않으면 이
            // 검사가 매번 실패한다
            if (pageName == EnhancePageName) return Specs.Length + Categories.Length;
            if (pageName == GrowthPageName) return HeaderRows + StatSpecs.Length;
            if (pageName == AwakenPageName) return AwakenRows;
            return 0;
        }

        /**
         * @brief 전직 페이지의 자식 수. **잠금 안내 + 진화 카드 둘이다** (33단계).
         *
         * 둘은 같은 자리에 겹쳐 서고 상태(Lv.30 해금)에 따라 하나만 켜진다
         * (EvolutionPanel). 잔재 검사는 켜짐과 무관하게 개수를 세므로 둘 다 든다.
         */
        private const int AwakenRows = 2;

        /**
         * @brief 잠금 안내가 세로로 차지하는 줄 수.
         *
         * 한 줄짜리 안내가 텅 빈 페이지 맨 위에 붙어 있으면 "행 하나만 로드된
         * 목록"처럼 보이므로 두 줄 높이로 세운다.
         */
        private const int AwakenRowSpan = 2;

        /**
         * @brief 진화 카드의 높이 (33단계).
         *
         * 페이지 높이가 이 값에서 나온다. 뷰포트(584px)를 넘지 않아야 한다 -
         * 성장·전직 페이지는 스크롤하지 않는 것이 설계이고(VerifyPageHeights),
         * TopPadding(12) + 540 + RowGap(14) = 566 < 584가 그 검산이다.
         */
        private const float AwakenCardHeight = 540f;

        /**
         * @brief 강화 축의 계열. 이제 **목록 안의 머리글**이고 탭이 아니다.
         *
         * 17단계에 서브탭으로 만들었다가 19단계에 되돌렸다. 탭은 한 번에 두 축만
         * 보여주는 대신 나머지 넷을 **감춘다** - 무엇이 있는지 알려면 탭을 세 번
         * 눌러야 하고, 어느 축에 골드를 쓸지 고르는 화면에서 그 비용이 스크롤
         * 한 번보다 크다. 여섯이면 훑어 내리는 편이 낫다.
         *
         * 배열의 순서가 곧 화면의 순서다(공격 -> 치명타 -> 생존). 계열 순서와
         * Specs 순서가 같아야 트랙 인덱스와 화면 순서가 어긋나지 않는다.
         */
        private struct CategorySpec
        {
            public string DisplayName;
            public string[] TrackIds;
        }

        private static readonly CategorySpec[] Categories =
        {
            new CategorySpec { DisplayName = "공격", TrackIds = new[] {
                UpgradeSystem.AttackPowerId, UpgradeSystem.AttackSpeedId } },
            new CategorySpec { DisplayName = "치명타", TrackIds = new[] {
                UpgradeSystem.CritRateId, UpgradeSystem.CritDamageId } },
            new CategorySpec { DisplayName = "생존", TrackIds = new[] {
                UpgradeSystem.HealthId, UpgradeSystem.HealthRegenId } },
            // 20단계. 공격/치명타/생존 어디에도 안 맞는다 - 이 축은 전투를
            // 세게 하는 것이 아니라 벌이를 늘린다. 계열 이름이 "무엇을 세게
            // 하는가"의 분류이므로 새 이름이 필요하다
            new CategorySpec { DisplayName = "획득", TrackIds = new[] {
                UpgradeSystem.GoldGainId } },

            // 43단계. 치명타 확률 100% 뒤에 열리는 심화 축들. 맨 끝이 맞다 -
            // 목록의 순서가 여정의 순서다(잠긴 마지막 절이 "앞으로 무엇이
            // 열리는가"를 말한다 - LockedTab과 같은 규칙)
            new CategorySpec { DisplayName = "심화", TrackIds = new[] {
                UpgradeSystem.TranscendId, UpgradeSystem.ComboId } },
        };

        /** 탭 줄 높이와 그 아래 여백 */
        private const float TabBarHeight = 76f;
        private const float TabBarGap = 12f;

        /** 탭 줄이 세로로 먹는 몫. 19단계에 계열 줄이 빠져 이제 한 줄뿐이다 */
        private const float TabRowStride = TabBarHeight + TabBarGap;

        public const string TopTabBarName = "TopTabBar";

        /**
         * @brief 경험치 스트립 (2a 후속). 패널 최상단 경계의 풀폭 얇은 바.
         *
         * 상단 바에 있던 60px 경험치 바가 내려온 자리다. 세우는 것은
         * BattleContentBuilder.BuildExpRow(LevelHud 배선이 거기 있다)이고,
         * 여기는 자리만 안다 - 탭 줄과 뷰포트가 그만큼 내려앉는다.
         *
         * 높이 10px은 "채움이 보이는 가장 얇은 줄"이다. 바탕의 경계선(TopEdge,
         * 6px)과 같은 색 트랙이라, 비어 있을 때는 구분선으로만 읽힌다.
         */
        public const string ExpStripName = "ExpStrip";
        public const float ExpStripHeight = 10f;

        /** 스트립이 패널 위쪽에서 먹는 몫. 탭 줄이 이만큼 내려앉는다 */
        public const float ExpStripStride = ExpStripHeight + TabBarGap;

        /** 성장 패널 직속에 있어도 되는 것. 그 외는 잔재다 */
        // 2b: 레벨업 버튼이 상단 바에서 패널 헤더(스트립 라인)로 내려왔다.
        // 세우는 것은 BattleContentBuilder.BuildExpRow다
        public static readonly string[] PanelChildNames =
        {
            "Viewport", TopTabBarName, ExpStripName, "LevelUpButton"
        };

        // 1080 폭 캔버스 기준 배치값. 55pt 글자가 들어가야 하므로 줄 높이는 넉넉히 준다
        private const float SidePadding = 48f;

        /**
         * @brief 한 줄의 높이. 55pt 글자에 위아래 여백을 더한 값.
         *
         * 10단계에서 축이 둘에서 넷으로 늘면서 72에서 62로 줄였다. 11단계에서
         * 여섯이 되면서 **더 줄일 수 없는 지점에 닿았다.**
         *
         *   성장 패널 높이 = 1920 x 35% = 672px
         *   여섯 줄이 들어가려면 한 줄에 최대 약 104px
         *   한 줄은 55pt 글자 두 줄이므로 최소 62 x 2 + 여백
         *
         * 글자를 줄이는 선택지는 없다. 55는 아틀라스를 구운 크기이고 그 사이 값을
         * 쓰면 비트맵이 리샘플되어 흐려진다(PixelFontSizes).
         *
         * 그래서 스크롤을 넣었다. 10단계 주석에 "축이 여섯을 넘어가면 탭으로
         * 나누는 것이 스크롤보다 낫다"고 적었는데, 지금이 정확히 여섯이고
         * 12단계가 UI 개편이다. 탭은 그때 만들고 지금은 목록이 잘리지 않게만 한다.
         */
        private const float LineHeight = 62f;

        /** 이름/비용 한 줄 + 증가폭 한 줄 */
        private const float RowHeight = LineHeight * 2f + 20f;
        private const float RowGap = 14f;
        private const float TopPadding = 12f;

        /**
         * @brief 계열 머리글의 높이와 여백.
         *
         * **한 줄짜리다.** 행(144)의 절반도 안 된다 - 머리글이 행만큼 두꺼우면
         * 목록이 아홉 줄로 읽히고, 그러면 나누려던 여섯이 오히려 더 길어 보인다.
         * 배경판도 버튼도 없다(EnsureRow를 쓰지 않는 이유). 눌리지 않는 것이
         * 형태에서 먼저 읽혀야 한다.
         *
         * 머리글 셋이 더해지는 높이는 3 x (62 + 10) + 2 x 18 = 252px이다. 그만큼
         * 스크롤이 길어지는 값을 치르고 훑을 때의 기준점을 얻는다.
         */
        private const float CategoryHeaderHeight = LineHeight;
        private const float CategoryHeaderGap = 10f;

        /** 앞 계열의 마지막 행과 다음 머리글 사이. 여기가 실제로 계열을 가른다 */
        private const float CategoryGroupGap = 18f;

        /** 비용 칸의 고정 폭. "1.5K" 정도는 물론 "999.9aa"도 잘리지 않는 크기 */
        private const float CostWidth = 300f;

        /**
         * @brief 남은 포인트 배지의 크기와 모서리 밖으로 내미는 거리.
         *
         * **한글은 55pt에서 55px/자다.** 17단계가 남긴 33.7px은 숫자·영문의
         * 반각 폭이고(실측 37px), 한글은 그 두 배에 가깝다. 처음에 33.7을 한글에도
         * 쓰는 바람에 탭 이름을 67px로 계산했는데 실제 "성장"은 110px이라, 배지가
         * 글자 오른쪽 15px을 덮어 "장"이 잘렸다.
         *
         *   탭 316  |  "성장" 110 가운데 정렬 -> 103..213
         *   배지 왼쪽 끝 = 316 - 폭 + 내밈 = 228   (글자와 15px 떨어진다)
         *
         * 폭 104는 두 자리("24" = 74px)에 여백이 남고 세 자리("999" = 111px)도
         * 거의 들어가는 크기다. 그보다 키우면 글자를 덮고, 그러면 배지가 알리려던
         * 탭의 이름을 배지가 지우는 셈이 된다.
         *
         * 어림이 아니라 VerifyBadgeClearsLabel이 빌드에서 TMP에게 직접 물어
         * 확인한다 - 같은 어림을 이 프로젝트에서 세 번째로 틀렸다.
         */
        private const float BadgeWidth = 104f;
        private const float BadgeHeight = 62f;

        /** 탭 모서리 밖으로 내미는 거리. 탭 사이 간격(12)과 비슷해야 옆 탭을 덮지 않는다 */
        private const float BadgeOverhang = 16f;

        /** 전직 해금 레벨. 하단 탭바에 있던 값을 그대로 옮겼다 */
        private const int AwakenRequiredLevel = 30;

        /**
         * @brief 아이콘 왼쪽 여백과, 그 뒤 글자가 시작하는 x.
         *
         * 15단계에서 행이 세 칸이 됐다: 왼쪽 아이콘 / 가운데 이름+수치 / 오른쪽 비용.
         * 14단계까지는 글자만 있어서 목록을 훑을 때 눈이 글자를 읽어야만 어느
         * 축인지 알 수 있었다.
         */
        private const float IconLeft = 24f;
        private const float TextLeft = IconLeft + UiIcons.Size + 20f;

        private static readonly Color RowColor = new Color32(0x3A, 0x35, 0x50, 0xFF);
        // 39단계 톤 통일: 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /**
         * @brief 안전 영역 루트가 없는 기존 씬을 옮겨 붙인다.
         *
         * Rebuild Main Scene은 스테이지를 통째로 날리므로 씬을 다시 만들 수 없다.
         * 밴드를 안전 영역 아래로 옮기기만 하면 되고, 밴드의 앵커는 부모 기준 비율이라
         * 옮겨도 그대로 유효하다.
         */
        public static Transform EnsureSafeArea()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null) return null;

            var safeArea = canvas.transform.Find(MainSceneBuilder.SafeAreaName);
            if (safeArea == null)
            {
                var go = new GameObject(MainSceneBuilder.SafeAreaName, typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                safeArea = go.transform;
                Debug.Log("[Onikiri] Added SafeArea root to the existing canvas.");
            }

            if (safeArea.GetComponent<Onikiri.UI.SafeAreaFitter>() == null)
                safeArea.gameObject.AddComponent<Onikiri.UI.SafeAreaFitter>();

            // 순회 중에 부모를 바꾸면 인덱스가 밀린다. 먼저 목록으로 뽑고 옮긴다
            string[] bands = { "BottomTabBar", "GrowthPanel", "BattleArea", "TopBar" };
            foreach (var name in bands)
            {
                var band = canvas.transform.Find(name);
                if (band == null) continue;
                band.SetParent(safeArea, false);
            }

            return safeArea;
        }

        /** 하단 UI 전용 바탕. 지역이 바뀌어도 그대로다 */
        private const string BackdropName = "PanelBackdrop";

        /**
         * @brief 하단 UI(성장 패널 + 탭 바) 뒤에 깔리는 **고정 먹빛 바탕**.
         *
         * ## 왜 필요한가
         *
         * 24단계까지 이 자리는 비어 있었고, 뒤로 라이브 씬의 하늘 채움이 그대로 비쳤다.
         * 그동안 문제가 없어 보였던 것은 지역 1·3이 자줏빛 밤이라 뒤가 어두웠기 때문이다 -
         * 패널이 불투명해 보였을 뿐 실은 씬을 비추고 있었다. 봄숲이 들어오자 하단 절반이
         * 하늘색으로 물들고 글자가 떠 보였다.
         *
         * 두 가지가 걸린다.
         *
         *   가독성   목록 글씨 뒤가 지역마다 다른 색이면 대비를 보장할 수 없다
         *   정체성   "위=씬(지역별로 변한다) / 아래=고정 먹빛 UI"가 무너진다
         *
         * 그래서 지역과 무관한 한 장을 깐다. 이 위에서 행 카드·헤더 색·MASTER 표기는
         * 예전 그대로다 - 바뀌는 것은 밑바탕뿐이다.
         *
         * ## 안전 영역 아래까지 내린다
         *
         * 밴드는 안전 영역 안에 있지만 바탕은 그 아래(제스처 바 구간)까지 덮어야 한다.
         * 안 그러면 화면 맨 밑에 씬이 한 줄 비친다.
         */
        private static void EnsurePanelBackdrop(Transform safeArea)
        {
            if (safeArea == null) return;

            var existing = safeArea.Find(BackdropName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(BackdropName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            // 밴드들보다 먼저 그려져야 한다. 뒤에 두면 목록을 덮는다
            go.transform.SetAsFirstSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, Onikiri.Core.DisplayConfig.GrowthPanelTop);
            rect.offsetMin = new Vector2(0f, -400f);   // 안전 영역 아래로 흘려보낸다
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            image.type = Image.Type.Tiled;
            image.color = UiSkin.PanelInk;
            image.raycastTarget = false;

            // 씬과 UI의 경계. 탭 바 위에 얇은 먹선 하나를 그어 "여기부터 UI"를 말한다.
            // 이것이 없으면 배경이 아무리 어두워도 두 영역이 그라디언트처럼 이어진다
            var edge = new GameObject("TopEdge", typeof(RectTransform));
            edge.transform.SetParent(go.transform, false);

            var edgeRect = (RectTransform)edge.transform;
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = new Vector2(1f, 1f);
            edgeRect.pivot = new Vector2(0.5f, 1f);
            edgeRect.sizeDelta = new Vector2(0f, 6f);
            edgeRect.anchoredPosition = Vector2.zero;

            var edgeImage = edge.AddComponent<Image>();
            edgeImage.color = UiSkin.PanelEdge;
            edgeImage.raycastTarget = false;

            // 화지 위의 벚가지 (39단계). 다른 하단 패널들이 같은 자리에 같은
            // 가지를 얹으므로, 어느 탭을 열어도 가지가 이어져 보인다.
            // 형제 순서: AddSakuraBranch가 맨 앞에 넣지만 TopEdge보다는 뒤에
            // 그려져야 하는데, 여기서는 TopEdge가 자식이라 어차피 나중에 그려진다
            BackdropTextureBuilder.AddSakuraBranch(rect);

            // 가지는 밴드 안(경계선 아래)에 있어야 한다. 백드롭은 GrowthPanelTop에서
            // 시작하므로 그대로 두면 경계선(6px)에 물린다
            var branch = rect.Find(BackdropTextureBuilder.BranchName);
            if (branch != null)
                ((RectTransform)branch).anchoredPosition = new Vector2(0f, -6f);
        }

        /** 성장 패널을 채우고 UpgradeSystem을 배선한다 */
        public static UpgradeSystem Build()
        {
            var safeArea = EnsureSafeArea();
            EnsurePanelBackdrop(safeArea);

            var panel = MainSceneBuilder.FindBand("GrowthPanel");
            if (panel == null)
            {
                Debug.LogError("[Onikiri] GrowthPanel band missing - run Rebuild Main Scene first.");
                return null;
            }

            var samurai = GameObject.Find("Samurai");
            var combat = samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
            if (combat == null)
            {
                Debug.LogError("[Onikiri] PlayerCombat missing - run Build Combat Content first.");
                return null;
            }

            var system = panel.GetComponent<UpgradeSystem>();
            if (system == null) system = panel.gameObject.AddComponent<UpgradeSystem>();

            WriteTracks(system, combat, samurai != null ? samurai.GetComponent<PlayerHealth>() : null);

            var content = EnsureScroll(panel);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            // 세 페이지 모두 스크롤 영역 맨 위에서 시작한다. 19단계에 계열 탭
            // 줄이 빠지면서 강화 페이지가 비워두던 자리도 함께 사라졌다
            var enhancePage = EnsurePage(content, EnhancePageName);
            var growthPage = EnsurePage(content, GrowthPageName);
            var awakenPage = EnsurePage(content, AwakenPageName);

            BuildEnhanceList(enhancePage, system, font);
            BuildStatSection(growthPage, font);
            BuildAwakenPage(awakenPage, font);

            BuildTopTabs(panel, font, enhancePage, growthPage, awakenPage);

            // 페이지가 아닌 Content 직속 자식은 전부 이전 세대의 잔재다. 17단계까지
            // 행이 Content 바로 아래에 있었으므로 이 프로젝트에는 실제로 그것이
            // 남아 있고, 지우지 않으면 페이지 뒤에서 비쳐 보인다 - 11단계 유령
            // 텍스트와 같은 종류다
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                if (System.Array.IndexOf(PageNames, child.name) >= 0) continue;
                Object.DestroyImmediate(child.gameObject);
            }

            VerifyPageHeights();

            Debug.Log(string.Format(
                "[Onikiri] Upgrade panel built: 강화 {0} gold tracks in {1} groups ({2:F0}px, scrolls) / "
                + "성장 {3} stat axes / 전직 Lv.{4} 진화 카드, {5} pages under Content.",
                Specs.Length, Categories.Length, PageHeight(EnhancePageName),
                StatSpecs.Length, AwakenRequiredLevel, content.childCount));
            return system;
        }

        /**
         * @brief 스크롤이 **의도한 페이지에서만** 생기는지.
         *
         * 강화는 19단계부터 여섯 축을 한 목록으로 보여주므로 화면을 넘는 것이
         * 설계다. 성장·전직은 각각 두 축과 안내 하나뿐이라 넘칠 이유가 없고,
         * 넘겼다면 누가 줄 높이나 축 수를 건드린 것이다.
         *
         * 화면에서는 어느 쪽이든 "스크롤이 생겼다"로만 보여서 의도인지 사고인지
         * 구분되지 않는다. 그래서 의도를 여기 적어두고 빌드가 대조한다.
         *
         * 어림하지 않고 빌더가 쓰는 상수로 계산한다 - 17·18단계에서 글자 폭
         * 어림이 세 번 틀린 뒤로 이 프로젝트는 화면 크기 주장을 빌드가 검산한다
         * (VerifyExpRowFits, VerifyBadgeClearsLabel과 같은 계열).
         */
        private static void VerifyPageHeights()
        {
            foreach (var name in PageNames)
            {
                float height = PageHeight(name);
                bool scrolls = height > ViewportHeight;

                // 강화만 넘쳐도 된다
                if (name == EnhancePageName) continue;
                if (!scrolls) continue;

                Debug.LogWarning(string.Format(
                    "[Onikiri] Growth page '{0}' is {1:F0}px but the viewport is {2:F0}px"
                    + " - that tab now scrolls. Only 강화 is meant to.",
                    name, height, ViewportHeight));
            }
        }

        /**
         * @brief 성장 패널을 스크롤 가능하게 만들고 페이지가 들어갈 Content를 돌려준다.
         *
         * 11단계에 여섯 축이 화면 높이에 안 들어가서 넣었다. 17단계의 계열 탭이
         * 한때 그것을 없앴지만, 19단계에 탭을 걷어내고 여섯을 한 목록으로 되돌리면서
         * **강화 페이지는 다시 스크롤한다**(1206px 대 584px). 성장·전직은 들어가고,
         * VerifyPageHeights가 그 둘만 지킨다.
         *
         * RectMask2D를 쓴다 - Mask와 달리 스텐실 버퍼를 쓰지 않아 오버레이 캔버스에서
         * 드로우 콜이 늘지 않고, 사각형으로 자르는 것이 여기서 필요한 전부다.
         */
        private static RectTransform EnsureScroll(Transform panel)
        {
            // 11단계에서 행의 부모가 패널에서 Viewport/Content로 바뀌었다. 그런데
            // 행을 지우는 코드도 함께 옮겨가는 바람에, 10단계까지 패널 바로 아래에
            // 만들어져 있던 행들이 아무도 지우지 않는 채로 남았다.
            //
            // 남은 행은 Viewport보다 앞 형제라 **뒤에 그려진다.** 새 행 사이의
            // 틈으로 옛 수치가 비쳐 보이는 유령 텍스트가 그것이다. 화면에는
            // "글자가 겹쳐 보인다"로만 나타나서 원인을 찾기 어려웠다.
            //
            // 패널의 직속 자식은 Viewport와 탭 줄 둘뿐이어야 한다. 빌더가 단일
            // 출처인 이상 그 외의 것은 전부 이전 세대의 잔재다.
            //
            // 탭 줄은 스크롤 밖에 고정돼야 해서(목록이 움직여도 탭은 제자리)
            // Viewport 안에 넣을 수 없다. 18단계에서 두 줄이 됐다 - 재화(강화/
            // 성장/전직)가 위, 계열(공격/치명타/생존)이 그 아래다
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (System.Array.IndexOf(PanelChildNames, child.name) >= 0) continue;
                Object.DestroyImmediate(child.gameObject);
            }

            var viewport = panel.Find("Viewport");
            if (viewport == null)
            {
                var go = new GameObject("Viewport", typeof(RectTransform));
                go.transform.SetParent(panel, false);
                viewport = go.transform;
            }

            var viewportRect = (RectTransform)viewport;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            // 경험치 스트립과 최상위 탭 줄 몫을 비운다. 계열 탭 줄은 강화
            // 페이지에만 있으므로 여기서 비우면 성장·전직 탭에서 그 자리가 빈
            // 채로 남는다 - 대신 강화 페이지 루트가 자기 몫으로 흡수한다(EnsurePage)
            viewportRect.offsetMax = new Vector2(0f, -(ExpStripStride + TabRowStride));

            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();

            var content = viewport.Find("Content");
            if (content == null)
            {
                var go = new GameObject("Content", typeof(RectTransform));
                go.transform.SetParent(viewport, false);
                content = go.transform;
            }

            var contentRect = (RectTransform)content;
            // 위에서 아래로 자란다. 스크롤은 이 rect를 위아래로 움직인다
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            // 가장 긴 페이지에 맞춘다. 페이지는 한 번에 하나만 켜지므로 합이 아니다 -
            // 합으로 잡으면 어느 페이지를 보든 그 아래로 빈 공간이 스크롤된다
            contentRect.sizeDelta = new Vector2(0f, TallestPageHeight);
            contentRect.anchoredPosition = Vector2.zero;

            var scroll = panel.GetComponent<ScrollRect>();
            if (scroll == null) scroll = panel.gameObject.AddComponent<ScrollRect>();

            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            // Elastic이 기본인데 손을 떼면 튕겨 돌아온다. 목록은 그냥 멈추는 편이
            // 읽기 쉽고, 튕김은 이 화면에서 아무것도 알려주지 않는다
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;

            // 패널 자체가 드래그를 받으려면 레이캐스트 대상이 있어야 한다.
            // 보이지 않는 판을 깔되 색은 완전 투명으로 둔다
            var blocker = panel.GetComponent<Image>();
            if (blocker == null) blocker = panel.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;

            return contentRect;
        }

        /**
         * @brief 강화 곡선을 직렬화된 필드에 기록한다.
         *
         * UpgradeTrack은 MonoBehaviour가 아니라 직렬화 클래스라, 인스턴스를 만들어
         * 대입하는 대신 SerializedProperty로 필드를 하나씩 쓴다.
         */
        private static void WriteTracks(UpgradeSystem system, PlayerCombat combat, PlayerHealth health)
        {
            var so = new SerializedObject(system);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("health").objectReferenceValue = health;

            var tracks = so.FindProperty("tracks");
            // 기존 레벨은 유지한다. 곡선을 손볼 때마다 플레이 진행이 초기화되면
            // 밸런싱을 눈으로 확인할 수가 없다
            int previousCount = tracks.arraySize;
            tracks.arraySize = Specs.Length;

            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                var element = tracks.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("displayName").stringValue = spec.DisplayName;
                element.FindPropertyRelative("maxLevel").intValue = spec.MaxLevel;
                element.FindPropertyRelative("costGrowth").doubleValue = spec.CostGrowth;
                element.FindPropertyRelative("curve").enumValueIndex = (int)spec.Curve;
                element.FindPropertyRelative("step").doubleValue = spec.Step;
                element.FindPropertyRelative("valueCeiling").doubleValue = spec.ValueCeiling;
                element.FindPropertyRelative("display").enumValueIndex = (int)spec.Display;

                SetBigDouble(element.FindPropertyRelative("baseCost"), spec.BaseCost);
                SetBigDouble(element.FindPropertyRelative("baseValue"), spec.BaseValue);

                // 관문(2상 비용, 43단계) - 치명타 확률만 쓴다. 곡선 상수가
                // 단일 출처이고 트랙은 사본이다(UpgradeTrack.CostAtLevel 주석)
                bool wall = spec.Id == UpgradeSystem.CritRateId;
                element.FindPropertyRelative("wallFromLevel").intValue =
                    wall ? CritRateCurve.DeepPhaseLevel - 1 : 0;
                element.FindPropertyRelative("wallJump").doubleValue =
                    wall ? CritRateCurve.WallJump : 1d;
                element.FindPropertyRelative("wallGrowth").doubleValue =
                    wall ? CritRateCurve.DeepGrowth : 1d;

                // 새로 생긴 칸만 레벨 1로 초기화한다
                if (i >= previousCount) element.FindPropertyRelative("level").intValue = 1;
                if (element.FindPropertyRelative("level").intValue < 1)
                    element.FindPropertyRelative("level").intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBigDouble(SerializedProperty property, double value)
        {
            var big = BigDouble.FromDouble(value);
            property.FindPropertyRelative("m").doubleValue = big.Mantissa;
            property.FindPropertyRelative("e").longValue = big.Exponent;
        }

        /**
         * @brief 강화 목록을 위에서 아래로 훑으며 각 조각의 세로 위치를 부른다.
         *
         * 배치와 높이 계산이 **같은 코드를 지난다.** 따로 두면 머리글 여백을 한쪽만
         * 고쳤을 때 Content 크기와 실제 내용이 어긋나고, 화면에서는 "마지막 줄이
         * 안 보인다" 또는 "아래가 텅 빈 채로 스크롤된다"로만 나타난다.
         *
         * @param onHeader  (계열 인덱스, y) - y는 페이지 위에서 아래로 잰 거리
         * @param onRow     (Specs 인덱스, y)
         * @return 목록 전체가 차지하는 높이
         */
        private static float WalkEnhanceList(System.Action<int, float> onHeader,
                                             System.Action<int, float> onRow)
        {
            float y = TopPadding;

            for (int c = 0; c < Categories.Length; c++)
            {
                if (c > 0) y += CategoryGroupGap;

                if (onHeader != null) onHeader(c, y);
                y += CategoryHeaderHeight + CategoryHeaderGap;

                foreach (var trackId in Categories[c].TrackIds)
                {
                    int specIndex = IndexOfSpec(trackId);
                    if (specIndex < 0) continue;

                    if (onRow != null) onRow(specIndex, y);
                    y += RowHeight + RowGap;
                }
            }

            // 마지막 행 뒤에 붙은 RowGap은 아래 여백으로 그대로 둔다. 목록 끝이
            // 스크롤 바닥에 딱 붙으면 더 있는지 없는지가 안 읽힌다
            return y;
        }

        /** 페이지가 실제로 그리는 높이. 스크롤 Content의 크기를 여기서 잡는다 */
        private static float PageHeight(string pageName)
        {
            if (pageName == EnhancePageName) return WalkEnhanceList(null, null);

            // 전직은 잠금 안내와 진화 카드가 **같은 자리에 겹쳐** 서므로 행 수가
            // 아니라 둘 중 큰 쪽(카드)의 높이를 쓴다
            if (pageName == AwakenPageName) return TopPadding + AwakenCardHeight + RowGap;

            int rows = RowsInPage(pageName);
            return TopPadding + rows * (RowHeight + RowGap);
        }

        private static float TallestPageHeight
        {
            get
            {
                float max = 0f;
                foreach (var name in PageNames)
                    if (PageHeight(name) > max) max = PageHeight(name);
                return max;
            }
        }

        /**
         * @brief 성장 패널이 스크롤 없이 담을 수 있는 높이.
         *
         * 화면 10~45%가 성장 패널이고(DisplayConfig), 그 위쪽은 경험치 스트립과
         * 최상위 탭 줄이 가져간다. 남는 것이 페이지가 쓸 수 있는 전부다.
         *
         * 성장(486px)과 전직(328px)은 이 안에 들어간다. 강화는 여섯 축을 한
         * 목록으로 보여주므로 넘치고, 그것이 19단계의 선택이다 - 탭으로 감추는
         * 대신 스크롤한다.
         */
        private static float ViewportHeight
        {
            get
            {
                float panelHeight = Onikiri.Core.DisplayConfig.DesignHeight
                                    * (Onikiri.Core.DisplayConfig.GrowthPanelTop
                                       - Onikiri.Core.DisplayConfig.BottomTabBarTop);
                return panelHeight - ExpStripStride - TabRowStride;
            }
        }

        /** 페이지 루트 하나. 스크롤 영역 맨 위에서 시작해 자기 높이만큼 자란다 */
        private static RectTransform EnsurePage(RectTransform content, string pageName)
        {
            var existing = content.Find(pageName);

            // 이름이 같은 것을 지우고 다시 만든다. 남겨두면 이전 세대의 행이
            // 페이지 안에 섞이는데, 페이지 단위 검사는 그것을 개수로만 잡는다
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(pageName, typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, PageHeight(pageName));
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /**
         * @brief 강화 페이지. 골드 여섯 축을 계열 머리글과 함께 한 목록으로 세운다.
         *
         * 화면(584px)을 넘으므로 스크롤한다 - 19단계에 서브탭을 걷어내면서 고른
         * 쪽이다. 탭은 나머지 넷을 감추는데, 어디에 골드를 쓸지 고르는 화면에서
         * "무엇이 있는지" 자체가 필요한 정보다.
         */
        private static void BuildEnhanceList(RectTransform page, UpgradeSystem system, TMP_FontAsset font)
        {
            WalkEnhanceList(
                (categoryIndex, y) => BuildCategoryHeader(page, font, categoryIndex, y),
                (specIndex, y) => BuildRow(page, system, font, specIndex, y));
        }

        /**
         * @brief 계열 머리글. 얇은 글자 한 줄뿐이고 배경도 버튼도 없다.
         *
         * 버튼을 안 다는 것이 중요하다. 행 아이콘 검사가 버튼 유무로 행과 머리글을
         * 가르므로(VerifyRowIcons), 버튼이 있으면 "아이콘 없는 행"으로 잡힌다.
         * 남은 포인트 머리글에서 이미 한 번 겪은 자리다.
         */
        private static void BuildCategoryHeader(RectTransform page, TMP_FontAsset font,
                                                int categoryIndex, float y)
        {
            string headerName = "Category" + categoryIndex;

            var existing = page.Find(headerName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(headerName, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, CategoryHeaderHeight);
            rect.anchoredPosition = new Vector2(0f, -y);

            var label = CreateLabel(go.transform, font, "Label", TextAlignmentOptions.Left);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 0f);
            labelRect.offsetMax = Vector2.zero;

            label.text = Categories[categoryIndex].DisplayName;

            // 흐린 색이다. 머리글은 훑을 때의 기준점이지 읽을 내용이 아니라서,
            // 행 이름과 같은 밝기면 목록이 아홉 줄로 읽힌다
            label.color = DimColor;
        }

        private static void BuildRow(RectTransform page, UpgradeSystem system, TMP_FontAsset font,
                                     int index, float y)
        {
            string rowName = "Upgrade" + index;

            var existing = page.Find(rowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(rowName, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            // 위에서 아래로 쌓는다. 세로 위치는 목록을 훑는 코드가 정한다
            // (WalkEnhanceList) - 머리글이 섞이므로 인덱스로는 못 잡는다
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -y);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            // 두 줄이다. 위는 이름과 비용, 아래는 이번 구매의 증가폭.
            //
            // 이름은 왼쪽에서 늘어나고 비용은 오른쪽 끝에 고정폭으로 붙는다. 비용은
            // 자릿수가 계속 늘어나므로(10 -> 1.5K -> 3.2M) 오른쪽 정렬이라야 숫자가
            // 자라도 줄 전체가 흔들리지 않는다
            CreateIcon(go.transform, UiIcons.For(Specs[index].Id), UiIcons.Tint);

            // 행의 글자는 전부 캡션 크기다(38단계 위계). 목록은 훑는 화면이고,
            // 큰 글자는 상단 바의 재화·머리글·스탯 창의 최종 값에만 남는다 -
            // 크기가 하나면 색으로만 위계를 만들어야 했다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            UiFonts.Demote(nameLabel);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Right);
            UiFonts.Demote(costLabel);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;

            // 증가폭은 흐린 색으로 둔다. 이름과 비용이 먼저 읽히고, 값은 그 다음에
            // 확인하는 정보다
            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Left);
            UiFonts.Demote(valueLabel);
            PlaceStretched((RectTransform)valueLabel.transform, TextLeft, 24f, 10f + LineHeight, LineHeight);
            valueLabel.color = DimColor;

            var upgradeButton = go.AddComponent<Onikiri.UI.UpgradeButton>();
            var so = new SerializedObject(upgradeButton);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("trackIndex").intValue = index;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;

            // 완성 표현. 상한에 닿은 축이 죽은 회색이 아니라 금색이 된다
            so.FindProperty("rowBackground").objectReferenceValue = image;
            so.FindProperty("masteredColor").colorValue = UiSkin.Gold;
            so.FindProperty("masteredRowTint").colorValue = new Color(0.62f, 0.55f, 0.42f, 1f);
            so.FindProperty("masteredLabel").stringValue = "MASTER";

            // 해금 게이트. 0이면 잠금 없음이라 나머지 여섯 축은 이 줄이 무해하다
            so.FindProperty("unlockStage").intValue = Specs[index].UnlockStage;
            so.FindProperty("lockedColor").colorValue = new Color32(0x5A, 0x51, 0x6B, 0xFF);
            so.FindProperty("lockedRowTint").colorValue = new Color(0.34f, 0.36f, 0.55f, 1f);

            // 심화 게이트(43단계). 잠긴 비용 칸에는 스테이지 대신 조건이 선다
            so.FindProperty("deepGate").boolValue = Specs[index].DeepGate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 탭 줄. 재화로만 가른다 - 강화(골드) / 성장(포인트) / 전직(잠금).
         *
         * **한 줄이다.** 18단계에는 그 아래 계열 줄(공격/치명타/생존)이 하나 더
         * 있었는데 19단계에 걷어냈다.
         *
         * 계열 탭은 강화 페이지를 584px 안에 넣어 스크롤을 없앴지만, 그 대가로
         * 여섯 축 중 **넷을 항상 감췄다.** 골드를 어디에 쓸지 고르는 화면에서는
         * 무엇이 있는지가 곧 필요한 정보라, 세 번 눌러 비교하는 비용이 한 번
         * 훑어 내리는 비용보다 크다.
         *
         * 이제 여섯이 한 목록으로 이어지고(1206px) 화면을 넘는 만큼 스크롤한다.
         * 계열은 목록 안의 얇은 머리글로만 남는다 - 나누되 감추지는 않는다.
         *
         * 탭 줄이 한 줄로 줄면서 층위 문제도 없어졌다. 남은 줄은 "무엇을 쓰는가"
         * 하나뿐이다.
         */
        private static void BuildTopTabs(Transform panel, TMP_FontAsset font,
                                         RectTransform enhancePage, RectTransform growthPage,
                                         RectTransform awakenPage)
        {
            var existing = panel.Find(TopTabBarName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var barObject = new GameObject(TopTabBarName, typeof(RectTransform));
            barObject.transform.SetParent(panel, false);

            var barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(SidePadding, 0f);
            barRect.offsetMax = new Vector2(-SidePadding, 0f);
            barRect.sizeDelta = new Vector2(-SidePadding * 2f, TabBarHeight);
            // 경험치 스트립이 패널 최상단을 가져갔다. 탭 줄은 그 아래다
            barRect.anchoredPosition = new Vector2(0f, -ExpStripStride);

            var pageRoots = new[]
            {
                new[] { enhancePage.gameObject },
                new[] { growthPage.gameObject },
                new[] { awakenPage.gameObject }
            };
            string[] names = { "강화", "성장", "전직" };

            // 남은 포인트 배지는 성장 탭에. 33단계부터 전직 탭에도 자기 배지가
            // 붙는다 - 진화 가능(해금 + 두 재화 충족)할 때만 켜진다
            const int BadgePage = 1;
            const int EvolutionBadgePage = 2;

            var tabs = barObject.AddComponent<Onikiri.UI.GrowthPanelTabs>();
            var tabsSo = new SerializedObject(tabs);
            var array = tabsSo.FindProperty("pages");
            array.arraySize = names.Length;

            float slice = 1f / names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                var tabObject = new GameObject("Tab_" + names[i], typeof(RectTransform));
                tabObject.transform.SetParent(barObject.transform, false);

                var rect = (RectTransform)tabObject.transform;
                rect.anchorMin = new Vector2(i * slice, 0f);
                rect.anchorMax = new Vector2((i + 1) * slice, 1f);
                rect.offsetMin = new Vector2(6f, 0f);
                rect.offsetMax = new Vector2(-6f, 0f);

                var tabImage = tabObject.AddComponent<Image>();
                UiSkin.ApplyPanel(tabImage, UiSkin.Row);

                var tabButton = tabObject.AddComponent<Button>();
                UiSkin.ApplyButton(tabButton, tabImage);

                var label = CreateLabel(tabObject.transform, font, "Label", TextAlignmentOptions.Center);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(0f, -12f);
                labelRect.offsetMax = new Vector2(0f, 12f);
                label.text = names[i];

                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("displayName").stringValue = names[i];
                element.FindPropertyRelative("tab").objectReferenceValue = tabButton;
                element.FindPropertyRelative("tabLabel").objectReferenceValue = label;
                element.FindPropertyRelative("tabBackground").objectReferenceValue = tabImage;

                var roots = element.FindPropertyRelative("roots");
                roots.arraySize = pageRoots[i].Length;
                for (int r = 0; r < pageRoots[i].Length; r++)
                    roots.GetArrayElementAtIndex(r).objectReferenceValue = pageRoots[i][r];

                if (i == BadgePage)
                {
                    TMP_Text badgeLabel;
                    var badge = CreateBadge(tabObject.transform, font, out badgeLabel);
                    element.FindPropertyRelative("badge").objectReferenceValue = badge.gameObject;
                    element.FindPropertyRelative("badgeLabel").objectReferenceValue = badgeLabel;

                    VerifyBadgeClearsLabel(label, TabInnerWidth(names.Length));
                }

                if (i == EvolutionBadgePage)
                {
                    // GrowthPanelTabs의 배지 칸은 남은 포인트 전용이라 쓰지 않는다.
                    // 전직 배지는 자기 판정(EvolutionSystem.AnyAffordable)을 가진
                    // 별도 컴포넌트가 굴린다 - EquipmentTabBadge와 같은 구조다
                    TMP_Text badgeLabel;
                    var badge = CreateBadge(tabObject.transform, font, out badgeLabel);
                    badge.gameObject.SetActive(false);

                    var evolutionBadge = tabObject.AddComponent<Onikiri.UI.EvolutionTabBadge>();
                    var badgeSo = new SerializedObject(evolutionBadge);
                    badgeSo.FindProperty("badge").objectReferenceValue = badge.gameObject;
                    badgeSo.FindProperty("label").objectReferenceValue = badgeLabel;
                    badgeSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            tabsSo.ApplyModifiedPropertiesWithoutUndo();

            // 씬에 저장되는 상태도 기본값(강화)과 맞춰둔다. 런타임에는 Start가
            // 다시 정하지만, 어긋난 채로 저장되면 에디터에서 세 페이지가 겹쳐
            // 보이고 그것이 버그처럼 읽힌다
            enhancePage.gameObject.SetActive(true);
            growthPage.gameObject.SetActive(false);
            awakenPage.gameObject.SetActive(false);
        }

        /** 탭 하나의 안쪽 폭. 탭 줄이 좌우 여백을 뺀 폭을 균등 분할하고 각 탭이 6씩 더 준다 */
        private static float TabInnerWidth(int tabCount)
        {
            float barWidth = Onikiri.Core.DisplayConfig.DesignWidth - SidePadding * 2f;
            return barWidth / tabCount - 12f;
        }

        /** 배지가 탭 이름을 덮지 않는지. 폭은 어림하지 않고 TMP에게 묻는다 */
        private static void VerifyBadgeClearsLabel(TMP_Text label, float tabWidth)
        {
            if (label == null) return;

            // 최악은 가장 긴 탭 이름이다. 지금은 전부 두 글자지만 세 글자짜리가
            // 들어오면(예: "치명타" 165px) 이 여유가 바로 사라진다
            float textWidth = 0f;
            foreach (var name in new[] { "강화", "성장", "전직" })
            {
                float w = label.GetPreferredValues(name, 0f, 0f).x;
                if (w > textWidth) textWidth = w;
            }

            float textRight = (tabWidth + textWidth) * 0.5f;
            float badgeLeft = tabWidth - BadgeWidth + BadgeOverhang;

            if (badgeLeft >= textRight) return;

            Debug.LogWarning(string.Format(
                "[Onikiri] Points badge starts at {0:F0}px but the tab label ends at {1:F0}px"
                + " - the badge covers the tab's own name. Narrow BadgeWidth ({2:F0}) or"
                + " push BadgeOverhang ({3:F0}) further out. 55pt Galmuri is 55px per Hangul"
                + " glyph and 37px per digit - measured, not estimated.",
                badgeLeft, textRight, BadgeWidth, BadgeOverhang));
        }

        /**
         * @brief 남은 포인트 배지. 탭 버튼의 오른쪽 위 모서리에 얹는다.
         *
         * **탭 줄에 붙는 것이 요점이다.** 성장 페이지 안에 두면 그 탭을 연
         * 사람에게만 보이는데, 알려야 할 상대는 정확히 그 탭을 안 여는 사람이다.
         *
         * 붉은 판을 쓴다. 이 화면에서 붉은색은 이미 "네 차례"(레벨업 버튼과 같은
         * 계열)이고, 금색은 완성(MASTER)이라 이미 다른 뜻을 갖고 있다.
         */
        private static Image CreateBadge(Transform parent, TMP_FontAsset font, out TMP_Text label)
        {
            var go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);
            // 모서리에 걸치게 내민다. 탭 안에 얌전히 들어가 있으면 탭 장식으로
            // 읽히고, 걸쳐야 "나중에 붙은 알림"으로 읽힌다
            rect.anchoredPosition = new Vector2(BadgeOverhang, BadgeOverhang);

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Danger);
            image.raycastTarget = false;

            label = CreateLabel(go.transform, font, "Count", TextAlignmentOptions.Center);
            // 배지 숫자는 캡션 크기(38단계). 62px 판에 44pt 숫자는 두 자리부터
            // 삐져나오고 있었다
            UiFonts.Demote(label);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -10f);
            labelRect.offsetMax = new Vector2(0f, 10f);
            label.text = "0";

            return image;
        }

        /**
         * @brief 전직 페이지. **33단계에 실체가 생겼다** - 잠금 안내 + 진화 카드.
         *
         * 12단계부터 여기 서 있던 잠금 안내는 남는다. Lv.30 전에는 그것이 화면의
         * 전부여야 하기 때문이다(값을 보여주면 이미 가진 것으로 읽힌다 -
         * EquipmentRow.DrawLocked과 같은 규칙). 해금되면 EvolutionPanel이 안내를
         * 끄고 카드를 켠다. 둘은 같은 자리에 겹쳐 있고 동시에 켜지지 않는다.
         *
         * pendingLabel("전직 준비 중")은 지웠다 - 그 문구의 존재 이유가 "조건은
         * 넘겼는데 화면이 없다"였고, 이제 화면이 있다. LockedTab.screen에 카드를
         * 넣는 것이 그 증거다(참조가 있어야 구현됐다는 판정이 서는 규칙).
         */
        private static void BuildAwakenPage(RectTransform page, TMP_FontAsset font)
        {
            var notice = EnsureRow(page, "AwakenLocked", 0);

            // 두 줄 높이로 세운다. 한 줄짜리 안내가 텅 빈 페이지 맨 위에 붙어
            // 있으면 "행 하나만 로드된 목록"처럼 보인다
            var rect = (RectTransform)notice;
            rect.sizeDelta = new Vector2(-SidePadding * 2f,
                                         AwakenRowSpan * RowHeight + (AwakenRowSpan - 1) * RowGap);

            // 눌리는 것이 아니다. 버튼이 남아 있으면 실제로 눌리고, 아무 일도
            // 하지 않는 버튼은 "안 눌렸나?"로 읽힌다 - 남은 포인트 머리글에서
            // 한 번 겪은 것과 같다. 행 아이콘 검사도 버튼 유무로 행과 안내를
            // 가르므로, 버튼을 두면 "아이콘 없는 행"으로 잡힌다
            var button = notice.GetComponent<Button>();
            if (button != null) Object.DestroyImmediate(button);

            // 안으로 파인 판. "여기는 아직 잠겨 있다"를 색이 아니라 형태로 말한다
            var image = notice.GetComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Inlay, UiSkin.InlayTint * 0.7f);

            var label = CreateLabel(notice, font, "Label", TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // 빌더가 완성된 문구를 적어둔다. LockedTab.Start는 페이지가 **처음
            // 켜진 다음 프레임**에 돌고, 그때까지 자리표시가 그대로 보인다
            label.text = "전직 Lv." + AwakenRequiredLevel;
            label.color = DimColor;

            var card = BuildEvolutionCard(page, font);

            var locked = notice.gameObject.AddComponent<Onikiri.UI.LockedTab>();
            var so = new SerializedObject(locked);
            so.FindProperty("displayName").stringValue = "전직";
            so.FindProperty("requiredLevel").intValue = AwakenRequiredLevel;
            so.FindProperty("pendingLabel").stringValue = string.Empty;
            so.FindProperty("screen").objectReferenceValue = card.gameObject;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("background").objectReferenceValue = image;
            so.FindProperty("lockedBackground").colorValue = UiSkin.InlayTint * 0.7f;
            so.FindProperty("unlockedBackground").colorValue = UiSkin.InlayTint;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireEvolutionPanel(page, notice.gameObject, card);
        }

        // ------------------------------------------------------------ 33단계: 진화 카드

        /** 카드 안 배치. 위에서부터 초상 / 이름 / 배수 / 버튼 순이다 */
        private const float PortraitTop = 16f;
        private const float PortraitSize = 180f;
        private const float TierNameTop = PortraitTop + PortraitSize + 4f;
        private const float TierStatTop = TierNameTop + LineHeight;
        private const float EvolveButtonTop = TierStatTop + LineHeight * 2f + 12f;
        private const float EvolveButtonHeight = AwakenCardHeight - EvolveButtonTop - 16f;

        /** 초상 반쪽의 좌우 여백. 카드 폭의 절반씩을 현재/다음이 나눠 쓴다 */
        private const float PortraitInset = 90f;

        /**
         * @brief 진화 카드. 현재/다음 티어를 나란히 놓고 아래에 진화 버튼 하나.
         *
         * 초상은 티어 idle 첫 프레임이다 - 아이콘을 따로 그리지 않는 이유는
         * 이 화면의 약속이 "전직하면 **이 모습**이 된다"이기 때문이다. 별도
         * 아이콘은 그 약속과 실물 사이에 한 겹을 더 끼운다.
         */
        private static RectTransform BuildEvolutionCard(RectTransform page, TMP_FontAsset font)
        {
            var card = EnsureRow(page, "EvolutionCard", 0);
            var rect = (RectTransform)card;
            rect.sizeDelta = new Vector2(-SidePadding * 2f, AwakenCardHeight);

            // 카드 자체는 눌리는 것이 아니다. 행 아이콘 검사(버튼 유무)도 피한다
            var cardButton = card.GetComponent<Button>();
            if (cardButton != null) Object.DestroyImmediate(cardButton);

            var image = card.GetComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Inlay, UiSkin.InlayTint);

            float half = (Onikiri.Core.DisplayConfig.DesignWidth - SidePadding * 4f) * 0.5f;

            // 현재 티어 (왼쪽 반)
            BuildPortraitColumn(card, font, "Current", 0f, half);

            // 화살표. 두 초상 사이 - "이것이 저것이 된다"를 기호 하나로 말한다
            var arrow = CreateLabel(card, font, "Arrow", TextAlignmentOptions.Center);
            var arrowRect = (RectTransform)arrow.transform;
            PlaceStretched(arrowRect, half - 40f, half - 40f,
                           PortraitTop + PortraitSize * 0.5f - LineHeight * 0.5f, LineHeight);
            arrow.text = "→";
            arrow.color = DimColor;

            // 다음 티어 (오른쪽 반)
            BuildPortraitColumn(card, font, "Next", half, half);

            // 진화 버튼. 등급업과 같은 청색 - 보석이 드는 버튼의 색이다.
            // 39단계까지 이 버튼만 (0.55, 0.66, 1.00)으로 미세하게 밝았다 -
            // 같은 뜻(보석이 든다)이면 같은 값이어야 한다
            var buttonObject = new GameObject("EvolveButton", typeof(RectTransform));
            buttonObject.transform.SetParent(card, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            PlaceStretched(buttonRect, 120f, 120f, EvolveButtonTop, EvolveButtonHeight);

            var buttonImage = buttonObject.AddComponent<Image>();
            UiSkin.ApplyPanel(buttonImage, UiSkin.GemAction);

            var evolveButton = buttonObject.AddComponent<Button>();
            UiSkin.ApplyButton(evolveButton, buttonImage);

            // 동작은 44pt, 비용은 캡션(39단계 - 장비·동료 버튼과 같은 위계)
            var title = CreateLabel(buttonObject.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceStretched((RectTransform)title.transform, 0f, 0f, 6f, LineHeight);
            title.text = "진화";

            var cost = CreateLabel(buttonObject.transform, font, "Cost", TextAlignmentOptions.Center);
            UiFonts.Demote(cost);
            PlaceStretched((RectTransform)cost.transform, 0f, 0f, 6f + LineHeight, LineHeight);
            cost.text = string.Empty;

            // 카드가 켜진 채로 저장되면 에디터에서 잠금 안내와 겹쳐 보인다.
            // 런타임에는 EvolutionPanel이 상태에 맞게 다시 정한다
            card.gameObject.SetActive(false);

            return rect;
        }

        private static void BuildPortraitColumn(Transform card, TMP_FontAsset font, string prefix,
                                                float left, float width)
        {
            var iconObject = new GameObject(prefix + "Icon", typeof(RectTransform));
            iconObject.transform.SetParent(card, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            iconRect.anchoredPosition = new Vector2(left + width * 0.5f, -PortraitTop);

            var icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // 경지 이름·배수는 캡션 크기(39단계). 이 카드에서 44pt로 남는 것은
            // 진화 버튼의 동사 하나다 - 그림(초상)이 크게 말하고 글자는 받친다
            var name = CreateLabel(card, font, prefix + "Name", TextAlignmentOptions.Center);
            UiFonts.Demote(name);
            PlaceStretchedHalf((RectTransform)name.transform, left, width, TierNameTop, LineHeight);

            var stat = CreateLabel(card, font, prefix + "Stat", TextAlignmentOptions.Center);
            UiFonts.Demote(stat);
            PlaceStretchedHalf((RectTransform)stat.transform, left, width, TierStatTop, LineHeight * 2f);
            stat.text = string.Empty;

            // 배수 두 줄("공격 ×n\n체력 ×n")이 들어온다. NoWrap이 기본이라
            // 명시적으로 줄바꿈을 허용해야 한 줄로 뭉개지지 않는다
            stat.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        /** 카드의 왼쪽/오른쪽 반쪽에 붙인다. PlaceStretched의 반폭 버전 */
        private static void PlaceStretchedHalf(RectTransform rect, float left, float width,
                                               float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        /**
         * @brief EvolutionPanel을 페이지 루트에 앉히고 참조를 배선한다.
         *
         * 초상은 EvolutionAppearance가 구운 티어 idle 첫 프레임에서 가져온다.
         * 그쪽 빌더(Build Evolution Content)를 먼저 돌리지 않았으면 초상이
         * 비는데, 그 상태를 조용히 넘기지 않고 경고한다.
         */
        private static void WireEvolutionPanel(RectTransform page, GameObject notice, RectTransform card)
        {
            var panel = page.GetComponent<Onikiri.UI.EvolutionPanel>();
            if (panel == null) panel = page.gameObject.AddComponent<Onikiri.UI.EvolutionPanel>();

            var so = new SerializedObject(panel);
            so.FindProperty("system").objectReferenceValue =
                Object.FindFirstObjectByType<EvolutionSystem>(FindObjectsInactive.Include);
            so.FindProperty("lockedRoot").objectReferenceValue = notice;
            so.FindProperty("contentRoot").objectReferenceValue = card.gameObject;

            so.FindProperty("currentIcon").objectReferenceValue = FindImage(card, "CurrentIcon");
            so.FindProperty("currentName").objectReferenceValue = FindText(card, "CurrentName");
            so.FindProperty("currentStat").objectReferenceValue = FindText(card, "CurrentStat");
            so.FindProperty("nextIcon").objectReferenceValue = FindImage(card, "NextIcon");
            so.FindProperty("nextName").objectReferenceValue = FindText(card, "NextName");
            so.FindProperty("nextStat").objectReferenceValue = FindText(card, "NextStat");

            var buttonTransform = card.Find("EvolveButton");
            if (buttonTransform != null)
            {
                so.FindProperty("evolveButton").objectReferenceValue = buttonTransform.GetComponent<Button>();
                so.FindProperty("evolveTitle").objectReferenceValue = FindText(buttonTransform, "Title");
                so.FindProperty("evolveCost").objectReferenceValue = FindText(buttonTransform, "Cost");
            }

            var appearance = Object.FindFirstObjectByType<Onikiri.Battle.EvolutionAppearance>(
                FindObjectsInactive.Include);
            var portraits = so.FindProperty("tierPortraits");
            var feet = so.FindProperty("tierPortraitFeet");
            var centers = so.FindProperty("tierPortraitCenter");
            var nudges = so.FindProperty("tierPortraitNudge");

            if (appearance == null || appearance.TierCount == 0)
            {
                portraits.arraySize = 0;
                feet.arraySize = 0;
                centers.arraySize = 0;
                nudges.arraySize = 0;
                Debug.LogWarning("[Onikiri] EvolutionAppearance has no tiers baked"
                                 + " - run Onikiri/Build Evolution Content first, then rebuild this panel.");
            }
            else
            {
                portraits.arraySize = appearance.TierCount;
                feet.arraySize = appearance.TierCount;
                centers.arraySize = appearance.TierCount;
                nudges.arraySize = appearance.TierCount;
                for (int t = 0; t < appearance.TierCount; t++)
                {
                    var frames = appearance.GetTier(t);
                    var portrait = frames != null && frames.idle != null && frames.idle.Length > 0
                        ? frames.idle[0] : null;

                    portraits.GetArrayElementAtIndex(t).objectReferenceValue = portrait;

                    // 발선·가로 중심은 초상 프레임의 그려진 픽셀에서 실측한다.
                    // 스프라이트 피벗은 팩 전체(쓰러지는 DEATH까지)의 최솟값이라
                    // idle의 실제 발보다 낮다 - 검객(팩 2px, idle 13px)이 그
                    // 차이만큼 떠 보였다. 정렬의 기준은 메타데이터가 아니라 픽셀이다
                    var bounds = MeasurePortraitBounds(portrait);
                    feet.GetArrayElementAtIndex(t).floatValue = bounds.x;
                    centers.GetArrayElementAtIndex(t).floatValue = bounds.y;

                    // 마지막 픽셀은 눈으로 잡는다 (39단계) - 팩별 표 참고
                    nudges.GetArrayElementAtIndex(t).vector2Value = PortraitNudgeOf(t);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 팩별 초상 마감 넛지 (원본 아트 픽셀, 39단계).
         *
         * 실측 발선이 못 잡는 마지막 어긋남의 표다. 팩마다 잉크 경계에 섞이는
         * 것이 다르다 - 칼끝이 발보다 낮게 그려진 팩은 실측 발선이 칼끝이 되어
         * 몸이 위로 뜨고, 발밑 그림자가 있는 팩은 반 픽셀 가라앉는다. 값은
         * 실기 캡처를 눈으로 재서 적는다 - 여기 산식을 넣으려는 시도가 곧
         * 실측 발선이었고, 그것이 못 잡는 잔차가 이 표다.
         *
         * 티어 5·6은 같은 팩(Demon_Samurai)이라 같은 값을 쓴다.
         */
        private static Vector2 PortraitNudgeOf(int tier)
        {
            string folder = tier <= 0
                ? Onikiri.Progression.EvolutionCatalog.BaseSpriteFolder
                : Onikiri.Progression.EvolutionCatalog.Tiers[
                      Mathf.Min(tier, Onikiri.Progression.EvolutionCatalog.Count) - 1].SpriteFolder;

            switch (folder)
            {
                // 39단계 실기 캡처 실측 전까지 전부 0. 캡처에서 잰 값을 여기 적는다
                case "FULL_Samurai": return Vector2.zero;
                case "Samurai_3": return Vector2.zero;
                case "Samurai_5": return Vector2.zero;
                case "Samurai_6": return Vector2.zero;
                case "Samurai_2": return Vector2.zero;
                case "Demon_Samurai": return Vector2.zero;
                default: return Vector2.zero;
            }
        }

        /**
         * @brief 초상 스프라이트의 (발선, 가로 중심, 머리선) - 셀 안에서 그려진
         * 픽셀의 최저 줄 비율, 좌우 범위 중앙 비율, 최고 줄 비율(전부 0~1,
         * 아래 기준). 못 재면 (-1, -1, -1) (런타임이 피벗/셀 중앙으로 내려간다).
         *
         * 임포트된 텍스처는 CPU에서 못 읽으므로 원본 PNG를 직접 읽는다 -
         * CharacterSpriteSlicer가 빈 셀을 거를 때와 같은 방법이다.
         *
         * public인 이유: 캐릭터 탭 초상(BattleContentBuilder, 39단계)이 같은
         * 실측을 쓴다 - 측정이 두 벌이면 두 화면의 정렬이 따로 논다.
         */
        public static Vector3 MeasurePortraitBounds(Sprite portrait)
        {
            var missing = new Vector3(-1f, -1f, -1f);
            if (portrait == null || portrait.texture == null) return missing;

            string path = AssetDatabase.GetAssetPath(portrait.texture);
            if (string.IsNullOrEmpty(path)) return missing;

            Texture2D readable = null;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!readable.LoadImage(bytes)) return missing;

                var cell = portrait.rect;
                int x0 = Mathf.Clamp((int)cell.x, 0, readable.width);
                int y0 = Mathf.Clamp((int)cell.y, 0, readable.height);
                int w = Mathf.Clamp((int)cell.width, 0, readable.width - x0);
                int h = Mathf.Clamp((int)cell.height, 0, readable.height - y0);
                if (w <= 0 || h <= 0) return missing;

                var pixels = readable.GetPixels(x0, y0, w, h);

                int lowest = -1, highest = -1, minX = int.MaxValue, maxX = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[y * w + x].a <= 0.03f) continue;
                        if (lowest < 0) lowest = y;   // 아래에서 위로 훑으므로 첫 줄이 발선
                        highest = y;                  // 마지막으로 걸린 줄이 머리선
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }

                if (lowest < 0) return missing;   // 전부 투명 - 잘못 잘린 셀이다

                return new Vector3(lowest / (float)h,
                                   (minX + maxX + 1) * 0.5f / w,
                                   (highest + 1) / (float)h);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Onikiri] Could not measure portrait bounds in " + path + ": " + e.Message);
                return missing;
            }
            finally
            {
                if (readable != null) Object.DestroyImmediate(readable);
            }
        }

        private static Image FindImage(Transform root, string childName)
        {
            var child = root.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            var child = root.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static int IndexOfSpec(string trackId)
        {
            for (int i = 0; i < Specs.Length; i++)
                if (Specs[i].Id == trackId) return i;
            return -1;
        }

        /**
         * @brief 성장 페이지. 남은 포인트 머리글 + 증폭 축 두 줄.
         *
         * 17단계까지 이 구역은 골드 축 **아래에** 붙어 있었다. 처음 켠 플레이어가
         * 포인트 0인 줄을 먼저 보지 않게 하려던 것인데, 부작용으로 어느 계열 탭을
         * 골라도 화면 아래쪽에 포인트 축이 남아 한 화면에 두 재화가 섞였다.
         *
         * 이제 자기 탭을 갖는다. 처음 켠 플레이어가 이 화면을 먼저 보는 문제는
         * 기본 탭이 강화인 것으로 이미 해결되고(GrowthPanelTabs), 배지가 켜지기
         * 전까지는 이 탭을 열 이유 자체가 없다.
         *
         * 두 축뿐이라 스크롤도 서브탭도 필요 없다.
         */
        private static void BuildStatSection(RectTransform page, TMP_FontAsset font)
        {
            var character = Object.FindFirstObjectByType<CharacterLevel>();
            if (character == null)
            {
                Debug.LogError("[Onikiri] CharacterLevel missing - run Build Combat Content first.");
                return;
            }

            var header = EnsureRow(page, "StatHeader", 0);
            var headerLabel = CreateLabel(header, font, "Points", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)headerLabel.transform, 24f, 24f, 10f, LineHeight);
            headerLabel.text = "남은 포인트 0";

            // 머리글은 눌리는 것이 아니므로 행 배경과 버튼을 뺀다. 배경이 있으면
            // 위아래 행과 같은 모양이 되어 눌릴 것처럼 보이고, 버튼이 남아 있으면
            // 실제로 눌린다 - 아무 일도 하지 않는 버튼은 "안 눌렸나?"로 읽힌다.
            //
            // 15단계의 아이콘 검사가 이것을 찾아냈다. 버튼 유무로 행과 머리글을
            // 가르는데 머리글에도 버튼이 있어서 "아이콘 없는 행"으로 잡혔다
            var headerButton = header.GetComponent<Button>();
            if (headerButton != null) Object.DestroyImmediate(headerButton);

            var headerImage = header.GetComponent<Image>();
            if (headerImage != null) Object.DestroyImmediate(headerImage);

            // 남은 포인트 표시는 LevelHud가 갱신한다. 값의 출처가 CharacterLevel
            // 하나뿐이라, 이 라벨만을 위한 컴포넌트를 하나 더 두면 같은 이벤트를
            // 두 곳에서 듣게 된다
            var levelHud = Object.FindFirstObjectByType<Onikiri.UI.LevelHud>();
            if (levelHud != null)
            {
                var hudSo = new SerializedObject(levelHud);
                hudSo.FindProperty("pointsLabel").objectReferenceValue = headerLabel;
                hudSo.ApplyModifiedPropertiesWithoutUndo();
            }

            for (int i = 0; i < StatSpecs.Length; i++)
                BuildStatRow(page, font, i, HeaderRows + i);
        }

        private static void BuildStatRow(RectTransform page, TMP_FontAsset font, int specIndex, int rowIndex)
        {
            var spec = StatSpecs[specIndex];

            var row = EnsureRow(page, "Stat" + specIndex, rowIndex);
            var image = row.GetComponent<Image>();
            var button = row.gameObject.GetComponent<Button>();
            button.targetGraphic = image;

            // 증폭 축은 기본 축과 같은 심볼을 쓰고 밝기로 가른다. UiIcons 참고
            CreateIcon(row, UiIcons.For(spec.AxisId), UiIcons.AmplifierTint);

            // 강화 행과 같은 캡션 크기(38단계 위계)
            var nameLabel = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
            UiFonts.Demote(nameLabel);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);

            var costLabel = CreateLabel(row, font, "Cost", TextAlignmentOptions.Right);
            UiFonts.Demote(costLabel);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;

            var valueLabel = CreateLabel(row, font, "Value", TextAlignmentOptions.Left);
            UiFonts.Demote(valueLabel);
            PlaceStretched((RectTransform)valueLabel.transform, TextLeft, 24f, 10f + LineHeight, LineHeight);
            valueLabel.color = DimColor;

            var stat = row.gameObject.AddComponent<Onikiri.UI.StatPointButton>();
            var so = new SerializedObject(stat);
            so.FindProperty("axisId").stringValue = spec.AxisId;
            so.FindProperty("displayName").stringValue = spec.DisplayName;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 페이지 안의 행 하나. 인덱스가 세로 위치를 결정한다.
         *
         * 인덱스는 **페이지 안에서의 순번**이다. 페이지 루트가 위쪽 여백을 이미
         * 흡수했으므로(EnsurePage), 어느 페이지의 행이든 이 식 하나로 놓인다.
         */
        private static Transform EnsureRow(RectTransform page, string rowName, int index)
        {
            var existing = page.Find(rowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(rowName, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -(TopPadding + index * (RowHeight + RowGap)));

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);
            return go.transform;
        }

        /**
         * @brief 부모 위쪽에 붙여 가로로 늘린다.
         *
         * 위 기준 앵커를 쓰면 offsetMin/Max만으로 위치와 크기가 완전히 결정되어,
         * 부모 폭을 몰라도 "왼쪽 24, 오른쪽 300 비우고, 위에서 10 아래" 를 그대로 적을 수 있다.
         */
        private static void PlaceStretched(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        /** 오른쪽 끝에 고정폭으로 붙인다 */
        private static void PlaceRight(RectTransform rect, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(CostWidth, height);
            rect.anchoredPosition = new Vector2(-right, -top);
        }

        /**
         * @brief 행 왼쪽의 스탯 아이콘.
         *
         * 세로 가운데에 놓는다. 위 정렬하면 두 줄짜리 글자와 무게중심이 어긋나
         * 행이 기울어 보인다.
         *
         * 스프라이트가 없어도 오브젝트는 만든다. 그래야 VerifyWiring이 "행은
         * 있는데 아이콘이 비었다"를 셀 수 있다 - 오브젝트 자체가 없으면 아이콘을
         * 안 붙인 것인지 붙이려다 실패한 것인지 구분되지 않는다.
         */
        private static Image CreateIcon(Transform parent, Sprite sprite, Color tint)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(UiIcons.Size, UiIcons.Size);
            rect.anchoredPosition = new Vector2(IconLeft, 0f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;

            // Simple이다. 아이콘은 늘어나지 않으므로 9-슬라이스가 필요 없고,
            // 늘어난다면 그것이 버그다
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // 스프라이트가 없으면 빈 사각형이 남는다. 그것이 화면에 보이면
            // "아이콘이 빠졌다"가 눈에도 드러난다
            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);

            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            // 아틀라스를 구운 크기와 1:1. 다른 값을 쓰면 비트맵이 리샘플되어 흐려진다
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriSmall;
            label.alignment = alignment;
            label.color = TextColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = name;
            return label;
        }
    }
}
