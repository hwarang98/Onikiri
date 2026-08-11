using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 동료(펫)의 씬 내용을 세운다.
     *
     * 하는 일이 넷이다:
     *
     *   1. 세 펫 팩 슬라이싱 - 팩마다 셀 크기가 다르므로 재고 자른다
     *      (Wolf 192x64 / Panda 128x64 / Archer 96x80, EvolutionContentBuilder
     *      방식). 궁수의 ARROW / ARROW HIT는 높이가 달라 별도 규격(48x16)이다
     *   2. PetSystem을 Battle에 세우고 카탈로그 값을 옮겨 적고 GameSession에 배선
     *   3. Battle/Player 아래 Pet 전투체를 세우고 PetCombat에 클립을 굽는다
     *   4. 펫 화면은 PetPanelBuilder가 만든다 - 이 빌더를 먼저 돌려야 그쪽이
     *      초상(idle 첫 프레임)을 읽을 수 있다
     *
     * ## 색상 변형은 빌더가 정한다
     *
     * 늑대는 외곽선 있는 쪽(with_outline) - 어두운 배경에서 몸이 뭉개지지
     * 않는다. 궁수는 GREEN - 로닌(적갈)·데몬(적)과 갈리는 색이라 두 전투원이
     * 한 화면에서 섞이지 않는다. 아트 선택은 카탈로그(밸런스)가 아니라
     * 빌더(씬)의 것이다 - EvolutionCatalog가 팩 이름만 들고 있는 것과 같은 결.
     */
    public static class PetContentBuilder
    {
        private const string CharacterRoot = "Assets/ThirdParty/Characters/";

        /**
         * @brief 동료별 전투체 배치. **로닌 왼쪽 뒤의 부채꼴 포메이션이다.**
         *
         * 처음에 청랑을 로닌 앞(x -0.60)에 뒀다가 물렸다 - 로닌이 돌진(런지)
         * 하는 자리와 겹쳐서 주인공이 늑대에 파묻혔다. 규칙을 다시 세웠다:
         *
         *   로닌이 시각적 초점이다. 동료는 전부 로닌의 **왼쪽 뒤**에 서고,
         *   정렬 순서도 전부 로닌(50) 아래다. 동료끼리 X 간격을 벌린다.
         *   발선은 **전원 로닌과 같은 지면(땅·풀 경계, Y=0)이다** - 아래
         *   FighterSpec 주석 참고.
         *
         *   근접(청랑)=로닌 바로 뒤 / 강타(묵웅)=중간 / 원거리(명궁)=맨 뒤.
         *   각자 자기 자리에서 적 방향으로 공격하고 자리를 뜨지 않는다.
         *
         * 동료는 전부 로닌보다 조금 작다(청랑 0.78, 묵웅·명궁 0.90) - 백라인
         * 포메이션의 원근이기도 하고, 세로 화면 폭에서 몸집을 줄여야 넷이
         * 겹치지 않고 각자 읽힌다. 특히 청랑은 셀이 192px로 로닌보다 커서
         * 원본 크기로는 어디에 세워도 주인공을 덮는다.
         *
         * artFacesLeft는 팩의 그려진 방향이다(아트 속성이라 빌더 소유).
         * **실측 확정 표다 - 어림 금지, 화면으로 확인한 값만 적는다:**
         *
         *   Wolf_Samurai (with_outline)  좌향  -> true
         *   Samurai_Panda                좌향  -> true   (어림으로 우향이라
         *                                        적었다가 세 번째 방향 사고)
         *   Samurai_Archer (GREEN)       우향  -> false
         *
         * 런타임이 flipX = artFacesLeft로 유도해 셋 다 적(오른쪽)을 본다.
         * 하드코딩 flip은 어디에도 없다. 새 팩이 들어오면 이 표에 줄을
         * 추가하기 전에 반드시 화면에서 방향부터 확인한다.
         */
        private struct FighterSpec
        {
            public string Id;
            public float LocalX;
            public float LocalY;
            public float Scale;
            public int SortingOrder;
            public bool ArtFacesLeft;
            public float AttackRange;
        }

        private static readonly FighterSpec[] Fighters =
        {
            // LocalY는 전원 0이다 - 플레이어와 같은 발선(땅과 풀의 경계).
            // "뒷줄일수록 Y를 올리는 가짜 깊이"를 두 번 시도했다가 두 번 다
            // 공중 부양으로 읽혀 폐기했다(0.36 -> 0.14 -> 0). 이 게임의 지면은
            // 경계선 하나라 원근이 없다 - 깊이는 X 간격과 정렬 순서만으로
            // 낸다. 동료 발이 로닌 발과 다른 높이면 그것은 깊이가 아니라
            // 버그로 보인다는 것이 네 번의 지시가 가르친 사실이다.
            new FighterSpec {
                Id = PetCatalog.WolfId, LocalX = -1.90f, LocalY = 0f, Scale = 0.78f,
                SortingOrder = SortingOrders.Pet, ArtFacesLeft = true, AttackRange = 2.2f
            },
            new FighterSpec {
                Id = PetCatalog.PandaId, LocalX = -2.50f, LocalY = 0f, Scale = 0.90f,
                SortingOrder = SortingOrders.Pet - 1, ArtFacesLeft = true, AttackRange = 2.9f
            },
            new FighterSpec {
                Id = PetCatalog.ArcherId, LocalX = -2.90f, LocalY = 0f, Scale = 0.90f,
                SortingOrder = SortingOrders.Pet - 2, ArtFacesLeft = false, AttackRange = 3.5f
            }
        };

        /** id -> 실제 시트 폴더 (색상 변형 포함) */
        private static string SheetFolder(string petId)
        {
            if (petId == PetCatalog.WolfId) return "Wolf_Samurai/Sprites/with_outline";
            if (petId == PetCatalog.ArcherId) return "Samurai_Archer/Sprites/GREEN";
            if (petId == PetCatalog.PandaId) return "Samurai_Panda/Sprites";
            return null;
        }

        private const string ArrowPath =
            CharacterRoot + "Samurai_Archer/Sprites/ARROW.png";
        private const string ArrowHitPath =
            CharacterRoot + "Samurai_Archer/Sprites/ARROW HIT.png";

        /** 시트 이름 후보. 팩마다 표기가 다르다 (ATTACK / ATTACK 1) */
        private static readonly string[] IdleSheets = { "IDLE" };
        private static readonly string[] RunSheets = { "RUN" };
        private static readonly string[] AttackSheets = { "ATTACK 1", "ATTACK1", "ATTACK" };

        [MenuItem("Onikiri/Build Pet Content")]
        public static void BuildMenu()
        {
            Build();
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        public static PetSystem Build()
        {
            var battle = GameObject.Find("Battle");
            if (battle == null)
            {
                Debug.LogError("[Onikiri] Battle root missing - run Build Combat Content first.");
                return null;
            }

            // 플레이어 루트는 GroundAnchor 아래다 - 지면 Y를 앵커가 공급하므로
            // 그 아래 있어야 펫도 로컬 Y 0으로 같은 지면에 선다
            var player = battle.transform.Find("GroundAnchor/Player");
            if (player == null) player = battle.transform.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Onikiri] Battle/GroundAnchor/Player missing - run Build Battle Stage first.");
                return null;
            }

            SlicePacks();

            var system = EnsureSystem(battle);
            WireSession(system);
            BuildPetFighters(player, battle);

            Debug.Log(string.Format(
                "[Onikiri] Companion content built: {0} companions, unlock st{1}, gems {2}, "
                + "sum ceiling {3:P0}.",
                PetCatalog.Count, PetCurve.UnlockStage, PetCatalog.TotalUnlockGems,
                PetCatalog.TotalBonusCeiling));
            return system;
        }

        // ---------------------------------------------------------------- 슬라이싱

        /**
         * @brief 세 펫 폴더를 자른다. EvolutionContentBuilder.SlicePacks와 같은
         * 알고리즘이다 - 셀 높이는 다수결, 폭은 GCD, 발밑 여백은 시트마다 실측.
         *
         * 폴더 단위가 다른 이유는 색상 변형이다. 궁수 팩은 BLUE/GREEN/RED 세
         * 폴더가 같은 시트를 들고 있어서 팩 루트를 통째로 재면 GCD는 무사하지만
         * 세 벌을 전부 자르게 된다. 쓰는 폴더만 자른다.
         */
        private static void SlicePacks()
        {
            foreach (var pet in PetCatalog.Pets)
            {
                string folder = CharacterRoot + SheetFolder(pet.Id);
                var paths = new List<string>();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));

                if (paths.Count == 0)
                {
                    Debug.LogWarning("[Onikiri] Pet pack '" + folder + "' has no textures.");
                    continue;
                }

                int cellHeight = DominantHeight(paths);
                if (cellHeight <= 0) continue;

                int cellWidth = 0;
                var sheets = new List<string>();
                foreach (var path in paths)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null || texture.height != cellHeight) continue;
                    sheets.Add(path);
                    cellWidth = cellWidth == 0 ? texture.width : Gcd(cellWidth, texture.width);
                }

                if (cellWidth < 32)
                {
                    Debug.LogWarning(string.Format(
                        "[Onikiri] '{0}' sheet widths reduce to a {1}px cell - slice it by hand.",
                        folder, cellWidth));
                    continue;
                }

                int sliced = 0;
                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (var path in sheets)
                    {
                        int padding = CharacterSpriteSlicer.MeasureFeetPadding(path, cellWidth, cellHeight);
                        if (padding < 0) padding = 0;

                        var pivot = new Vector2(0.5f, padding / (float)cellHeight);
                        if (CharacterSpriteSlicer.SliceGrid(path, cellWidth, cellHeight, pivot)) sliced++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }

                Debug.Log(string.Format("[Onikiri] Sliced {0} pet sheets in '{1}' at {2}x{3}.",
                    sliced, folder, cellWidth, cellHeight));
            }

            // 화살 시트 둘. 캐릭터 셀(96x80)과 높이가 달라 위 GCD를 못 쓰고,
            // 발이 아니라 **중심**으로 나는 물건이라 피벗도 가운데다
            try
            {
                AssetDatabase.StartAssetEditing();
                CharacterSpriteSlicer.SliceGrid(ArrowPath, 48, 16, new Vector2(0.5f, 0.5f));
                CharacterSpriteSlicer.SliceGrid(ArrowHitPath, 48, 16, new Vector2(0.5f, 0.5f));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static int DominantHeight(List<string> paths)
        {
            var counts = new Dictionary<int, int>();
            foreach (var path in paths)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                counts[texture.height] = counts.ContainsKey(texture.height) ? counts[texture.height] + 1 : 1;
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

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief PetSystem을 Battle에 세우고 값을 카탈로그에서 옮겨 적는다.
         *
         * **해금·레벨·액티브는 덮어쓰지 않는다.** 여기서 되돌리면 빌더를 돌릴
         * 때마다 플레이 중인 세이브의 펫이 사라진다 - EquipmentPanelBuilder.
         * EnsureSystem과 같은 규칙이다.
         */
        private static PetSystem EnsureSystem(GameObject battle)
        {
            var system = battle.GetComponent<PetSystem>();
            if (system == null) system = battle.AddComponent<PetSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();

            var slots = so.FindProperty("pets");
            slots.arraySize = PetCatalog.Count;

            for (int i = 0; i < PetCatalog.Count; i++)
            {
                var spec = PetCatalog.Pets[i];
                var element = slots.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("petName").stringValue = spec.Name;
                element.FindPropertyRelative("role").stringValue = spec.Role;
                element.FindPropertyRelative("unlockGems").intValue = spec.UnlockGems;
                element.FindPropertyRelative("attackIntervalSeconds").doubleValue =
                    spec.AttackIntervalSeconds;
                element.FindPropertyRelative("firstBonus").doubleValue = spec.FirstBonus;
                element.FindPropertyRelative("bonusCeiling").doubleValue = spec.BonusCeiling;
                element.FindPropertyRelative("ranged").boolValue = spec.Ranged;

                // 해금/레벨은 건드리지 않는다. 새 칸이면 직렬화 기본값
                // (잠금·Lv.1)이 그대로 들어간다
                var level = element.FindPropertyRelative("level");
                if (level.intValue < 1) level.intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        private static void WireSession(PetSystem system)
        {
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session == null)
            {
                Debug.LogWarning("[Onikiri] GameSession missing - pets will not be saved.");
                return;
            }

            var so = new SerializedObject(session);
            so.FindProperty("petSystem").objectReferenceValue = system;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 전투체

        /**
         * @brief Battle/Player 아래 동료 전투체 셋을 세우고 각자 PetCombat을 배선한다.
         *
         * 규칙은 사무라이(BattleStageBuilder.BuildSamurai)와 같다:
         *   - 이미 있으면 트랜스폼은 건드리지 않는다 (씬 배치는 아트 결정)
         *   - Mecanim Animator는 붙이지 않고, 있으면 지운다 (SpriteAnimator를
         *     조용히 덮어쓰는 사고 - "달리는데 다리가 안 움직인다")
         *   - 튜닝 값은 전부 빌더가 명시적으로 기록한다
         *
         * 방향은 FighterSpec.ArtFacesLeft가 데이터로 들고, 런타임(PetCombat.
         * Awake)이 flipX로 유도한다 - 늑대 팩만 왼쪽을 보고 그려져 있다.
         */
        private static void BuildPetFighters(Transform player, GameObject battle)
        {
            // 단일 출전 시절의 전투체. 이름이 바뀌었으므로 잔재를 지운다 -
            // 남겨두면 "유령 동료"가 씬에 남는다 (성장 패널 유령 행과 같은 사고)
            var legacy = player.Find("Pet");
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);

            var samurai = GameObject.Find("Samurai");
            var spawner = battle.GetComponent<EnemySpawner>()
                          ?? Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);

            foreach (var fighter in Fighters)
            {
                int index = PetCatalog.IndexOf(fighter.Id);
                if (index < 0) continue;
                var spec = PetCatalog.Pets[index];

                string name = "Pet_" + spec.Id.Substring(spec.Id.LastIndexOf('.') + 1);
                var existing = player.Find(name);
                GameObject go;
                if (existing == null)
                {
                    go = new GameObject(name);
                    go.transform.SetParent(player, false);
                }
                else
                {
                    go = existing.gameObject;
                }

                // 사무라이("있으면 트랜스폼을 건드리지 않는다")와 달리 포메이션은
                // **빌더가 강제한다.** 동료 자리는 씬에서 손으로 미는 아트 결정이
                // 아니라 위 FighterSpec 표의 데이터다 - 뭉침을 표에서 고쳤는데
                // 씬이 옛 자리를 들고 있으면 고친 것이 화면에 도달하지 않는다
                go.transform.localPosition = new Vector3(fighter.LocalX, fighter.LocalY, 0f);
                go.transform.localScale = new Vector3(fighter.Scale, fighter.Scale, 1f);

                Object.DestroyImmediate(go.GetComponent<Animator>(), true);

                var renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = fighter.SortingOrder;

                var animator = go.GetComponent<SpriteAnimator>();
                if (animator == null) animator = go.AddComponent<SpriteAnimator>();

                var combat = go.GetComponent<PetCombat>();
                if (combat == null) combat = go.AddComponent<PetCombat>();

                var so = new SerializedObject(combat);
                so.FindProperty("spawner").objectReferenceValue = spawner;
                so.FindProperty("bossFight").objectReferenceValue = battle.GetComponent<BossFight>();
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
                so.FindProperty("playerCombat").objectReferenceValue =
                    samurai != null ? samurai.GetComponent<PlayerCombat>() : null;
                so.FindProperty("hitAudio").objectReferenceValue =
                    Object.FindFirstObjectByType<HitAudio>(FindObjectsInactive.Include);
                so.FindProperty("damageNumbers").objectReferenceValue =
                    Object.FindFirstObjectByType<Onikiri.UI.DamageNumberSpawner>(
                        FindObjectsInactive.Include);

                so.FindProperty("petId").stringValue = spec.Id;
                so.FindProperty("artFacesLeft").boolValue = fighter.ArtFacesLeft;
                so.FindProperty("attackRange").floatValue = fighter.AttackRange;
                so.FindProperty("impactPoint").floatValue = 0.5f;
                so.FindProperty("entranceSpeed").floatValue = 3.2f;
                so.FindProperty("numberTint").colorValue = new Color32(0x9B, 0xE8, 0xD8, 0xFF);

                var idle = LoadClip(SheetFolder(spec.Id), IdleSheets, spec.Name);
                AssignClip(so.FindProperty("idleFrames"), idle);
                AssignClip(so.FindProperty("runFrames"),
                           LoadClip(SheetFolder(spec.Id), RunSheets, spec.Name));
                AssignClip(so.FindProperty("attackFrames"),
                           LoadClip(SheetFolder(spec.Id), AttackSheets, spec.Name));
                so.FindProperty("idleFrameRate").floatValue = 8f;
                so.FindProperty("runFrameRate").floatValue = 12f;
                so.FindProperty("attackFrameRate").floatValue = 14f;

                // 화살은 궁수만 쓰지만 참조 굽기는 값싸다 - 궁수 여부는
                // 런타임이 슬롯(ranged)에서 읽는다
                var arrow = LoadSprites(ArrowPath);
                so.FindProperty("arrowSprite").objectReferenceValue =
                    arrow.Count > 0 ? arrow[0] : null;
                AssignClip(so.FindProperty("arrowHitFrames"), LoadSprites(ArrowHitPath));
                so.FindProperty("arrowHitFrameRate").floatValue = 20f;
                so.FindProperty("arrowSpeed").floatValue = 14f;
                so.FindProperty("arrowMuzzle").vector2Value = new Vector2(0.4f, 0.7f);

                so.ApplyModifiedPropertiesWithoutUndo();

                // 씬에 저장되는 기본 모습. 실행 전 씬에서도 자리가 보이게 idle
                // 첫 프레임을 넣어 두되, 렌더러는 꺼 둔다 - 켜는 것은 해금됐을
                // 때 PetCombat.Refresh의 일이다. 그려진 방향의 반전도 런타임과
                // 같은 유도를 씬에 적어 에디터에서도 적을 보게 한다
                if (idle.Count > 0 && renderer.sprite == null) renderer.sprite = idle[0];
                renderer.flipX = fighter.ArtFacesLeft;
                renderer.enabled = false;
            }
        }

        private static List<Sprite> LoadClip(string folder, string[] names, string label)
        {
            foreach (var name in names)
            {
                var sprites = LoadSprites(CharacterRoot + folder + "/" + name + ".png");
                if (sprites.Count > 0) return sprites;
            }

            Debug.LogWarning(string.Format(
                "[Onikiri] Pet '{0}' has no {1} sheet in {2}.", label, names[0], folder));
            return new List<Sprite>();
        }

        private static List<Sprite> LoadSprites(string path)
        {
            var sprites = new List<Sprite>();
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return sprites;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }

            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites;
        }

        private static void AssignClip(SerializedProperty array, List<Sprite> sprites)
        {
            array.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            if (underscore < 0) return 0;

            int index;
            return int.TryParse(spriteName.Substring(underscore + 1), out index) ? index : 0;
        }

        /** 펫 초상(idle 첫 프레임). PetPanelBuilder가 카드 아이콘으로 쓴다 */
        public static Sprite PortraitOf(string petId)
        {
            var sprites = new List<Sprite>();
            foreach (var name in IdleSheets)
            {
                sprites = LoadSprites(CharacterRoot + SheetFolder(petId) + "/" + name + ".png");
                if (sprites.Count > 0) break;
            }
            return sprites.Count > 0 ? sprites[0] : null;
        }
    }
}
