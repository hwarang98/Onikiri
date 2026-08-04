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
        }

        private static readonly TrackSpec[] Specs =
        {
            // 공격력: 지수. 요괴 체력도 티어마다 지수로 오르므로 같은 형태여야 따라간다.
            // baseValue 5는 PlayerCombat이 지금 쓰는 데미지와 같은 값이다. 레벨 1의
            // 곡선값과 시작 스탯이 어긋나면 첫 구매에서 수치가 튄다
            new TrackSpec {
                Id = UpgradeSystem.AttackPowerId, DisplayName = "공격력 강화",
                BaseCost = 10d, CostGrowth = 1.15d,
                Curve = UpgradeTrack.Curve.Multiplicative, BaseValue = 5d, Step = 1.12d,
                MaxLevel = 0
            },
            // 공격속도: 선형 + 상한. 지수로 두면 몇 십 레벨 만에 프레임당 여러 번
            // 공격하는 값이 되어 축 자체가 무의미해진다.
            // 상한 60에서 1.15 + 0.12*59 = 8.23회/초. 공격 애니메이션 원본 길이(0.5초)가
            // 만들던 초당 2회 상한을 한참 넘으므로, 스윙 압축이 실제로 동작하는지가
            // 이 트랙에서 바로 드러난다
            new TrackSpec {
                Id = UpgradeSystem.AttackSpeedId, DisplayName = "공격속도 강화",
                BaseCost = 25d, CostGrowth = 1.35d,
                Curve = UpgradeTrack.Curve.Additive, BaseValue = 1.15d, Step = 0.12d,
                MaxLevel = 60
            }
        };

        // 1080 폭 캔버스 기준 배치값. 55pt 글자가 들어가야 하므로 줄 높이는 넉넉히 준다
        private const float SidePadding = 48f;

        /** 한 줄의 높이. 55pt 글자에 위아래 여백을 더한 값 */
        private const float LineHeight = 72f;

        /** 이름/비용 한 줄 + 증가폭 한 줄 */
        private const float RowHeight = LineHeight * 2f + 24f;
        private const float RowGap = 24f;
        private const float TopPadding = 24f;

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

            WriteTracks(system, combat);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);
            for (int i = 0; i < Specs.Length; i++)
                BuildRow(panel, system, font, i);

            Debug.Log("[Onikiri] Upgrade panel built: " + Specs.Length + " tracks.");
            return system;
        }

        /**
         * @brief 강화 곡선을 직렬화된 필드에 기록한다.
         *
         * UpgradeTrack은 MonoBehaviour가 아니라 직렬화 클래스라, 인스턴스를 만들어
         * 대입하는 대신 SerializedProperty로 필드를 하나씩 쓴다.
         */
        private static void WriteTracks(UpgradeSystem system, PlayerCombat combat)
        {
            var so = new SerializedObject(system);
            so.FindProperty("combat").objectReferenceValue = combat;

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

        private static void BuildRow(Transform panel, UpgradeSystem system, TMP_FontAsset font, int index)
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
