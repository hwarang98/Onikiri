using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 부팅 화면(인트로 스텝): 스플래시 -> 타이틀 오버레이를 세운다.
     *
     * ## 자리가 다른 화면들과 다르다
     *
     * 하단 탭·상단 바 화면들은 SafeArea 아래 살고 성장 패널 띠만 덮는다.
     * 이 오버레이는 **UI Canvas 직속 + 전체 화면 + 켜진 채 저장**이다:
     *
     *   직속       노치 보정 밖이어야 한다. 부팅 화면은 기기 화면 전체를
     *              먹빛으로 덮는 것이 목적이라, SafeArea 안에 두면 노치 옆에
     *              게임이 비쳐 보인다
     *   맨 뒤      형제 순서가 그리기 순서다. 마지막 형제 = 맨 위
     *   켜진 채    첫 프레임부터 게임을 가려야 한다(로딩 게이트). 꺼진 채
     *              저장하면 켜 줄 코드가 뜰 때까지 게임이 한 번 드러난다
     *
     * WireScreenExclusivity 목록에는 **넣지 않는다** - 성장 띠를 나눠 쓰는
     * 화면이 아니고, 한 번 내려가면 다시 안 올라온다.
     *
     * ## 톤
     *
     * 먹빛 바탕(카메라 배경과 같은 sumi) · 적 포인트(보스 체력의 적) · 벚꽃
     * (기존 벚가지 실루엣 재사용). 로고 아트는 없다 - 타이포그래피가 로고다
     * (진짜 로고는 폴리싱 스텝에서. 재작화 없이 톤으로 가는 것이 이 스텝의 판단).
     */
    public static class IntroScreenBuilder
    {
        public const string OverlayName = "IntroOverlay";

        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        /** 카메라 배경과 같은 먹빛. 스플래시와 월드가 같은 어둠에서 이어진다 */
        private static readonly Color Sumi = new Color(0.055f, 0.047f, 0.067f, 1f);

        /** 적 포인트. 보스 체력 바와 같은 적이다 - 강조색 셋 규칙 안 */
        private static readonly Color Crimson = UiSkin.BossHealth;

        [MenuItem("Onikiri/Build Intro Screen")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Build()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null)
            {
                Debug.LogError("[Onikiri] UI Canvas missing - run Build Main Scene first.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            var existing = canvas.transform.Find(OverlayName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // ---------------------------------------------------------- 뿌리
            var root = new GameObject(OverlayName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var backdrop = root.AddComponent<Image>();
            backdrop.color = Sumi;
            backdrop.raycastTarget = true;

            // 화면 전체가 탭 받이다. 스플래시 스킵과 "터치하여 시작"이 이 버튼
            var screenButton = root.AddComponent<Button>();
            screenButton.transition = Selectable.Transition.None;

            // ---------------------------------------------------------- 스플래시 1: 스튜디오
            var studio = CreateGroup(rootRect, "StudioGroup");

            var studioLabel = CreateLabel(studio.transform, font, "Label",
                                          TextAlignmentOptions.Center);
            Place(studioLabel, 0f, 0f, 900f, 120f);
            studioLabel.fontSize = Onikiri.UI.PixelFontSizes.GalmuriLarge;
            studioLabel.richText = true;
            studioLabel.text = "<color=#C83A46>202</color> STUDIO";

            // ---------------------------------------------------------- 스플래시 2 + 타이틀 로고
            var brand = CreateGroup(rootRect, "BrandGroup");

            // 벚가지 실루엣(39단계 애셋 재사용). 오른쪽 위 여백에 걸친다
            BackdropTextureBuilder.AddSakuraBranch(brand.transform);

            var title = CreateLabel(brand.transform, font, "Title", TextAlignmentOptions.Center);
            Place(title, 0f, 330f, 1000f, 180f);
            // 132 = 44 x 3. 정수배만 선명하다 (PixelFontSizes 규칙)
            title.fontSize = Onikiri.UI.PixelFontSizes.GalmuriAtlasSize * 3;
            title.text = "ONIKIRI";

            // 먹 위의 한 획. 제목과 부제를 가르는 적선이 이 화면의 적 포인트다
            var rule = new GameObject("Rule", typeof(RectTransform));
            rule.transform.SetParent(brand.transform, false);
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.sizeDelta = new Vector2(460f, 6f);
            ruleRect.anchoredPosition = new Vector2(0f, 232f);
            var ruleImage = rule.AddComponent<Image>();
            ruleImage.color = Crimson;
            ruleImage.raycastTarget = false;

            var subtitle = CreateLabel(brand.transform, font, "Subtitle",
                                       TextAlignmentOptions.Center);
            Place(subtitle, 0f, 172f, 400f, 70f);
            subtitle.color = Crimson;
            subtitle.text = "귀참";

            // ---------------------------------------------------------- 타이틀 (CTA)
            var titleGroup = CreateGroup(rootRect, "TitleGroup");

            // -- 갈래 A: 계정 선택 (첫 실행)
            var account = CreateGroup(titleGroup.transform, "AccountGroup");

            var googleArea = CreateGroup(account.transform, "GoogleArea");

            var googleButton = CreateButton(googleArea.transform, "GoogleButton",
                                            new Vector2(0f, -360f), new Vector2(640f, 96f));
            // 주(主) 버튼. 새 색이 아니라 같은 판을 밝힌 것이다 (랭킹 내 줄과
            // 같은 판단 - 첫 설치가 가입 의향의 최고점이라 이쪽이 앞선다)
            UiSkin.ApplyPanel(googleButton.GetComponent<Image>(), UiSkin.Panel, UiSkin.InlayTint);
            var googleLabel = CreateLabel(googleButton.transform, font, "Label",
                                          TextAlignmentOptions.Center);
            Fill(googleLabel);
            googleLabel.text = "구글로 로그인";

            var note = CreateLabel(googleArea.transform, font, "Note", TextAlignmentOptions.Center);
            Place(note, 0f, -288f, 800f, 44f);
            UiFonts.Demote(note);
            note.color = UiSkin.TextDim;
            note.text = "기록이 계정에 남아 기기를 옮겨도 이어집니다";

            var guestButton = CreateButton(account.transform, "GuestButton",
                                           new Vector2(0f, -492f), new Vector2(640f, 80f));
            var guestLabel = CreateLabel(guestButton.transform, font, "Label",
                                         TextAlignmentOptions.Center);
            Fill(guestLabel);
            UiFonts.Demote(guestLabel);
            guestLabel.text = "게스트로 시작";

            // -- 갈래 B: 이미 고른 사람
            var touch = CreateGroup(titleGroup.transform, "TouchGroup");
            var touchLabel = CreateLabel(touch.transform, font, "Label",
                                         TextAlignmentOptions.Center);
            Place(touchLabel, 0f, -420f, 700f, 70f);
            touchLabel.text = "터치하여 시작";

            // -- 상태줄들
            var warn = CreateLabel(titleGroup.transform, font, "Warn", TextAlignmentOptions.Center);
            Place(warn, 0f, -620f, 960f, 44f);
            UiFonts.Demote(warn);
            warn.color = UiSkin.Danger;
            warn.gameObject.SetActive(false);

            var status = CreateLabel(titleGroup.transform, font, "Status",
                                     TextAlignmentOptions.Center);
            Place(status, 0f, -680f, 960f, 44f);
            UiFonts.Demote(status);
            status.color = UiSkin.TextDim;
            status.gameObject.SetActive(false);

            var version = CreateLabel(titleGroup.transform, font, "Version",
                                      TextAlignmentOptions.Center);
            var versionRect = (RectTransform)version.transform;
            versionRect.anchorMin = new Vector2(0.5f, 0f);
            versionRect.anchorMax = new Vector2(0.5f, 0f);
            versionRect.sizeDelta = new Vector2(800f, 44f);
            versionRect.anchoredPosition = new Vector2(0f, 64f);
            UiFonts.Demote(version);
            version.color = UiSkin.TextDim;
            version.text = "버전 0.0  ·  202 STUDIO";

            // ---------------------------------------------------------- 배선
            var link = googleButton.gameObject.AddComponent<Onikiri.UI.GoogleLinkButton>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("button").objectReferenceValue = googleButton;
            // 버튼과 안내문이 함께 사라져야 한다 - 버튼 없는 안내문은 고아다
            linkSo.FindProperty("hideRoot").objectReferenceValue = googleArea;
            linkSo.ApplyModifiedPropertiesWithoutUndo();

            var flow = root.AddComponent<Onikiri.UI.IntroFlow>();
            var so = new SerializedObject(flow);
            so.FindProperty("session").objectReferenceValue =
                Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            so.FindProperty("studioGroup").objectReferenceValue = studio;
            so.FindProperty("brandGroup").objectReferenceValue = brand;
            so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
            so.FindProperty("accountGroup").objectReferenceValue = account;
            so.FindProperty("touchGroup").objectReferenceValue = touch;
            so.FindProperty("touchLabel").objectReferenceValue = touchLabel;
            so.FindProperty("googleLink").objectReferenceValue = link;
            so.FindProperty("guestButton").objectReferenceValue = guestButton;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("warnLabel").objectReferenceValue = warn;
            so.FindProperty("versionLabel").objectReferenceValue = version;
            so.FindProperty("screenButton").objectReferenceValue = screenButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 첫 프레임의 모습이 곧 저장 상태다: 스튜디오만 켜져 있다.
            // (IntroFlow.ApplyPhase가 플레이 시작에서 같은 상태를 다시 세운다)
            studio.SetActive(true);
            brand.SetActive(false);
            titleGroup.SetActive(false);

            Debug.Log("[Onikiri] Intro overlay built: splash -> title -> game.");
        }

        // ---------------------------------------------------------------- 조각

        private static GameObject CreateGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static Button CreateButton(Transform parent, string name,
                                           Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, UiSkin.Chrome);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);
            return button;
        }

        /** 가운데 앵커 기준 배치 */
        private static void Place(TMP_Text label, float x, float y, float width, float height)
        {
            var rect = (RectTransform)label.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        /** 부모(버튼)를 꽉 채운다. 세로는 잘림 방지로 넓힌다 */
        private static void Fill(TMP_Text label)
        {
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, -8f);
            rect.offsetMax = new Vector2(0f, 8f);
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
            label.color = UiSkin.Text;
            label.raycastTarget = false;
            return label;
        }
    }
}
