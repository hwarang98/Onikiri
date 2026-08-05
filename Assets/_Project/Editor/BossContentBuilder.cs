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
     * @brief 보스(다크 사무라이) 정의와 보스전 UI를 만들고 배선한다.
     *
     * 보스는 별도의 클래스가 아니라 Enemy 하나다. 하는 일이 잡몹과 완전히 같기
     * 때문이다 - 걸어와서 멈추고 맞고 죽는다. 다른 것은 아트와 스탯과 연출뿐이고,
     * 그중 스탯은 스폰 시점에 BossFight가 확정해서 넘긴다. 그래서 여기서 만드는
     * 것은 아트가 들어간 EnemyDefinition 하나와 화면 몇 장이다.
     */
    public static class BossContentBuilder
    {
        private const string BossSpriteFolder = "Assets/ThirdParty/Characters/Demon_Samurai/Sprites";
        private const string BossIdleSheet = BossSpriteFolder + "/IDLE.png";
        private const string BossHurtSheet = BossSpriteFolder + "/HURT.png";
        private const string BossDeathSheet = BossSpriteFolder + "/DEATH.png";

        private const string DataFolder = "Assets/_Project/Data";
        private const string BossDefinitionPath = DataFolder + "/Enemy_DarkSamurai.asset";
        private const string GalmuriFontPath = "Assets/_Project/Art/Fonts/Galmuri11 SDF.asset";

        public const string BossDisplayName = "다크 사무라이";

        private static readonly Color DimColor = new Color32(0x0A, 0x08, 0x10, 0xE0);
        private static readonly Color PanelColor = new Color32(0x3A, 0x35, 0x50, 0xF0);
        private static readonly Color TextColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        private static readonly Color BossNameColor = new Color32(0xE8, 0x6A, 0x6A, 0xFF);
        private static readonly Color ButtonColor = new Color32(0x8C, 0x3A, 0x4E, 0xFF);
        private static readonly Color HealthColor = new Color32(0xC8, 0x3A, 0x46, 0xFF);
        private static readonly Color HealthBackColor = new Color32(0x1A, 0x16, 0x24, 0xD0);

        // ---------------------------------------------------------------- 정의 에셋

        /**
         * @brief 보스 정의 에셋.
         *
         * 체력과 골드는 여기 적지 않는다. 잡몹 평균에 스테이지 배수와 보스 배수를
         * 곱한 값이라 스테이지마다 다르고, BossFight가 스폰 시점에 계산해서 넘긴다.
         * 에셋에 그럴듯한 숫자를 적어두면 언젠가 그 값이 실제 밸런스라고 오해받는다.
         */
        public static EnemyDefinition BuildDefinition()
        {
            // 자동 슬라이싱이 만든 타이트 렉트를 그대로 두면 프레임마다 피벗이 달라
            // 보스가 제자리에서 떨린다. 격자로 다시 자른다
            CharacterSpriteSlicer.SliceBoss();

            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, BossDefinitionPath);
            }

            definition.displayName = BossDisplayName;
            definition.idleFrames = OrderedSprites(BossIdleSheet).ToArray();
            definition.hurtFrames = OrderedSprites(BossHurtSheet).ToArray();
            definition.deathFrames = OrderedSprites(BossDeathSheet).ToArray();

            // 잡몹보다 느린 12fps다. 보스는 26프레임짜리 사망 연출을 들고 있어서
            // 잡몹과 같은 속도로 돌리면 죽는 데 2초가 넘게 걸린다. 그 길이가
            // 보상 순간으로는 적당하다고 판단해 그대로 둔다
            definition.frameRate = 12f;

            // 잡몹 추첨에 절대 끼면 안 된다. 스포너의 definitions 배열에도 넣지
            // 않지만, 가중치 0을 함께 적어 의도를 에셋에서도 읽히게 한다
            definition.spawnWeight = 0f;

            // 스폰 시점에 덮어쓰인다. 0으로 두면 혹시 이 값으로 스폰되는 경로가
            // 생겼을 때 즉사하는 보스로 곧바로 드러난다
            definition.maxHealth = BigDouble.Zero;
            definition.goldReward = BigDouble.Zero;

            // 잡몹보다 느리게 걸어 들어온다. 무게가 속도로 읽힌다
            definition.moveSpeed = 0.85f;
            definition.queueSpacing = 2.0f;
            definition.hoverHeight = 0f;

            // 잡몹과 달리 0이다. 잡몹은 Aseprite 캔버스 피벗을 쓰기 때문에 아트까지의
            // 거리를 런타임에 빼줘야 하지만, 보스는 SliceBoss가 피벗을 아예 발밑
            // (아래에서 12px)에 찍는다. 여기서 또 빼면 공중에 뜬다
            definition.artBottomOffset = 0f;

            EditorUtility.SetDirty(definition);

            Debug.Log(string.Format(
                "[Onikiri] Boss '{0}': idle={1} hurt={2} death={3} frames.",
                definition.displayName, definition.idleFrames.Length,
                definition.hurtFrames.Length, definition.deathFrames.Length));

            return definition;
        }

        // ---------------------------------------------------------------- 씬 배선

        /**
         * @brief 보스전 컴포넌트와 화면을 씬에 붙인다.
         *
         * BattleContentBuilder가 스포너·강화·세션을 배선한 뒤에 부른다.
         */
        public static BossFight Wire(EnemySpawner spawner)
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BossDefinitionPath);
            if (definition == null)
            {
                Debug.LogError("[Onikiri] Boss definition missing at " + BossDefinitionPath);
                return null;
            }

            var battle = GameObject.Find("Battle");
            if (battle == null) { Debug.LogError("[Onikiri] Battle root missing."); return null; }

            var fight = battle.GetComponent<BossFight>();
            if (fight == null) fight = battle.AddComponent<BossFight>();

            var panel = MainSceneBuilder.FindBand("GrowthPanel");

            var fightSo = new SerializedObject(fight);
            fightSo.FindProperty("spawner").objectReferenceValue = spawner;
            fightSo.FindProperty("progress").objectReferenceValue = battle.GetComponent<StageProgress>();
            fightSo.FindProperty("bossDefinition").objectReferenceValue = definition;
            fightSo.FindProperty("upgrades").objectReferenceValue =
                panel != null ? panel.GetComponent<UpgradeSystem>() : null;
            fightSo.FindProperty("introSeconds").floatValue = 1f;
            fightSo.FindProperty("failSeconds").floatValue = 3f;
            fightSo.FindProperty("bossName").stringValue = BossDisplayName;
            fightSo.ApplyModifiedPropertiesWithoutUndo();

            BuildHud(fight);
            return fight;
        }

        /**
         * @brief 보스전 화면 네 장.
         *
         * 등장 연출만 안전 영역 루트에 두어 화면 전체를 덮는다. 나머지 셋은 전투
         * 밴드 안이다 - 시계와 도전 버튼은 전투를 가리면 안 되고, 실패 문구는
         * 뒤의 강화 버튼이 계속 눌려야 한다. 실패 직후에 해야 할 일이 정확히
         * 그 버튼을 누르는 것이기 때문이다.
         */
        private static void BuildHud(BossFight fight)
        {
            var safeArea = UpgradePanelBuilder.EnsureSafeArea();
            var band = MainSceneBuilder.FindBand("BattleArea");
            if (safeArea == null || band == null)
            {
                Debug.LogError("[Onikiri] Cannot build the boss HUD without SafeArea/BattleArea.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GalmuriFontPath);

            Replace(safeArea, "BossIntro");
            Replace(band, "BossChallenge");
            Replace(band, "BossFightHud");
            Replace(band, "BossResult");

            // --- 등장 연출: 화면 전체를 덮는 어두운 판 + 이름
            var intro = new GameObject("BossIntro", typeof(RectTransform));
            intro.transform.SetParent(safeArea, false);
            intro.transform.SetAsLastSibling();
            Stretch((RectTransform)intro.transform);
            // 판 자체가 레이캐스트를 먹어 연출 중 강화 버튼이 눌리는 것을 막는다
            intro.AddComponent<Image>().color = DimColor;

            var introLabel = CreateLabel(intro.transform, font, "Name", PixelFontSizesLarge, TextAlignmentOptions.Center);
            // 화면 정중앙이 아니라 전투 밴드의 한가운데다. 안전 영역 기준 중앙은 지면선
            // 바로 위라, 이름이 사무라이와 지면 풀에 겹쳐 읽힌다. 밴드는 45~90% 구간이고
            // 그 중심은 화면 중심에서 위로 22.5% - 1920 기준 432px이다
            CenterBox((RectTransform)introLabel.transform, new Vector2(1000f, 160f),
                      new Vector2(0f, DisplayConfig.DesignHeight * 0.225f));
            introLabel.color = BossNameColor;
            introLabel.text = BossDisplayName;

            // --- 도전 버튼: 전투 밴드 위쪽. 보스 체력 바와 같은 자리다.
            //
            // 처음에는 밴드 아래쪽에 뒀는데, 그 자리가 지면선이라 버튼이 사무라이의
            // 하반신과 풀을 덮었다. 보스가 열린 동안은 계속 떠 있는 버튼이라 그동안
            // 전투가 가려진다.
            //
            // 위쪽은 하늘이라 비어 있고, 무엇보다 이 슬롯은 보스 체력 바가 쓰는 자리와
            // 같다. 두 화면은 배타적이므로 겹칠 일이 없고, "보스에 관한 것은 여기 뜬다"가
            // 한 자리로 유지된다.
            var challenge = new GameObject("BossChallenge", typeof(RectTransform));
            challenge.transform.SetParent(band, false);
            var challengeRect = (RectTransform)challenge.transform;
            challengeRect.anchorMin = challengeRect.anchorMax = new Vector2(0.5f, 1f);
            challengeRect.pivot = new Vector2(0.5f, 1f);
            challengeRect.sizeDelta = new Vector2(440f, 124f);
            challengeRect.anchoredPosition = new Vector2(0f, -24f);

            var challengeImage = challenge.AddComponent<Image>();
            challengeImage.color = ButtonColor;
            var challengeButton = challenge.AddComponent<Button>();
            challengeButton.targetGraphic = challengeImage;

            var challengeLabel = CreateLabel(challenge.transform, font, "Label",
                                             PixelFontSizesSmall, TextAlignmentOptions.Center);
            Stretch((RectTransform)challengeLabel.transform);
            challengeLabel.text = "보스 도전";

            // --- 전투 HUD: 밴드 위쪽에 체력 바, 그 아래 시계
            var fightHud = new GameObject("BossFightHud", typeof(RectTransform));
            fightHud.transform.SetParent(band, false);
            Stretch((RectTransform)fightHud.transform);

            var healthBack = new GameObject("HealthBack", typeof(RectTransform));
            healthBack.transform.SetParent(fightHud.transform, false);
            var backRect = (RectTransform)healthBack.transform;
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(1f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.offsetMin = new Vector2(60f, -66f);
            backRect.offsetMax = new Vector2(-60f, -24f);
            healthBack.AddComponent<Image>().color = HealthBackColor;

            var healthFill = new GameObject("HealthFill", typeof(RectTransform));
            healthFill.transform.SetParent(healthBack.transform, false);
            Stretch((RectTransform)healthFill.transform);
            var fillImage = healthFill.AddComponent<Image>();
            fillImage.color = HealthColor;
            // Filled 타입이라야 fillAmount가 동작한다. Simple이면 값을 넣어도 아무
            // 일도 일어나지 않고, 체력 바가 항상 가득 찬 채로 남는다
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            // 스프라이트가 없으면 Filled가 무시된다. 내장 흰색 UI 스프라이트를 쓴다
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var timerLabel = CreateLabel(fightHud.transform, font, "Timer",
                                         PixelFontSizesSmall, TextAlignmentOptions.Center);
            var timerRect = (RectTransform)timerLabel.transform;
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.sizeDelta = new Vector2(400f, 80f);
            timerRect.anchoredPosition = new Vector2(0f, -76f);
            timerLabel.text = "30초";

            // --- 실패/결과 문구: 밴드 가운데. 레이캐스트를 먹지 않는다
            var result = new GameObject("BossResult", typeof(RectTransform));
            result.transform.SetParent(band, false);
            var resultRect = (RectTransform)result.transform;
            resultRect.anchorMin = resultRect.anchorMax = new Vector2(0.5f, 0.5f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.sizeDelta = new Vector2(960f, 300f);
            resultRect.anchoredPosition = Vector2.zero;

            var resultImage = result.AddComponent<Image>();
            resultImage.color = PanelColor;
            resultImage.raycastTarget = false;

            var resultLabel = CreateLabel(result.transform, font, "Message",
                                          PixelFontSizesSmall, TextAlignmentOptions.Center);
            Stretch((RectTransform)resultLabel.transform);
            // 두 줄이 들어간다. 실패 문구는 "무슨 일이 있었는가"와 "무엇을 하면
            // 되는가"로 나뉘고, 한 줄로 붙이면 둘 다 흘려 읽힌다
            resultLabel.textWrappingMode = TextWrappingModes.Normal;
            resultLabel.text = "화력이 1.4배 모자란다.\n공격력 강화를 올리고 다시 도전하라";

            var hud = band.GetComponent<Onikiri.UI.BossHud>();
            if (hud == null) hud = band.gameObject.AddComponent<Onikiri.UI.BossHud>();

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("fight").objectReferenceValue = fight;
            hudSo.FindProperty("challengeRoot").objectReferenceValue = challenge;
            hudSo.FindProperty("challengeButton").objectReferenceValue = challengeButton;
            hudSo.FindProperty("challengeLabel").objectReferenceValue = challengeLabel;
            hudSo.FindProperty("introRoot").objectReferenceValue = intro;
            hudSo.FindProperty("introLabel").objectReferenceValue = introLabel;
            hudSo.FindProperty("fightRoot").objectReferenceValue = fightHud;
            hudSo.FindProperty("timerLabel").objectReferenceValue = timerLabel;
            hudSo.FindProperty("healthFill").objectReferenceValue = fillImage;
            hudSo.FindProperty("resultRoot").objectReferenceValue = result;
            hudSo.FindProperty("resultLabel").objectReferenceValue = resultLabel;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // 넷 다 꺼진 채로 저장한다. 켜진 채로 저장되면 게임을 켜자마자 보스
            // 이름이 화면을 덮은 상태로 시작한다. OfflineRewardPopup에서 같은
            // 실수를 한 적이 있다
            intro.SetActive(false);
            challenge.SetActive(false);
            fightHud.SetActive(false);
            result.SetActive(false);
        }

        // ---------------------------------------------------------------- 도구

        private const float PixelFontSizesSmall = Onikiri.UI.PixelFontSizes.GalmuriSmall;
        private const float PixelFontSizesLarge = Onikiri.UI.PixelFontSizes.GalmuriLarge;

        private static void Replace(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                                   float size, TextAlignmentOptions alignment)
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
            label.fontSize = size;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void CenterBox(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static List<Sprite> OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites;
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            int value;
            if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out value)) return value;
            return 0;
        }
    }
}
