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
            // 체력 회복: 초당 회복량. 가산으로 두면 8단계 공격속도와 같은 이유로
            // 죽으므로 곱연산이다. HealthRegenCurve 참고
            new TrackSpec {
                Id = UpgradeSystem.HealthRegenId, DisplayName = "체력 회복",
                BaseCost = HealthRegenCurve.BaseCost, CostGrowth = HealthRegenCurve.CostGrowth,
                Curve = UpgradeTrack.Curve.Multiplicative,
                BaseValue = HealthRegenCurve.BaseValue, Step = HealthRegenCurve.Step,
                MaxLevel = 0, ValueCeiling = 0d,
                Display = UpgradeTrack.Display.PerSecond
            }
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

        /** 비용 칸의 고정 폭. "1.5K" 정도는 물론 "999.9aa"도 잘리지 않는 크기 */
        private const float CostWidth = 300f;

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
            for (int i = 0; i < Specs.Length; i++)
                BuildRow(content, system, font, i);

            Debug.Log("[Onikiri] Upgrade panel built: " + Specs.Length + " tracks (scrollable).");
            return system;
        }

        /**
         * @brief 성장 패널을 스크롤 가능하게 만들고 행이 들어갈 Content를 돌려준다.
         *
         * 여섯 축이 화면 높이에 물리적으로 들어가지 않는다(LineHeight 주석 참고).
         * 글자를 줄이면 픽셀 폰트가 흐려지므로 목록을 스크롤한다.
         *
         * RectMask2D를 쓴다 - Mask와 달리 스텐실 버퍼를 쓰지 않아 오버레이 캔버스에서
         * 드로우 콜이 늘지 않고, 사각형으로 자르는 것이 여기서 필요한 전부다.
         */
        private static RectTransform EnsureScroll(Transform panel)
        {
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
            viewportRect.offsetMax = Vector2.zero;

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
            contentRect.sizeDelta = new Vector2(0f, TopPadding + Specs.Length * (RowHeight + RowGap));
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

        private static void BuildRow(RectTransform panel, UpgradeSystem system, TMP_FontAsset font, int index)
        {
            string rowName = "Upgrade" + index;

            var existing = panel.Find(rowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(rowName, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            // 위에서 아래로 쌓는다. 성장 축은 위쪽이 눈에 먼저 들어오고, 아래는
            // 탭바가 차지할 자리다
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -(TopPadding + index * (RowHeight + RowGap)));

            var image = go.AddComponent<Image>();
            image.color = RowColor;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // 두 줄이다. 위는 이름과 비용, 아래는 이번 구매의 증가폭.
            //
            // 이름은 왼쪽에서 늘어나고 비용은 오른쪽 끝에 고정폭으로 붙는다. 비용은
            // 자릿수가 계속 늘어나므로(10 -> 1.5K -> 3.2M) 오른쪽 정렬이라야 숫자가
            // 자라도 줄 전체가 흔들리지 않는다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)nameLabel.transform, 24f, CostWidth + 24f, 10f, LineHeight);

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Right);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;

            // 증가폭은 흐린 색으로 둔다. 이름과 비용이 먼저 읽히고, 값은 그 다음에
            // 확인하는 정보다
            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Left);
            PlaceStretched((RectTransform)valueLabel.transform, 24f, 24f, 10f + LineHeight, LineHeight);
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
            so.ApplyModifiedPropertiesWithoutUndo();
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
