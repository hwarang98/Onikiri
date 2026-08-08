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

        /** 전직 페이지의 잠금 안내 하나 */
        private const int AwakenRows = 1;

        /**
         * @brief 그 안내가 세로로 차지하는 줄 수.
         *
         * 개수(AwakenRows)와 따로 두는 이유는 안내가 두 줄 높이로 서기 때문이다.
         * 높이 계산에 개수를 그대로 쓰면 페이지가 실제보다 짧다고 보고되고,
         * 스크롤 검사가 넘침을 놓친다
         */
        private const int AwakenRowSpan = 2;

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
        };

        /** 탭 줄 높이와 그 아래 여백 */
        private const float TabBarHeight = 76f;
        private const float TabBarGap = 12f;

        /** 탭 줄이 세로로 먹는 몫. 19단계에 계열 줄이 빠져 이제 한 줄뿐이다 */
        private const float TabRowStride = TabBarHeight + TabBarGap;

        public const string TopTabBarName = "TopTabBar";

        /** 성장 패널 직속에 있어도 되는 것. 그 외는 잔재다 */
        public static readonly string[] PanelChildNames =
        {
            "Viewport", TopTabBarName
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
        private static readonly Color TextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        private static readonly Color DimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

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

        /** 성장 패널을 채우고 UpgradeSystem을 배선한다 */
        public static UpgradeSystem Build()
        {
            EnsureSafeArea();

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
                + "성장 {3} stat axes / 전직 locked, {4} pages under Content.",
                Specs.Length, Categories.Length, PageHeight(EnhancePageName),
                StatSpecs.Length, content.childCount));
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
            // 최상위 탭 줄 **하나만** 비운다. 계열 탭 줄은 강화 페이지에만 있으므로
            // 여기서 비우면 성장·전직 탭에서 그 자리가 빈 채로 남는다 - 대신 강화
            // 페이지 루트가 자기 몫으로 흡수한다(EnsurePage)
            viewportRect.offsetMax = new Vector2(0f, -TabRowStride);

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

            // 전직은 행 하나가 두 줄 높이로 선다
            int rows = pageName == AwakenPageName ? AwakenRowSpan : RowsInPage(pageName);
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
         * 화면 10~45%가 성장 패널이고(DisplayConfig), 그 위쪽 한 줄은 최상위
         * 탭이 가져간다. 남는 것이 페이지가 쓸 수 있는 전부다.
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
                return panelHeight - TabRowStride;
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

            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Right);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;

            // 증가폭은 흐린 색으로 둔다. 이름과 비용이 먼저 읽히고, 값은 그 다음에
            // 확인하는 정보다
            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Left);
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
            so.FindProperty("masteredColor").colorValue = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
            so.FindProperty("masteredRowTint").colorValue = new Color(0.62f, 0.55f, 0.42f, 1f);
            so.FindProperty("masteredLabel").stringValue = "MASTER";

            // 해금 게이트. 0이면 잠금 없음이라 나머지 여섯 축은 이 줄이 무해하다
            so.FindProperty("unlockStage").intValue = Specs[index].UnlockStage;
            so.FindProperty("lockedColor").colorValue = new Color32(0x5A, 0x51, 0x6B, 0xFF);
            so.FindProperty("lockedRowTint").colorValue = new Color(0.34f, 0.36f, 0.55f, 1f);
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
            barRect.anchoredPosition = Vector2.zero;

            var pageRoots = new[]
            {
                new[] { enhancePage.gameObject },
                new[] { growthPage.gameObject },
                new[] { awakenPage.gameObject }
            };
            string[] names = { "강화", "성장", "전직" };

            // 배지는 성장 탭에만. 남은 포인트가 있을 때만 켜진다
            const int BadgePage = 1;

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
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -10f);
            labelRect.offsetMax = new Vector2(0f, 10f);
            label.text = "0";

            return image;
        }

        /**
         * @brief 전직 페이지. 아직 화면이 없으므로 잠금 안내 하나뿐이다.
         *
         * 17단계까지 이 안내는 하단 탭바에 "전직 Lv.30" 버튼으로 따로 서 있었다.
         * 성장 패널에 전직 탭이 생긴 이상 그것은 같은 것이 두 곳에 있는 상태이고,
         * 둘 중 어느 쪽이 진짜 전직 화면인지 알 수 없게 된다. 하단에서 지우고
         * 여기로 옮겼다 - 하단에는 스킬만 남는다.
         *
         * 문구는 LockedTab이 쓴다. 해금 레벨에 닿으면 "전직 Lv.30"이 "전직"으로
         * 바뀌는 규칙이 이미 그 안에 있고, 같은 규칙을 두 번 적을 이유가 없다.
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

            // 안으로 파인 판. "여기는 아직 비어 있다"를 색이 아니라 형태로 말한다.
            // 하단 탭바의 잠긴 탭이 쓰던 것과 같은 판이라, 옮겨온 것이 눈에도 같다
            var image = notice.GetComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Inlay, UiSkin.InlayTint * 0.7f);

            var label = CreateLabel(notice, font, "Label", TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // 빌더가 완성된 문구를 적어둔다. LockedTab.Start는 페이지가 **처음
            // 켜진 다음 프레임**에 도는데, 그때까지 CreateLabel이 넣어둔 자리표시
            // "Label"이 화면에 그대로 보인다 - 탭을 처음 누른 사람이 한 프레임
            // 동안 정확히 그 글자를 본다. 잠긴 쪽 문구를 적어두면 최악이어도
            // 말이 되는 화면이다
            label.text = "전직 Lv." + AwakenRequiredLevel;
            label.color = DimColor;

            var locked = notice.gameObject.AddComponent<Onikiri.UI.LockedTab>();
            var so = new SerializedObject(locked);
            so.FindProperty("displayName").stringValue = "전직";
            so.FindProperty("requiredLevel").intValue = AwakenRequiredLevel;

            // 지금 세이브는 Lv.41이라 조건을 이미 넘겼다. 이 줄이 없으면 밝게
            // 열린 빈 판이 나오고, 그 화면은 "해금됐다"와 "고장났다"가 구분되지
            // 않는다. 전직 화면이 실제로 생기는 단계에서 이 줄을 지우면 된다
            so.FindProperty("pendingLabel").stringValue = "전직 준비 중";
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("background").objectReferenceValue = image;
            so.FindProperty("lockedBackground").colorValue = UiSkin.InlayTint * 0.7f;
            so.FindProperty("unlockedBackground").colorValue = UiSkin.InlayTint;
            so.ApplyModifiedPropertiesWithoutUndo();
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

            var nameLabel = CreateLabel(row, font, "Name", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);

            var costLabel = CreateLabel(row, font, "Cost", TextAlignmentOptions.Right);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;

            var valueLabel = CreateLabel(row, font, "Value", TextAlignmentOptions.Left);
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
