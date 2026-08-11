using System.Collections.Generic;
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
     * @brief 발도 오의 화면과 그것을 구동하는 SkillSystem을 세운다.
     *
     * ## 왜 성장 패널의 탭이 아닌가
     *
     * 18단계에 성장 패널의 최상위 탭이 **재화**로 정리됐다(강화=골드 / 성장=포인트
     * / 전직=잠금). 스킬을 그 줄에 얹으면 "이 줄은 캐릭터 성장"이라는 규칙이
     * 깨진다 - 오의는 캐릭터의 스탯이 아니라 별개 시스템이고, 그래서 12단계부터
     * 하단 탭바에 자기 자리를 갖고 있었다.
     *
     * 그 결정이 `BattleContentBuilder.LockedTabs` 주석에 적혀 있고, 이 빌더는
     * 그 자리에 실제 화면을 채운다.
     *
     * ## 성장 패널과 같은 띠를 쓴다
     *
     * 화면 10~45%를 그대로 덮는다. 새 자리를 만들지 않는 이유는 두 화면이
     * **동시에 보일 일이 없기** 때문이다 - 하단 탭이 둘 사이를 오간다. 같은
     * 자리를 쓰면 "아래쪽 절반은 목록"이라는 화면의 문법이 유지된다.
     *
     * 자기 바탕을 깔아야 한다. 뒤의 성장 패널 행이 비쳐 보이면 두 목록이 겹친
     * 것으로 읽힌다 - 11단계의 유령 텍스트와 같은 그림이다.
     */
    public static class SkillPanelBuilder
    {
        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string SlashPrefabPath = PrefabFolder + "/PackSlash.prefab";
        private const string StreakPrefabPath = PrefabFolder + "/DashStreak.prefab";
        private const string AfterimagePrefabPath = PrefabFolder + "/Afterimage.prefab";
        private const string StreakTexturePath = "Assets/_Project/Art/VFX/DashStreak.png";

        /**
         * @brief 참격 팩. 128x128 열 장짜리 시트가 모양 3종 x 색 5종으로 있다.
         *
         * 시트는 640x256 = 가로 5칸 x 세로 2칸이고, 프레임은 왼쪽 위부터
         * 오른쪽으로 읽는다.
         */
        private const string SlashPackFolder = "Assets/ThirdParty/VFX/Slashes/";
        private const int SlashFrameSize = 128;
        private const int SlashSheetColumns = 5;
        private const int SlashSheetRows = 2;

        public const string PanelName = "SkillPanel";

        private const float SidePadding = 48f;
        private const float LineHeight = 62f;
        private const float RowHeight = LineHeight * 2f + 20f;
        private const float RowGap = 14f;
        private const float TopPadding = 12f;
        private const float CostWidth = 300f;
        private const float IconLeft = 24f;
        private const float TextLeft = IconLeft + UiIcons.Size + 20f;

        /** 머리글(제목 + 자동 시전) 한 줄. 행보다 얇다 */
        private const float HeaderHeight = 76f;
        private const float HeaderGap = 14f;

        // 39단계 톤 통일: 자기 색을 갖지 않는다. 팔레트의 단일 출처는 UiSkin이다
        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /**
         * @brief 화면과 시스템을 세우고 SkillSystem을 돌려준다.
         *
         * PlayerCombat이 씬에 있어야 한다 - 오의가 그것을 통해 벤다.
         */
        public static SkillSystem Build()
        {
            var safeArea = UpgradePanelBuilder.EnsureSafeArea();
            if (safeArea == null)
            {
                Debug.LogError("[Onikiri] SafeArea missing - run Rebuild Main Scene first.");
                return null;
            }

            var samurai = GameObject.Find("Samurai");
            var combat = samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
            if (combat == null)
            {
                Debug.LogError("[Onikiri] PlayerCombat missing - run Build Combat Content first.");
                return null;
            }

            var system = samurai.GetComponent<SkillSystem>();
            if (system == null) system = samurai.AddComponent<SkillSystem>();

            var panel = EnsurePanel(safeArea);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            BuildHeader(panel, system, font);
            for (int i = 0; i < SkillCatalog.Count; i++) BuildRow(panel, system, font, i);

            PruneStrays(panel);
            VerifyPanelFits();

            // 씬에는 꺼진 채로 저장된다. 하단 탭이 켠다 - 처음 켠 플레이어가
            // 강화 목록 대신 잠긴 오의 셋을 먼저 보는 화면이 되면 안 된다
            panel.gameObject.SetActive(false);

            // 27단계의 화면 연출. 안무보다 먼저 세워야 참조를 넘길 수 있다
            var nameFlash = BuildNameFlash(safeArea, font);
            var screenFlash = BuildScreenFlash();

            var performer = WirePerformer(samurai, combat, nameFlash, screenFlash);
            WriteSlots(system, combat, performer);

            Debug.Log(string.Format(
                "[Onikiri] Skill panel built: {0} skills, panel {1:F0}px in a {2:F0}px band.",
                SkillCatalog.Count, PanelContentHeight, BandHeight));

            return system;
        }

        // ---------------------------------------------------------------- 화면 연출

        public const string NameFlashName = "SkillNameFlash";
        public const string ScreenFlashName = "ScreenFlash";
        public const string VignettePath = "Assets/_Project/Art/VFX/Vignette.png";

        /**
         * @brief 오의 이름이 뜨는 자리.
         *
         * 전투 영역 밴드 안, 가로 가운데, **사무라이 머리 위**에 둔다.
         *
         * 처음에 지면선 살짝 위(밴드 바닥 +210)에 뒀다가 스크린샷을 보고 올렸다 -
         * 글자가 사무라이 발밑과 지면 띠에 겹쳐서, 이름과 캐릭터가 서로를 가렸다.
         * +460이면 사무라이(키 1u = 화면 약 190px) 위의 빈 하늘에 선다.
         *
         * 상단 바에 두지 않은 이유는 시선이다. 오의가 터지는 곳은 전투 영역이고,
         * 이름이 상단 바에 뜨면 그 둘을 눈이 따로 봐야 한다.
         */
        private static Onikiri.UI.SkillNameFlash BuildNameFlash(Transform safeArea, TMP_FontAsset font)
        {
            var existing = safeArea.Find(NameFlashName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(NameFlashName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, DisplayConfig.GrowthPanelTop);
            rect.anchorMax = new Vector2(0.5f, DisplayConfig.GrowthPanelTop);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(520f, 120f);
            rect.anchoredPosition = new Vector2(0f, 460f);

            // 그림자를 먼저 만든다. 형제 순서가 곧 그리는 순서이므로 뒤에 있어야
            // 라벨에 덮인다 - 순서를 뒤집으면 그림자가 글자를 지운다
            var shadow = CreateFlashLabel(go.transform, font, "Shadow");
            var shadowRect = (RectTransform)shadow.transform;
            // 아트 픽셀 하나만큼 오른쪽 아래로. 데미지 팝업과 같은 규칙이다
            shadowRect.anchoredPosition = new Vector2(
                Onikiri.UI.PixelFontSizes.ArtPixelScale, -Onikiri.UI.PixelFontSizes.ArtPixelScale);
            shadow.color = new Color(0f, 0f, 0f, 0.75f);

            var label = CreateFlashLabel(go.transform, font, "Label");

            var flash = go.AddComponent<Onikiri.UI.SkillNameFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("rect").objectReferenceValue = rect;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 꺼진 채로 저장한다. 켜둔 채 저장하면 에디터에서 오의 이름이 항상
            // 화면에 떠 있고, 그것이 "버그"로 읽힌다
            go.SetActive(false);
            return flash;
        }

        /** 이름 플래시의 라벨 하나. 라벨과 그림자가 같은 크기·정렬이어야 한다 */
        private static TMP_Text CreateFlashLabel(Transform parent, TMP_FontAsset font, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
            // 아틀라스의 정수배. 제목 크기(88)를 쓴다 - 본문(44)이면 이펙트에 묻히고,
            // 그 사이 값은 비트맵이 리샘플되어 흐려진다(PixelFontSizes)
            label.fontSize = Onikiri.UI.PixelFontSizes.GalmuriLarge;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = SkillCatalog.Skills[0].DisplayName;
            return label;
        }

        /**
         * @brief 귀참의 화면 연출. **가장자리가 닫혔다 열린다.**
         *
         * 27단계의 흰 풀스크린을 걷어냈다. 이유는 ScreenFlash 주석에 적어뒀다 -
         * 가운데를 덮으면 무엇이 죽었는지 볼 수 없고, 먹빛 톤에서 흰 화면만
         * 다른 게임처럼 보인다.
         *
         * 두 겹 다 **비네트 텍스처를 쓴다.** 모양이 같아야 "테두리에서 빛이
         * 터지고 그 뒤를 어둠이 조인다"가 한 동작으로 읽힌다 - 하나는 원형이고
         * 하나는 사각형이면 두 사건으로 보인다.
         *
         * **안전 영역이 아니라 캔버스 직속이다.** 노치 옆까지 덮어야 한다 -
         * 화면 일부만 반응하면 그 경계선이 보이고, 경계선이 보이는 순간 연출이
         * 아니라 UI가 된다. 형제 순서의 맨 뒤에 두어 UI 위에도 덮인다.
         */
        private static Onikiri.UI.ScreenFlash BuildScreenFlash()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null) return null;

            var existing = canvas.transform.Find(ScreenFlashName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(ScreenFlashName, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();
            Stretch((RectTransform)go.transform);

            var vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BuildVignetteTexture());

            // 어둠이 먼저(뒤에), 빛이 나중(앞에). 순서를 뒤집으면 어둠이 빛을 덮어
            // 엣지 번쩍이 통째로 사라진다
            var vignette = CreateFullScreenImage(go.transform, "Vignette", vignetteSprite,
                new Color(0f, 0f, 0f, 0f));

            // 붉은 흰빛. 순백으로 두면 먹빛 톤에서 다시 튀고, 순적이면 피격
            // 플래시와 섞인다. 사이의 살굿빛이 "베인 자리에서 튀는 불티"로 읽힌다
            var edge = CreateFullScreenImage(go.transform, "EdgeFlash", vignetteSprite,
                new Color32(0xFF, 0x8A, 0x6A, 0x00));

            var flash = go.AddComponent<Onikiri.UI.ScreenFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("edgeFlash").objectReferenceValue = edge;
            so.FindProperty("vignette").objectReferenceValue = vignette;
            // 0.85로 두면 모서리가 완전히 살굿빛으로 막힌다. 번쩍은 "있었다"만
            // 남기면 되고, 무게는 히트스톱과 셰이크가 낸다
            so.FindProperty("edgePeak").floatValue = 0.6f;
            so.FindProperty("edgeSeconds").floatValue = 0.10f;
            so.FindProperty("vignettePeak").floatValue = 0.88f;
            so.FindProperty("vignetteSeconds").floatValue = 0.34f;
            so.FindProperty("vignetteCloseSeconds").floatValue = 0.05f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return flash;
        }

        private static Image CreateFullScreenImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;

            // 레이캐스트를 먹으면 번쩍이 도는 0.3초 동안 모든 버튼이 죽는다.
            // 화면을 덮는 판에서 가장 흔한 사고다
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /**
         * @brief 비네트 텍스처를 굽는다. 가운데 투명, 가장자리 검정.
         *
         * 코드로 굽는다. 팩 아트를 찾지 않는 이유는 불꽃·꽃잎과 같다 - 필요한 것이
         * 방사형 알파 그라디언트 하나이고, 그 크기에서는 그림 파일을 관리하는
         * 비용이 아트보다 크다.
         *
         * 128px으로 굽고 화면 전체로 늘린다. 비네트는 부드러운 그라디언트라
         * 픽셀 격자를 지킬 이유가 없는 유일한 이펙트다 - 오히려 정수배로 늘리면
         * 계단이 보인다.
         */
        private static string BuildVignetteTexture()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath) != null) return VignettePath;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 중심에서의 정규화 거리. 코너가 1을 넘으므로 코너가 가장 어둡다
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);

                    // **0.80까지 완전 투명, 1.35에서 최대.** 처음에 0.45~1.05로
                    // 잡았는데 그건 비네트가 아니라 화면을 통째로 덮는 판이었다 -
                    // 검은 비네트일 때는 "어둡다"로 넘어갔지만, 같은 모양으로
                    // 살굿빛 엣지 번쩍을 켜자 화면 전체가 살구색이 됐다. 걷어내려던
                    // 흰 풀스크린과 같은 것을 색만 바꿔 다시 만든 셈이다.
                    //
                    // 이 값이면 각 변의 한가운데(r=1.0)가 0.30, 네 귀퉁이(r=1.41)가
                    // 1.0이다. 무게가 모서리에 몰리고 가운데는 그대로 보인다.
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 1.35f, r));

                    // **RGB를 흰색으로 굽는다. 검정이 아니다.**
                    //
                    // 이 스프라이트는 두 장이 나눠 쓴다 - 비네트는 검게, 엣지
                    // 번쩍은 살굿빛으로 틴트한다. 그런데 Image의 색은 스프라이트에
                    // **곱해지므로**, 검게 구우면 살굿빛을 곱해도 검정이다.
                    // 실제로 28단계에 그렇게 구워 두고 "엣지 번쩍"을 넣었는데,
                    // 화면 가장자리 픽셀을 재 보니 밝아지기는커녕 더 어두워졌다
                    // (t=0.017에서 하늘 0.49 -> 0.29, 붉은 쪽으로 치우침 없음).
                    // 흰색으로 구워야 틴트가 그대로 색이 된다.
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            System.IO.File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(VignettePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // 비네트만 필터를 켠다. 그라디언트라 Point로 두면 128px 계단이 화면
            // 전체로 늘어나 띠가 보인다
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            Debug.Log("[Onikiri] Baked vignette " + size + "px -> " + VignettePath);
            return VignettePath;
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief SkillCatalog의 값을 씬 컴포넌트에 옮겨 적는다.
         *
         * 스크립트 기본값에 맡기지 않는 이유는 이 프로젝트의 규칙이다 - 컴포넌트가
         * 이미 씬에 있으면 코드의 기본값을 바꿔도 반영되지 않고, 그러면 빌더가
         * 단일 출처 역할을 못 한다.
         *
         * **레벨은 덮어쓰지 않는다.** 곡선을 손볼 때마다 플레이 진행이 초기화되면
         * 밸런싱을 눈으로 확인할 수가 없다(WriteTracks와 같은 처리).
         */
        private static void WriteSlots(SkillSystem system, PlayerCombat combat, SkillPerformer performer)
        {
            var so = new SerializedObject(system);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("performer").objectReferenceValue = performer;

            var slots = so.FindProperty("slots");
            int previousCount = slots.arraySize;
            slots.arraySize = SkillCatalog.Count;

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                var element = slots.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("displayName").stringValue = spec.DisplayName;
                element.FindPropertyRelative("baseMultiplier").doubleValue = spec.BaseMultiplier;
                element.FindPropertyRelative("cooldownSeconds").floatValue = (float)spec.CooldownSeconds;
                element.FindPropertyRelative("unlockLevel").intValue = spec.UnlockLevel;
                element.FindPropertyRelative("baseCost").doubleValue = spec.BaseCost;
                element.FindPropertyRelative("slashTint").colorValue = RgbaToColor(spec.SlashRgba);

                if (i >= previousCount) element.FindPropertyRelative("level").intValue = 1;
                if (element.FindPropertyRelative("level").intValue < 1)
                    element.FindPropertyRelative("level").intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /** 0xRRGGBBAA -> Color. 밸런스 표에 Color 구조체를 섞지 않기 위한 변환 */
        public static Color RgbaToColor(uint rgba)
        {
            return new Color32(
                (byte)((rgba >> 24) & 0xFF),
                (byte)((rgba >> 16) & 0xFF),
                (byte)((rgba >> 8) & 0xFF),
                (byte)(rgba & 0xFF));
        }

        // ---------------------------------------------------------------- 참격 / 안무

        /**
         * @brief 사무라이 클립. 27단계에 오의마다 고유 모션이 붙었다.
         *
         * 팩에 이미 있는 것을 쓴다. 새 아트를 만들지 않는 것이 이 프로젝트의
         * 규칙이고(불꽃·꽃잎을 코드로 구운 것과 같은 판단), 마침 필요한 세 가지가
         * 전부 있었다 - 연속 베기 셋, 돌진, 큰 피니셔.
         */
        private const string SamuraiSprites = "Assets/ThirdParty/Characters/FULL_Samurai/Sprites/";

        /**
         * @brief 클립별 타격 프레임. **실측값이다.**
         *
         * 23단계의 방법을 그대로 썼다 - 프레임마다 잉크의 오른쪽 끝을 재면 칼이
         * 뻗는 프레임에서만 값이 튄다. 그 프레임이 원화가가 참격을 그려 넣은
         * 자리이고, 타격은 거기서 나야 한다.
         *
         * ```
         * ATTACK 1 (7f)  f4  reach 1.38u  ink 808   <- 타격
         * ATTACK 2 (7f)  f3  reach 1.22u  ink 870   <- 타격
         * ATTACK 3 (6f)  f2  reach 1.34u  ink 749   <- 타격
         * SPECIAL  (14f) f5  reach 1.00u  ink 1140  <- 타격 (f0~4 준비, f6~13 마무리)
         * DASH     (8f)  f1~3 reach 0.72u          <- 자세만, 칼을 뻗지 않는다
         * ```
         *
         * DASH에 타격 프레임이 없는 것이 일섬의 설계를 정했다. 돌진 클립은 베는
         * 그림이 아니므로 **베는 것은 참격 애니가 맡는다** - 사무라이가 돌진 자세로
         * 지나가고 팩 참격이 일렬을 가른다.
         */
        private const int Attack1ImpactFrame = 4;
        private const int Attack2ImpactFrame = 3;
        private const int Attack3ImpactFrame = 2;
        private const int SpecialImpactFrame = 5;

        /**
         * @brief 일섬의 타격 프레임. **일부러 늦다.**
         *
         * 3번(0.115초)이 아니라 6번(0.231초)이다. 거합은 지나가는 것이 먼저고
         * 베이는 것이 나중이라, 지나가는 그 프레임에 숫자가 뜨면 "지나가며 벴다"가
         * 아니라 "부딪쳤다"로 읽힌다. 반 박자 뒤에 요괴들이 한꺼번에 반응해야 한다.
         *
         * DASH는 원래 칼을 뻗지 않는 자세 클립이라 "그려진 타격 프레임"이 없다.
         * 그래서 23단계의 실측 규칙(원화가가 그린 참격 프레임에 맞춘다)이 여기서는
         * 적용되지 않고, 연출 타이밍이 기준이 된다.
         *
         * **타격 횟수는 1회 그대로**이므로 총 데미지와 밴드는 움직이지 않는다.
         */
        private const int DashLateHitFrame = 6;

        /**
         * @brief 참격 프리팹을 굽고 안무를 배선한다.
         *
         * 29단계에 메시 트레일(`SlashTrail`)이 사라지고 **팩 참격 애니**가 들어왔다.
         *
         * 27단계에는 64px 참격 한 장을 3.4배로 키웠고(픽셀이 네모가 됐다),
         * 28단계에는 그 뭉개짐을 고치려고 같은 크기의 메시를 그렸다(뭉개짐은
         * 사라졌지만 여전히 화면을 덮는 덩어리였다). **두 번 다 크기가 원인이었지
         * 표현 방식이 원인이 아니었다.**
         *
         * 그래서 크기를 줄이고 팩으로 돌아온다. 팩 시트는 128x128 열 장짜리 실제
         * 베는 모션이라, 작아도 "벴다"로 읽힌다. 배율은 정수 2 이하로 묶는다.
         */
        private static SkillPerformer WirePerformer(GameObject samurai, PlayerCombat combat,
                                                    Onikiri.UI.SkillNameFlash nameFlash,
                                                    Onikiri.UI.ScreenFlash screenFlash)
        {
            var performer = samurai.GetComponent<SkillPerformer>();
            if (performer == null) performer = samurai.AddComponent<SkillPerformer>();

            var slashPrefab = BuildSlashPrefab();
            var streakPrefab = BuildStreakPrefab();
            var afterimagePrefab = BuildAfterimagePrefab();
            var vfxRoot = GameObject.Find("Battle").transform.Find("VFX");

            var so = new SerializedObject(performer);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("samuraiRenderer").objectReferenceValue = samurai.GetComponent<SpriteRenderer>();
            so.FindProperty("vfxParent").objectReferenceValue = vfxRoot;
            so.FindProperty("slashPrefab").objectReferenceValue = slashPrefab;
            so.FindProperty("streakPrefab").objectReferenceValue = streakPrefab;
            so.FindProperty("afterimagePrefab").objectReferenceValue = afterimagePrefab;
            so.FindProperty("nameFlash").objectReferenceValue = nameFlash;
            so.FindProperty("screenFlash").objectReferenceValue = screenFlash;
            so.FindProperty("slashPrewarm").intValue = 3;
            so.FindProperty("streakPrewarm").intValue = 2;
            so.FindProperty("afterimagePrewarm").intValue = 4;
            // 알파 0.5로 뒀더니 실측에서 첫 장이 0.17까지 내려가 화면에 안 보였다.
            // 0.8이면 가장 옅은 장도 0.36에서 시작한다
            so.FindProperty("afterimageTint").colorValue = new Color(0.78f, 0.86f, 1f, 0.8f);

            // ------------------------------------------------------------ 안무 셋

            var chain = new List<Sprite>();
            var attack1 = OrderedSprites(SamuraiSprites + "ATTACK 1.png");
            var attack2 = OrderedSprites(SamuraiSprites + "ATTACK 2.png");
            var attack3 = OrderedSprites(SamuraiSprites + "ATTACK 3.png");
            chain.AddRange(attack1);
            chain.AddRange(attack2);
            chain.AddRange(attack3);

            // 이어 붙인 클립의 타격 프레임. 각 시트의 실측 타격 프레임에 앞 시트의
            // 길이를 더한다 - 숫자를 손으로 적으면 시트가 한 프레임 늘어나는 날
            // 타격이 조용히 그림에서 떨어진다
            var chainHits = new[]
            {
                Attack1ImpactFrame,
                attack1.Count + Attack2ImpactFrame,
                attack1.Count + attack2.Count + Attack3ImpactFrame
            };

            var dash = OrderedSprites(SamuraiSprites + "DASH.png");
            var special = OrderedSprites(SamuraiSprites + "SPECIAL ATTACK.png");

            var list = so.FindProperty("choreographies");
            list.arraySize = 3;

            // 연참 - 제자리 연속 베기. **참격을 얹지 않는다. 27/28/29단계 모두 그대로다.**
            //
            // 클립에 원화가가 그린 궤적이 세 번 나오므로, 그 위에 무엇을 얹든
            // 23단계가 걷어낸 이중 참격이다. 27단계 플레이 소감에서 연참만 좋다고
            // 나온 이유이기도 하다 - 그린 사람이 그린 참격이 언제나 가장 잘 맞는다.
            var noSlash = new SlashSpec();
            WriteChoreography(list.GetArrayElementAtIndex(0), SkillCatalog.ChainSlashId,
                chain, 40f, chainHits,
                usesSlash: false, slash: noSlash,
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 1.4f, shake: 1.1f, perHitShake: 0.55f,
                numberSize: 1, flash: false);

            // 일섬 - **거합 돌진.** 팩 참격을 쓰지 않는다.
            //
            // ## 왜 팩을 뺐는가
            //
            // 29단계에는 Slash2를 썼는데 그 시트가 직선 돌진과 두 군데서 어긋났다:
            //
            //   굽은 사선     돌진은 직선인데 이펙트가 휘어 경로가 둘로 읽힌다
            //   큰 흰 폭발    맞은 자리를 덮어 무엇이 몇 대 맞았는지 안 보인다
            //
            // 팩에는 얇은 수평 섬광이 없다. 그래서 일섬만 섬광을 구워 쓴다
            // (`DashStreak`). 귀참은 팩 그대로다.
            //
            // 큰 흰 폭발이 사라진 자리는 **23단계 붉은 스파크**가 채운다 -
            // `PlayerCombat.DeliverSkillHit`가 맞은 요괴마다 이미 하나씩 낸다.
            // 오의 전용 폭발을 따로 두지 않는 것이 원래 규칙이었다.
            //
            // ## 거합 타이밍
            //
            //   0.00~0.09  돌진. 섬광이 칼끝을 따라 자란다. 아직 아무도 안 베인다
            //   0.09~0.30  복귀. 섬광이 잠시 머물다 옅어진다
            //   0.231      **타격.** 지나가고 반 박자 뒤에 요괴들이 한꺼번에
            //              번쩍이고 숫자가 뜬다. 히트스톱도 여기서 짧게
            //
            // 타격을 DASH 3번(0.115초) -> 6번(0.231초) 프레임으로 옮긴 것이 전부다.
            // **타격 횟수는 그대로 1회**이므로 총 데미지와 밴드가 움직이지 않는다.
            WriteChoreography(list.GetArrayElementAtIndex(1), SkillCatalog.FlashId,
                dash, 26f, new[] { DashLateHitFrame },
                usesSlash: false, slash: new SlashSpec(),
                pierceRange: 4.6f, pierceHeight: 2.6f,
                // **돌진을 1.9 -> 4.2u로 늘렸다.** 요괴 열이 대략 x 0.8~3.7에 서므로
                // 1.9로는 그 앞에서 멈춘다 - "적 사이로 지나간다"가 되려면 통과해야
                // 한다. 앵커(-1.2)에서 4.2면 3.0까지 나간다.
                //
                // 복귀 0.21초는 클립 길이(8f/26fps = 0.308초)에 맞춘 값이다.
                // 0.09+0.21 = 0.30 <= 0.308 - 넘기면 클립이 먼저 끝나 시전이
                // 해제되고, LungeOffsetX가 0으로 튀면서 사무라이가 순간이동한다
                lunge: 4.2f, lungeOut: 0.09f, lungeBack: 0.21f, afterimages: 3,
                // 히트스톱을 1.9 -> 1.2로 줄였다. 거합의 무게는 긴 정지가 아니라
                // **늦게 오는 타격**이 낸다
                hitStop: 1.2f, shake: 1.9f, perHitShake: 0f,
                numberSize: 2, flash: false,
                streak: new StreakSpec
                {
                    used = true,
                    thickness = 0.18f,      // 얇게. 굵으면 다시 덩어리다
                    height = -0.42f,        // 요괴 몸통 (지상 -0.16~0.71 / 도깨비불 0.19~1.03)
                    color = Color.white,    // 색은 텍스처에 구워져 있다 (흰 심 + 붉은 옆면)
                    reveal = 0.09f,         // 돌진이 나가는 시간과 같아야 머리가 칼끝에 붙는다
                    hold = 0.08f,
                    fade = 0.10f
                });

            // 귀참 - 화면 광역. 가장 무거운 한 방.
            // 팩 Slash3(큰 초승달) color2(흑적 - 검은 심 + 붉은 테두리).
            //
            // **Slash1이 아니라 Slash3을 골랐다.** 사양에서 예로 든 Slash1의 ">" 호는
            // 7~8번 프레임이 큰 단색 부채꼴이라, 크기를 줄여도 화면에 뜨는 것이
            // 궤적이 아니라 면이다 - 피하려던 "덩어리"가 작아진 채로 그대로 온다.
            // Slash3은 열 프레임 내내 초승달의 두께만 변해서 **베고 지나간 자국**으로
            // 읽힌다. 색은 사양대로 color2다 - 검은 심 덕분에 밝은 벚꽃 배경에서도,
            // 붉은 테두리 덕분에 밤 배경에서도 형태가 남는다.
            //
            // 배율 2는 셋 중 가장 크지만 원본 픽셀의 두 배다. 128px 프레임이
            // 32 PPU에서 4u이므로 2배면 8u 칸이고, 그 안에 실제로 그려진 초승달은
            // 약 5u다 - **사무라이 키(3u)의 1.7배**, 화면 폭(8.03u)의 62%.
            // 화면을 덮지 않는다.
            //
            // 화면 전체 타격은 그림이 아니라 `DeliverAll`이 한다. 이펙트가 맞는
            // 것들을 물리적으로 덮을 필요가 없다는 것이 이번 정정의 핵심이다.
            WriteChoreography(list.GetArrayElementAtIndex(2), SkillCatalog.OniCleaveId,
                special, 24f, new[] { SpecialImpactFrame },
                usesSlash: true,
                slash: new SlashSpec
                {
                    frames = SliceSlashSheet("Slash3", 2),
                    frameRate = 24f,        // SPECIAL 클립(14f/24fps = 0.58초) 안에서 끝난다
                    scale = 2f,
                    // 원본 초승달은 "위로 볼록"이다. -90도면 "오른쪽으로 볼록"인
                    // 곧은 세로 호가 되는데, 그러면 5.0u가 통째로 세로로 서서
                    // 전투 띠(약 3.5u)를 위아래로 넘친다. -55도로 눕히면 세로
                    // 폭이 5.0*cos35 = 4.1u로 줄고, 모양도 "가로막는 벽"이 아니라
                    // **비스듬히 내려긋는 베기**가 된다
                    angle = -55f,
                    // **앵커는 호의 곡률 중심이지 그림의 자리가 아니다.**
                    //
                    // 피벗을 절정 프레임의 잉크 경계 한가운데로 잡았는데, 초승달은
                    // 굽은 선이라 그 경계 한가운데가 **오목한 안쪽의 빈 곳**이다.
                    // 실측으로 앵커를 0.92에 두자 붉은 호는 2.25에 떴다 - 1.3u
                    // 차이가 곧 호의 반지름이다.
                    //
                    // 그래서 앵커를 사무라이 바로 앞에 두고 호가 앞으로 부풀게
                    // 한다. 칼을 쥔 손이 중심이고 궤적이 뻗어 나가는 모양이라,
                    // 우연히도 이쪽이 실제 베기와 더 닮았다.
                    forward = 0.8f, height = 0.15f
                },
                pierceRange: 0f, pierceHeight: 0f,
                lunge: 0f, lungeOut: 0f, lungeBack: 0f, afterimages: 0,
                hitStop: 3.0f, shake: 2.8f, perHitShake: 0f,
                numberSize: 2, flash: true);

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format(
                "[Onikiri] Skill choreography: 연참 chain {0}f hits [{1}] (참격 없음) / "
                + "일섬 DASH {2}f + 돌진 섬광 4.2u (타격 f6) / 귀참 SPECIAL {3}f + Slash3_color2 x2.",
                chain.Count, string.Join(",", System.Array.ConvertAll(chainHits, h => h.ToString())),
                dash.Count, special.Count));

            return performer;
        }

        /** 참격 값 묶음. 인자 목록이 스물을 넘어가 읽을 수 없어서 뽑았다 */
        private struct SlashSpec
        {
            public Sprite[] frames;
            public float frameRate;
            public float scale;
            public float angle;
            public float forward;
            public float height;
        }

        /** 돌진 섬광 값 묶음. 일섬만 쓴다 */
        private struct StreakSpec
        {
            public bool used;
            public float thickness;
            public float height;
            public Color color;
            public float reveal;
            public float hold;
            public float fade;
        }

        private static void WriteChoreography(SerializedProperty element, string id,
                                              List<Sprite> clip, float frameRate, int[] hitFrames,
                                              bool usesSlash, SlashSpec slash,
                                              float pierceRange, float pierceHeight,
                                              float lunge, float lungeOut, float lungeBack,
                                              int afterimages,
                                              float hitStop, float shake, float perHitShake,
                                              int numberSize, bool flash,
                                              StreakSpec streak = default(StreakSpec))
        {
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("clipFrameRate").floatValue = frameRate;
            AssignSprites(element.FindPropertyRelative("clip"), clip);

            var hits = element.FindPropertyRelative("hitFrames");
            hits.arraySize = hitFrames.Length;
            for (int i = 0; i < hitFrames.Length; i++)
                hits.GetArrayElementAtIndex(i).intValue = hitFrames[i];

            element.FindPropertyRelative("usesSlash").boolValue = usesSlash;

            var slashFrames = element.FindPropertyRelative("slashFrames");
            int frameCount = slash.frames != null ? slash.frames.Length : 0;
            slashFrames.arraySize = frameCount;
            for (int i = 0; i < frameCount; i++)
                slashFrames.GetArrayElementAtIndex(i).objectReferenceValue = slash.frames[i];

            element.FindPropertyRelative("slashFrameRate").floatValue = slash.frameRate;
            element.FindPropertyRelative("slashScale").floatValue = slash.scale;
            element.FindPropertyRelative("slashAngle").floatValue = slash.angle;
            element.FindPropertyRelative("slashForwardOffset").floatValue = slash.forward;
            element.FindPropertyRelative("slashHeightOffset").floatValue = slash.height;

            element.FindPropertyRelative("usesStreak").boolValue = streak.used;
            element.FindPropertyRelative("streakThickness").floatValue = streak.thickness;
            element.FindPropertyRelative("streakHeightOffset").floatValue = streak.height;
            element.FindPropertyRelative("streakColor").colorValue = streak.color;
            element.FindPropertyRelative("streakRevealSeconds").floatValue = streak.reveal;
            element.FindPropertyRelative("streakHoldSeconds").floatValue = streak.hold;
            element.FindPropertyRelative("streakFadeSeconds").floatValue = streak.fade;
            element.FindPropertyRelative("pierceRange").floatValue = pierceRange;
            element.FindPropertyRelative("pierceHeight").floatValue = pierceHeight;
            element.FindPropertyRelative("lungeDistance").floatValue = lunge;
            element.FindPropertyRelative("lungeOutSeconds").floatValue = lungeOut;
            element.FindPropertyRelative("lungeBackSeconds").floatValue = lungeBack;
            element.FindPropertyRelative("afterimageCount").intValue = afterimages;
            element.FindPropertyRelative("hitStopMultiplier").floatValue = hitStop;
            element.FindPropertyRelative("shakeMultiplier").floatValue = shake;
            element.FindPropertyRelative("perHitShakeMultiplier").floatValue = perHitShake;
            element.FindPropertyRelative("numberSizeMultiple").intValue = numberSize;
            element.FindPropertyRelative("screenFlash").boolValue = flash;
        }

        private static void AssignSprites(SerializedProperty array, List<Sprite> sprites)
        {
            array.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static List<Sprite> OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return sprites;
        }

        /**
         * @brief 참격 프리팹. 스프라이트 렌더러 하나뿐이다.
         *
         * 28단계의 메시 프리팹(서브메시 둘 + 전용 셰이더 + 재료 둘)이 통째로
         * 사라진 자리다. 팩 애니를 재생하는 데는 렌더러 하나면 된다.
         */
        private static PackSlash BuildSlashPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefabPath);
            if (existing == null)
            {
                var root = new GameObject("PackSlash");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Vfx;
                root.AddComponent<PackSlash>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, SlashPrefabPath);
                Object.DestroyImmediate(root);
            }

            var slash = existing.GetComponent<PackSlash>();

            var so = new SerializedObject(slash);
            so.FindProperty("spriteRenderer").objectReferenceValue = existing.GetComponent<SpriteRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return slash;
        }

        /**
         * @brief 팩 시트 한 장을 열 프레임으로 자르고 순서대로 돌려준다.
         *
         * 팩은 Single 스프라이트로 임포트되어 있어서 그대로는 정적인 한 장이다.
         * **여기서 잘라야 애니가 된다** - 자르지 않고 쓰는 것이 27단계에 한 장을
         * 키워 쓴 실수의 출발점이었다.
         *
         * 프레임 순서는 왼쪽 위 -> 오른쪽, 그다음 아랫줄이다. Unity 텍스처는
         * 좌하단이 원점이라 **행을 뒤집어야** 시트에 그려진 순서와 맞는다.
         *
         * 이름을 `Slash3_color2_00` 처럼 0을 채워 붙이는 이유는 정렬 때문이다 -
         * `_1`, `_10`, `_2` 순으로 읽히면 애니가 뒤섞인다.
         *
         * 45b에 요도 빌더에게도 열었다. 영체가 같은 팩 시트를 쓰는데
         * (YodoPanelBuilder의 시그니처 연출) 자르는 코드를 한 벌 더 두면
         * 피벗 계산이 두 곳에 살고, 두 곳이 갈리는 날 참격이 영체 옆에서
         * 어긋난다. 같은 시트를 두 번 자르는 것도 아니다 - 임포터 설정을
         * 고치는 일이라 두 번째부터는 그대로 읽는다.
         */
        public static Sprite[] SliceSlashSheet(string shape, int color)
        {
            string path = SlashPackFolder + "Slash_128x128_" + shape + "_color" + color + ".png";

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Slash pack sheet missing: " + path);
                return new Sprite[0];
            }

            int frames = SlashSheetColumns * SlashSheetRows;
            string prefix = shape + "_color" + color + "_";

            // 잉크가 실제로 놓인 자리를 재서 피벗을 잡는다. 아래 주석 참고
            var pivot = ContentPivot(path, importer);

            var sheet = new SpriteMetaData[frames];
            for (int i = 0; i < frames; i++)
            {
                int column = i % SlashSheetColumns;
                int row = i / SlashSheetColumns;

                sheet[i] = new SpriteMetaData
                {
                    name = prefix + i.ToString("00"),
                    // 행 뒤집기. 위 주석 참고
                    rect = new Rect(column * SlashFrameSize,
                                    (SlashSheetRows - 1 - row) * SlashFrameSize,
                                    SlashFrameSize, SlashFrameSize),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot
                };
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = sheet;
            // 게임 아트와 같은 격자에 얹혀야 톤이 맞는다. 32 PPU에서
            // 128px 프레임 = 4 월드 단위다
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var ordered = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite != null) ordered.Add(sprite);
            }
            ordered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (ordered.Count != frames)
                Debug.LogWarning("[Onikiri] " + shape + "_color" + color + " sliced into "
                                 + ordered.Count + " frames, expected " + frames);

            return ordered.ToArray();
        }

        /**
         * @brief 피벗 = **잉크가 가장 많은 프레임**의 잉크 한가운데.
         *
         * ## 왜 프레임 중심(0.5, 0.5)을 못 쓰는가
         *
         * 팩 프레임 안에서 그림은 가운데 있지 않다. 피벗을 프레임 중심에 두고
         * -90도 돌리면 **회전축과 그림 사이의 거리만큼 그림이 날아간다.** 실측으로
         * 참격이 앵커(0.40, 0.97) 대신 화면 오른쪽 끝으로 밀려 잘렸고, 배율 2배가
         * 그 어긋남까지 2배로 키웠다.
         *
         * ## 왜 열 장의 합집합도 못 쓰는가
         *
         * 처음에 그렇게 했다. 열 장을 겹치면 프레임을 거의 다 덮어서 결과가
         * (65, 69.5)/128 - **프레임 중심과 사실상 같았고, 아무것도 고쳐지지
         * 않았다.** 합집합은 "어디에 그려졌나"가 아니라 "어디까지 갔나"를 잰다.
         *
         * ## 왜 프레임마다 따로 구하지 않는가
         *
         * 팩은 **프레임 안에서 그림을 옮기고 키우면서** 애니메이션한다. 프레임마다
         * 제 잉크 중심으로 피벗을 잡으면 그 움직임이 통째로 상쇄되어, 참격이
         * 지나가지 않고 제자리에서 커졌다 작아진다.
         *
         * 그래서 **한 프레임을 대표로 뽑아 그 중심을 열 장이 공유한다.** 잉크가
         * 가장 많은 프레임이 곧 플레이어가 "참격"으로 보는 그림이므로, 그것을
         * 앵커에 맞추면 나머지는 그 주위에서 자란다.
         *
         * 읽기를 잠깐 켰다 되돌린다 - 픽셀을 읽으려면 필요하고, 켠 채로 두면
         * 빌드에 텍스처 사본이 하나 더 들어간다.
         */
        private static Vector2 ContentPivot(string path, TextureImporter importer)
        {
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels();
            int width = texture.width;

            int frames = SlashSheetColumns * SlashSheetRows;
            var counts = new int[frames];
            var minX = new int[frames]; var maxX = new int[frames];
            var minY = new int[frames]; var maxY = new int[frames];
            for (int i = 0; i < frames; i++)
            {
                minX[i] = int.MaxValue; minY[i] = int.MaxValue;
                maxX[i] = int.MinValue; maxY[i] = int.MinValue;
            }

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 0.004f) continue;

                    int column = x / SlashFrameSize;
                    // 텍스처는 좌하단 원점이라 위 줄이 row 0이다
                    int row = SlashSheetRows - 1 - (y / SlashFrameSize);
                    int frame = row * SlashSheetColumns + column;
                    if (frame < 0 || frame >= frames) continue;

                    int localX = x % SlashFrameSize;
                    int localY = y % SlashFrameSize;

                    counts[frame]++;
                    if (localX < minX[frame]) minX[frame] = localX;
                    if (localX > maxX[frame]) maxX[frame] = localX;
                    if (localY < minY[frame]) minY[frame] = localY;
                    if (localY > maxY[frame]) maxY[frame] = localY;
                }
            }

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            int peak = 0;
            for (int i = 1; i < frames; i++)
                if (counts[i] > counts[peak]) peak = i;

            if (counts[peak] == 0) return new Vector2(0.5f, 0.5f);

            var pivot = new Vector2(
                (minX[peak] + maxX[peak] + 1) * 0.5f / SlashFrameSize,
                (minY[peak] + maxY[peak] + 1) * 0.5f / SlashFrameSize);

            Debug.Log(string.Format(
                "[Onikiri] {0}: peak frame {1} ({2}px ink) spans {3}..{4} x {5}..{6} "
                + "-> pivot ({7:0.000}, {8:0.000}), ink {9}x{10}px",
                System.IO.Path.GetFileNameWithoutExtension(path),
                peak, counts[peak], minX[peak], maxX[peak], minY[peak], maxY[peak],
                pivot.x, pivot.y,
                maxX[peak] - minX[peak] + 1, maxY[peak] - minY[peak] + 1));

            return pivot;
        }

        /**
         * @brief 돌진 섬광 프리팹 + 그 텍스처.
         *
         * 팩에 얇은 수평 섬광이 없어서 이것만 굽는다. 귀참은 팩 그대로다.
         */
        private static DashStreak BuildStreakPrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BuildStreakTexture());

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StreakPrefabPath);
            if (existing == null)
            {
                var root = new GameObject("DashStreak");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Vfx;
                root.AddComponent<DashStreak>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, StreakPrefabPath);
                Object.DestroyImmediate(root);
            }

            var streak = existing.GetComponent<DashStreak>();
            var spriteRenderer = existing.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = SortingOrders.Vfx;

            var so = new SerializedObject(streak);
            so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return streak;
        }

        /**
         * @brief 섬광 텍스처를 굽는다. 흰 심 + 붉은 옆면.
         *
         * ## 왜 색을 틴트로 주지 않는가
         *
         * 사양이 "흰/적"인데 Image·SpriteRenderer의 색은 **한 겹**이라 틴트
         * 하나로는 심과 옆면을 다르게 만들 수 없다. 28단계에 비네트를 검게 구워
         * 두고 살굿빛 틴트를 씌웠다가 검정 x 주황 = 검정이 나온 것과 같은 자리다.
         * 그래서 RGB를 텍스처에 굽고 틴트는 흰색으로 둔다.
         *
         * ## 왜 Bilinear인가
         *
         * 이 스프라이트만 픽셀 아트가 아니라 **그라디언트**다. 길이에 맞춰 가로로
         * 늘여 쓰므로 Point로 두면 늘어난 만큼 계단이 커진다. 게임 아트가 아니라
         * 빛이므로 격자에 얹힐 이유도 없다(비네트와 같은 판단).
         */
        private static string BuildStreakTexture()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StreakTexturePath) != null) return StreakTexturePath;

            const int width = 256;
            const int height = 16;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                // 가운데에서의 거리. 0이 심, 1이 위아래 끝
                float d = Mathf.Abs((y + 0.5f) / height * 2f - 1f);

                // 심은 희고 옆면으로 갈수록 붉다. 얇은 선에서 색을 읽히게 하는
                // 유일한 방법이다 - 29단계 일섬에서 배웠다(얇은 것에 테두리로
                // 색을 입힐 수는 없다)
                var rgb = Color.Lerp(Color.white, new Color(1f, 0.20f, 0.12f),
                                     Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, d)));

                // **가운데 45%는 알파 1로 꽉 채운다.** 처음에 (1-d)^1.6으로
                // 부드럽게 떨어뜨렸더니 화면에 회색 실선 하나가 지나가는 것으로
                // 보였다 - 두께가 0.12u(약 19픽셀)뿐인데 그중 불투명한 것은
                // 가운데 몇 줄이라, 얇은 것을 더 얇게 만든 셈이었다.
                // 얇은 선은 **또렷해야** 보인다.
                float across = d <= 0.45f
                    ? 1f
                    : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.45f, 1f, d));

                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;

                    // 꼬리(0쪽)는 길게 옅어지고 머리(1쪽)는 짧게 끊긴다.
                    // 반대로 두면 날아가는 것으로 보인다
                    float tail = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.55f, u));
                    float head = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.94f, u));

                    var color = rgb;
                    color.a = across * tail * head;
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            EnsureFolder("Assets/_Project/Art/VFX");
            System.IO.File.WriteAllBytes(StreakTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(StreakTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(StreakTexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Bilinear;   // 위 주석 참고
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log("[Onikiri] Baked dash streak " + width + "x" + height + " -> " + StreakTexturePath);
            return StreakTexturePath;
        }

        /**
         * @brief 돌진 잔상 프리팹. 스프라이트 렌더러 하나뿐이다.
         *
         * 스프라이트는 런타임에 사무라이의 현재 프레임을 복사해 넣는다 - 새 아트를
         * 만들지 않는다는 규칙 그대로다.
         */
        private static Afterimage BuildAfterimagePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AfterimagePrefabPath);
            if (existing == null)
            {
                var root = new GameObject("Afterimage");
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = SortingOrders.Player - 1;
                root.AddComponent<Afterimage>();

                existing = PrefabUtility.SaveAsPrefabAsset(root, AfterimagePrefabPath);
                Object.DestroyImmediate(root);
            }

            var ghost = existing.GetComponent<Afterimage>();

            var so = new SerializedObject(ghost);
            so.FindProperty("spriteRenderer").objectReferenceValue = existing.GetComponent<SpriteRenderer>();
            so.FindProperty("lifetime").floatValue = 0.16f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);

            return ghost;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }


        // ---------------------------------------------------------------- 화면

        private static float BandHeight
        {
            get
            {
                return DisplayConfig.DesignHeight
                       * (DisplayConfig.GrowthPanelTop - DisplayConfig.BottomTabBarTop);
            }
        }

        private static float PanelContentHeight
        {
            get
            {
                return TopPadding + HeaderHeight + HeaderGap
                       + SkillCatalog.Count * (RowHeight + RowGap);
            }
        }

        /**
         * @brief 목록이 띠 안에 들어가는지 빌드가 검산한다.
         *
         * 이 화면에는 스크롤을 두지 않았다. 셋뿐이라 필요가 없고, 스크롤이 있으면
         * "더 있나?" 하고 끌어보게 된다 - 없는 것을 찾게 만드는 UI다. 대신 넷째
         * 오의가 생기는 순간 여기서 걸린다.
         *
         * 어림하지 않고 빌더가 쓰는 상수로 계산한다. 17~18단계에서 글자 폭 어림이
         * 세 번 틀린 뒤로 이 프로젝트는 화면 크기 주장을 빌드가 검산한다.
         */
        private static void VerifyPanelFits()
        {
            if (PanelContentHeight <= BandHeight) return;

            Debug.LogWarning(string.Format(
                "[Onikiri] Skill panel needs {0:F0}px but the band is {1:F0}px - the last row is "
                + "cut off. Either add a ScrollRect (and accept that the list looks longer than "
                + "it is) or drop a skill. {2} skills x {3:F0}px + header {4:F0}px.",
                PanelContentHeight, BandHeight, SkillCatalog.Count, RowHeight + RowGap,
                HeaderHeight + HeaderGap));
        }

        private static RectTransform EnsurePanel(Transform safeArea)
        {
            var existing = safeArea.Find(PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(PanelName, typeof(RectTransform));
            go.transform.SetParent(safeArea, false);

            // 성장 패널과 같은 띠. 하단 탭바 위, 전투 영역 아래
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, DisplayConfig.BottomTabBarTop);
            rect.anchorMax = new Vector2(1f, DisplayConfig.GrowthPanelTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 자기 바탕. 뒤의 성장 패널 행이 비쳐 보이면 두 목록이 겹친 것으로
            // 읽힌다. 하단 UI 바탕과 같은 먹빛이라 화면의 언어가 하나로 남는다
            var backdrop = go.AddComponent<Image>();
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTextureBuilder.WashiPath);
            backdrop.type = Image.Type.Tiled;
            backdrop.color = UiSkin.PanelInk;

            // 뒤의 성장 패널이 드래그를 받지 않게 막는다. 이 판이 레이캐스트를
            // 먹지 않으면 스킬 화면 위에서 끈 손가락이 뒤의 강화 목록을 스크롤한다
            backdrop.raycastTarget = true;

            // 화지 위의 벚가지 (39단계). 행이 덮는 부분은 안 보이고, 여백에만 남는다
            BackdropTextureBuilder.AddSakuraBranch(rect);

            return rect;
        }

        /** 빌더가 만들지 않은 자식은 이전 세대의 잔재다. 11단계 유령 텍스트와 같은 종류 */
        private static void PruneStrays(RectTransform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (child.name == "Header") continue;
                if (child.name == BackdropTextureBuilder.BranchName) continue;
                if (child.name.StartsWith("Skill")) continue;
                Object.DestroyImmediate(child.gameObject);
            }
        }

        /**
         * @brief 제목 + 자동 시전 토글.
         *
         * 제목을 두는 이유는 이 화면에 탭 줄이 없기 때문이다. 성장 패널은 탭이
         * "지금 무엇을 보고 있는가"를 말해주지만, 여기는 화면 전체가 하나라서
         * 그 자리가 비어 있다.
         */
        private static void BuildHeader(RectTransform panel, SkillSystem system, TMP_FontAsset font)
        {
            var existing = panel.Find("Header");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, HeaderHeight);
            rect.anchoredPosition = new Vector2(0f, -TopPadding);

            var title = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Left);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = Vector2.zero;
            title.text = "발도 오의";
            title.color = DimColor;

            // 토글은 오른쪽 절반. 자기 판을 갖는다 - 제목은 글자뿐이고 이쪽은
            // 눌리는 것이라, 그 차이가 형태에서 먼저 읽혀야 한다
            var toggleObject = new GameObject("AutoCast", typeof(RectTransform));
            toggleObject.transform.SetParent(go.transform, false);

            var toggleRect = (RectTransform)toggleObject.transform;
            toggleRect.anchorMin = new Vector2(0.44f, 0f);
            toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            var toggleImage = toggleObject.AddComponent<Image>();
            UiSkin.ApplyPanel(toggleImage, UiSkin.Chrome);

            var toggleButton = toggleObject.AddComponent<Button>();
            UiSkin.ApplyButton(toggleButton, toggleImage);

            var toggleLabel = CreateLabel(toggleObject.transform, font, "Label", TextAlignmentOptions.Center);
            var toggleLabelRect = (RectTransform)toggleLabel.transform;
            toggleLabelRect.anchorMin = Vector2.zero;
            toggleLabelRect.anchorMax = Vector2.one;
            toggleLabelRect.offsetMin = new Vector2(0f, -10f);
            toggleLabelRect.offsetMax = new Vector2(0f, 10f);
            toggleLabel.text = "자동 시전  ON";

            // 잠긴 미리보기의 해금 조건 배너(41단계). 헤더를 덮으므로 자동
            // 시전 토글도 잠긴 동안 함께 막힌다 - 토글은 상태 변경이다.
            // 조건은 탭(LockedTab)과 같은 출처에서 끌어온다
            LockBannerBuilder.Build(go.transform, font,
                                    "Lv." + SkillCatalog.PanelUnlockLevel + " 도달 시 해금",
                                    SkillCatalog.PanelUnlockLevel, 0);

            var toggle = toggleObject.AddComponent<Onikiri.UI.SkillAutoCastToggle>();
            var so = new SerializedObject(toggle);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("button").objectReferenceValue = toggleButton;
            so.FindProperty("label").objectReferenceValue = toggleLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRow(RectTransform panel, SkillSystem system, TMP_FontAsset font, int index)
        {
            var spec = SkillCatalog.Skills[index];
            string rowName = "Skill" + index;

            var existing = panel.Find(rowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(rowName, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(SidePadding, 0f);
            rect.offsetMax = new Vector2(-SidePadding, 0f);
            rect.sizeDelta = new Vector2(-SidePadding * 2f, RowHeight);
            rect.anchoredPosition = new Vector2(0f,
                -(TopPadding + HeaderHeight + HeaderGap + index * (RowHeight + RowGap)));

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Row);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            var icon = CreateIcon(go.transform, UiIcons.For(spec.Id));

            // 행 글자는 전부 캡션 크기(39단계 - 캐릭터 화면과 같은 위계). 이
            // 화면에서 44pt로 남는 것은 머리글("발도 오의")과 자동 시전 버튼뿐이다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            UiFonts.Demote(nameLabel);
            PlaceStretched((RectTransform)nameLabel.transform, TextLeft, CostWidth + 24f, 10f, LineHeight);
            nameLabel.text = spec.DisplayName;

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Right);
            UiFonts.Demote(costLabel);
            PlaceRight((RectTransform)costLabel.transform, 24f, 10f, LineHeight);
            costLabel.color = DimColor;
            costLabel.text = "Lv." + spec.UnlockLevel;

            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Left);
            UiFonts.Demote(valueLabel);
            PlaceStretched((RectTransform)valueLabel.transform, TextLeft, 24f, 10f + LineHeight, LineHeight);
            valueLabel.color = DimColor;
            valueLabel.text = spec.CooldownSeconds.ToString("F1") + "초";

            var skillButton = go.AddComponent<Onikiri.UI.SkillButton>();
            var so = new SerializedObject(skillButton);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("slotIndex").intValue = index;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("rowBackground").objectReferenceValue = image;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("iconTint").colorValue = UiIcons.Tint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 조각

        private static Image CreateIcon(Transform parent, Sprite sprite)
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
            image.color = UiIcons.Tint;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // 스프라이트가 없으면 자홍색 사각형이 남는다. 강화 행과 같은 규칙 -
            // 빠진 아이콘이 눈에도 드러나야 한다
            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);

            return image;
        }

        private static void PlaceStretched(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

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
