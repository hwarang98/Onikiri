using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 전직(사무라이 진화)의 씬 내용을 세운다 (33단계).
     *
     * 하는 일이 셋이다:
     *
     *   1. 티어 스프라이트 팩 슬라이싱 - 팩마다 셀 크기가 다르므로 재고 자른다
     *   2. EvolutionSystem을 Battle에 세우고 GameSession에 배선
     *   3. EvolutionAppearance를 사무라이에 세우고 일곱 벌의 클립을 굽는다
     *
     * 화면(전직 페이지)은 UpgradePanelBuilder.BuildAwakenPage가 만든다 - 그
     * 페이지는 성장 패널의 것이고, 페이지 잔재 검사(RowsInPage)가 그쪽에
     * 있기 때문이다. 이 빌더를 먼저 돌려야 그쪽이 초상(티어 idle 첫 프레임)을
     * 읽을 수 있다.
     */
    public static class EvolutionContentBuilder
    {
        private const string CharacterRoot = "Assets/ThirdParty/Characters/";

        [MenuItem("Onikiri/Build Evolution Content")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static EvolutionSystem Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return null;
            }

            var samurai = GameObject.Find("Samurai");
            if (samurai == null)
            {
                Debug.LogError("[Onikiri] Samurai missing - run Build Combat Content first.");
                return null;
            }

            SlicePacks();

            var system = EnsureSystem(battle);
            WireSession(system);
            BakeAppearance(samurai);

            Debug.Log(string.Format(
                "[Onikiri] Evolution content built: {0} tiers + base, unlock Lv.{1}, gems total {2}.",
                EvolutionCatalog.Count, EvolutionCurve.UnlockLevel, EvolutionCurve.TotalGems));
            return system;
        }

        // ---------------------------------------------------------------- 슬라이싱

        /**
         * @brief 카탈로그가 쓰는 팩을 전부 자른다.
         *
         * 데몬사무라이 팩은 보스 슬라이서(128x108, 발밑 12px)가 이미 소유자다 -
         * FLAMING 시트까지 같은 폴더라 SliceBoss 한 번이면 전부 잘린다. 여기서
         * 다른 격자로 다시 자르면 보스 프레임의 렉트가 통째로 바뀌어 기존
         * 참조가 흔들리므로, **그 팩은 그쪽 규격을 그대로 쓴다.**
         *
         * 사무라이 팩들(Samurai_2/3/5/6)은 시트가 가로 한 줄이고 **셀이
         * 정사각이 아니다** - 높이는 시트 높이 그대로, 폭은 팩마다 다르다
         * (96/106/96/98, 실측). 폭을 손으로 적는 대신 팩 안 모든 시트 폭의
         * 최대공약수로 유도한다 - 프레임 수가 시트마다 달라도(5/8/10칸) 셀
         * 폭은 같아서 GCD가 정확히 그 값이 된다. 처음에 "셀 = 높이 x 높이"로
         * 가정했다가 네 팩 중 셋이 0장으로 잘렸다(폭이 높이의 배수가 아니다).
         *
         * 발밑 여백은 **시트마다** 실측해 그 시트의 피벗에 넣는다. 처음에 팩
         * 전체의 최솟값 하나를 썼다가 물렸다 - 쓰러지는 DEATH 시트가 최솟값을
         * 2px까지 끌어내리는데 idle의 실제 발은 13px이라(검객), 피벗을 지면에
         * 놓는 월드 배치에서 캐릭터가 그 차이만큼 **공중에 떴다.** 시트별로
         * 재면 각 클립이 자기 발로 서고, 클립 전환에서도 발이 지면에 남는다.
         * 보스 슬라이서가 고정 12px 하나를 쓰는 것은 그 팩의 세 시트가 전부
         * 12px로 같다고 검증했기 때문이다 - 이 팩들은 그렇지 않다.
         */
        private static void SlicePacks()
        {
            CharacterSpriteSlicer.SliceBoss();

            foreach (var folder in SamuraiPackFolders())
            {
                string spriteFolder = CharacterRoot + folder + "/Sprites";
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { spriteFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    // 캐릭터 시트만. 팩에 딸린 소품(수리검 8x8, 먼지 128x32)은
                    // 셀 높이가 달라 GCD를 망가뜨린다 - 높이가 다르면 소품이다
                    if (texture == null) continue;
                    paths.Add(path);
                }

                if (paths.Count == 0)
                {
                    Debug.LogWarning("[Onikiri] Evolution pack '" + folder + "' has no textures.");
                    continue;
                }

                // 셀 높이 = 가장 흔한 시트 높이. 그와 다른 시트는 소품으로 본다
                int cellHeight = DominantHeight(paths);
                if (cellHeight <= 0) continue;

                // 셀 폭 = 캐릭터 시트 폭들의 최대공약수
                int cellWidth = 0;
                var sheets = new List<string>();
                foreach (var path in paths)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null || texture.height != cellHeight) continue;
                    sheets.Add(path);
                    cellWidth = cellWidth == 0 ? texture.width : Gcd(cellWidth, texture.width);
                }

                // GCD가 이상하게 작으면 어느 시트가 규격 밖이라는 뜻이다. 조용히
                // 자르면 프레임이 어긋난 채로 화면에 나오므로 여기서 멈춘다
                if (cellWidth < 32)
                {
                    Debug.LogWarning(string.Format(
                        "[Onikiri] '{0}' sheet widths reduce to a {1}px cell - the pack breaks"
                        + " the one-row grid assumption. Slice it by hand.", folder, cellWidth));
                    continue;
                }

                // 시트마다 발밑 여백을 재서 자기 피벗으로 자른다. 시트 안에서
                // 프레임마다 다르면 가장 낮은 프레임에 맞춘다(MeasureFeetPadding이
                // 최솟값을 낸다) - 다른 프레임이 땅에 박히는 것보다 낫다
                int sliced = 0;
                var feetLog = new System.Text.StringBuilder();
                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (var path in sheets)
                    {
                        int padding = CharacterSpriteSlicer.MeasureFeetPadding(path, cellWidth, cellHeight);
                        if (padding < 0) padding = 0;

                        var pivot = new Vector2(0.5f, padding / (float)cellHeight);
                        if (CharacterSpriteSlicer.SliceGrid(path, cellWidth, cellHeight, pivot))
                        {
                            sliced++;
                            feetLog.Append(System.IO.Path.GetFileNameWithoutExtension(path))
                                   .Append(":").Append(padding).Append("px ");
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }

                Debug.Log(string.Format(
                    "[Onikiri] Sliced {0} sheets in '{1}' at {2}x{3}, feet per sheet: {4}",
                    sliced, folder, cellWidth, cellHeight, feetLog.ToString()));
            }
        }

        /** 팩에서 가장 흔한 시트 높이. 소품(수리검·먼지)이 섞여 있어 다수결로 정한다 */
        private static int DominantHeight(List<string> paths)
        {
            var counts = new Dictionary<int, int>();
            foreach (var path in paths)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                int height = texture.height;
                counts[height] = counts.ContainsKey(height) ? counts[height] + 1 : 1;
            }

            int best = 0, bestCount = 0;
            foreach (var pair in counts)
                if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            return best;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return a;
        }

        /** 카탈로그의 사무라이 팩 폴더들 (데몬 제외, 중복 제거) */
        private static IEnumerable<string> SamuraiPackFolders()
        {
            var seen = new HashSet<string>();
            foreach (var tier in EvolutionCatalog.Tiers)
            {
                if (tier.SpriteFolder == "Demon_Samurai") continue;
                if (seen.Add(tier.SpriteFolder)) yield return tier.SpriteFolder;
            }
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief EvolutionSystem을 Battle에 세운다. **티어는 덮어쓰지 않는다.**
         *
         * 여기서 0으로 되돌리면 빌더를 돌릴 때마다 플레이 중인 세이브의 진화가
         * 사라진다 - EquipmentPanelBuilder.EnsureSystem과 같은 규칙이다.
         */
        private static EvolutionSystem EnsureSystem(GameObject battle)
        {
            var system = battle.GetComponent<EvolutionSystem>();
            if (system == null) system = battle.AddComponent<EvolutionSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("upgrades").objectReferenceValue =
                Object.FindFirstObjectByType<UpgradeSystem>(FindObjectsInactive.Include);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("character").objectReferenceValue =
                Object.FindFirstObjectByType<CharacterLevel>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        private static void WireSession(EvolutionSystem system)
        {
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session == null)
            {
                Debug.LogWarning("[Onikiri] GameSession missing - evolution tier will not be saved.");
                return;
            }

            var so = new SerializedObject(session);
            so.FindProperty("evolution").objectReferenceValue = system;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 외형

        /** 시트 이름 후보. 팩마다 표기가 다르다 (ATTACK1 / ATTACK 1) */
        private static readonly string[] IdleSheets = { "IDLE" };
        private static readonly string[] AttackSheets = { "ATTACK 1", "ATTACK1" };
        private static readonly string[] RunSheets = { "RUN" };
        private static readonly string[] HurtSheets = { "HURT" };
        private static readonly string[] DeathSheets = { "DEATH" };

        private static void BakeAppearance(GameObject samurai)
        {
            var appearance = samurai.GetComponent<EvolutionAppearance>();
            if (appearance == null) appearance = samurai.AddComponent<EvolutionAppearance>();

            var so = new SerializedObject(appearance);
            so.FindProperty("combat").objectReferenceValue = samurai.GetComponent<PlayerCombat>();
            so.FindProperty("health").objectReferenceValue = samurai.GetComponent<PlayerHealth>();
            so.FindProperty("sakura").objectReferenceValue = samurai.GetComponent<SakuraBurst>();
            so.FindProperty("flash").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.UI.ScreenFlash>(FindObjectsInactive.Include);
            so.FindProperty("whiteFlash").objectReferenceValue = BuildEvolveFlash();
            so.FindProperty("silhouetteRenderer").objectReferenceValue =
                samurai.GetComponent<SpriteRenderer>();

            var tiers = so.FindProperty("tiers");
            tiers.arraySize = EvolutionCatalog.Count + 1;

            BakeTier(tiers.GetArrayElementAtIndex(0), EvolutionCatalog.BaseName,
                     EvolutionCatalog.BaseSpriteFolder, false);

            for (int t = 0; t < EvolutionCatalog.Count; t++)
            {
                var spec = EvolutionCatalog.Tiers[t];
                BakeTier(tiers.GetArrayElementAtIndex(t + 1), spec.Name, spec.SpriteFolder, spec.Aura);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /**
         * @brief 경지 상승 전용 전면 백광 (39단계).
         *
         * 캔버스 직속 + 형제 맨 뒤 - 노치 옆까지 덮고 모든 UI 위에 그려진다.
         * ScreenFlash와 같은 배치 규칙이고, 꺼진 채로 저장되는 것도 같다.
         * 흰 단색이라 스프라이트가 필요 없다.
         */
        private static Onikiri.UI.EvolveFlash BuildEvolveFlash()
        {
            var canvas = GameObject.Find("UI Canvas");
            if (canvas == null)
            {
                Debug.LogWarning("[Onikiri] UI Canvas missing - evolve flash not built.");
                return null;
            }

            const string name = "EvolveFlash";
            var existing = canvas.transform.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var cover = go.AddComponent<UnityEngine.UI.Image>();
            cover.color = new Color(1f, 1f, 1f, 0f);

            // 레이캐스트를 먹으면 백광이 걷히는 0.5초 동안 모든 버튼이 죽는다
            cover.raycastTarget = false;

            var flash = go.AddComponent<Onikiri.UI.EvolveFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("cover").objectReferenceValue = cover;
            so.FindProperty("peak").floatValue = 1f;
            so.FindProperty("holdSeconds").floatValue = 0.08f;
            so.FindProperty("fadeSeconds").floatValue = 0.45f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return flash;
        }

        private static void BakeTier(SerializedProperty element, string label, string folder, bool aura)
        {
            element.FindPropertyRelative("label").stringValue = label;

            AssignClip(element.FindPropertyRelative("idle"), folder, IdleSheets, aura, label);
            AssignClip(element.FindPropertyRelative("attack"), folder, AttackSheets, aura, label);
            AssignClip(element.FindPropertyRelative("run"), folder, RunSheets, aura, label);
            AssignClip(element.FindPropertyRelative("hurt"), folder, HurtSheets, aura, label);
            AssignClip(element.FindPropertyRelative("death"), folder, DeathSheets, aura, label);
        }

        /**
         * @brief 시트 하나를 찾아 프레임 배열로 굽는다.
         *
         * Aura 티어는 "(FLAMING SWORD)" 변형을 먼저 찾고, 없으면 일반 시트로
         * 내려간다 - 데몬 팩의 DEATH가 그렇다(불검 변형이 없다). 죽는 순간
         * 불이 꺼지는 것은 어색하지 않다.
         */
        private static void AssignClip(SerializedProperty array, string folder,
                                       string[] names, bool aura, string label)
        {
            var sprites = new List<Sprite>();

            foreach (var name in names)
            {
                if (aura && TryLoad(folder, name + " (FLAMING SWORD)", sprites)) break;
                if (TryLoad(folder, name, sprites)) break;
            }

            if (sprites.Count == 0)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Tier '{0}' has no {1} sheet in {2} - the previous tier's look will linger.",
                    label, names[0], folder));

            array.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static bool TryLoad(string folder, string sheetName, List<Sprite> into)
        {
            string path = CharacterRoot + folder + "/Sprites/" + sheetName + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return false;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite != null) into.Add(sprite);
            }

            // 슬라이서가 이름을 "BASE_i"로 붙인다. 인덱스로 정렬해야 재생 순서가
            // 시트 순서와 같다
            into.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return into.Count > 0;
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            if (underscore < 0) return 0;

            int index;
            return int.TryParse(spriteName.Substring(underscore + 1), out index) ? index : 0;
        }
    }
}
