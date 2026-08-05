using System.Collections.Generic;
using System.IO;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 벚꽃잎 스프라이트 시트와 프리팹을 만들고 전투에 배선한다.
     *
     * 아트를 코드로 굽는 이유는 크기 때문이다. 필요한 것은 1~2px짜리 조각 몇 개이고,
     * 그 크기에서는 그림 파일을 관리하는 비용이 아트 자체보다 크다. 어떤 모양인지도
     * 파일을 열어봐야 알 수 있는데, 여기 적어두면 코드가 곧 아트 명세가 된다.
     *
     * 시트는 흰색으로 굽는다. 색은 SpriteRenderer.color로 런타임에 입힌다. 그래야
     * 사양서의 두 분홍(#D9A7B0 / #F0CDD3)을 한 시트로 쓸 수 있고, 나중에 스테이지별로
     * 다른 색(단풍, 눈)을 넣을 때 아트를 다시 굽지 않아도 된다.
     */
    public static class SakuraContentBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/VFX";
        private const string PetalSheetPath = ArtFolder + "/Sakura_Petals.png";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PetalPrefabPath = PrefabFolder + "/SakuraPetal.prefab";

        /** 셀 하나의 크기. 꽃잎은 최대 2px이므로 4px 셀이면 여유가 충분하다 */
        private const int Cell = 4;

        /**
         * @brief 꽃잎 네 모양. 1이 그려지는 픽셀이다.
         *
         * 행은 위에서 아래 순이다. 크기를 섞은 것은 의도적이다 - 전부 같은 크기면
         * 여덟 개가 한 덩어리로 읽히고, 크기가 다르면 거리가 다른 것처럼 보여
         * 얕은 깊이가 생긴다.
         */
        private static readonly string[][] Shapes =
        {
            // 1px 티끌. 가장 흔하고 가장 멀리 있는 것처럼 보인다
            new[] { "0000",
                    "0100",
                    "0000",
                    "0000" },

            // 2px 가로. 흩날리는 방향이 읽힌다
            new[] { "0000",
                    "0110",
                    "0000",
                    "0000" },

            // 3px 꺾인 조각. 꽃잎 한 장의 실루엣
            new[] { "0000",
                    "0110",
                    "0100",
                    "0000" },

            // 2x2 덩어리. 가장 가깝고 가장 무겁게 떨어진다
            new[] { "0000",
                    "0110",
                    "0110",
                    "0000" }
        };

        [MenuItem("Onikiri/Art/Build Sakura Petals")]
        public static void BuildArt()
        {
            BuildSheet();
            BuildPrefab();
            AssetDatabase.SaveAssets();
        }

        /**
         * @brief 네 모양을 가로로 늘어놓은 16x4 시트를 굽는다.
         *
         * 이미 있으면 다시 굽지 않는다. 재임포트가 일어나면 프리팹이 들고 있는
         * 스프라이트 참조가 끊길 수 있고, 이 아트는 바뀔 이유가 없다.
         */
        public static void BuildSheet()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(PetalSheetPath) != null) return;

            EnsureFolder(ArtFolder);

            int width = Cell * Shapes.Length;
            var texture = new Texture2D(width, Cell, TextureFormat.RGBA32, false);

            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[width * Cell];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int shape = 0; shape < Shapes.Length; shape++)
            {
                var rows = Shapes[shape];
                for (int row = 0; row < Cell; row++)
                {
                    for (int column = 0; column < Cell; column++)
                    {
                        if (rows[row][column] != '1') continue;

                        // 문자열은 위에서 아래로 읽지만 텍스처 좌표의 원점은 좌하단이다
                        int x = shape * Cell + column;
                        int y = Cell - 1 - row;
                        pixels[y * width + x] = Color.white;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(PetalSheetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(PetalSheetPath, ImportAssetOptions.ForceUpdate);

            // 꽃잎은 회전하며 날아가므로 피벗이 조각의 한가운데여야 한다. 모서리에
            // 있으면 회전이 제자리 돌기가 아니라 궤도 돌기가 되어, 1px 조각이 눈에
            // 띄게 흔들린다
            CharacterSpriteSlicer.SliceGrid(PetalSheetPath, Cell, Cell, new Vector2(0.5f, 0.5f));

            Debug.Log("[Onikiri] Baked " + Shapes.Length + " sakura petal sprites at " +
                      Cell + "x" + Cell + " -> " + PetalSheetPath);
        }

        public static SakuraPetal BuildPrefab()
        {
            EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PetalPrefabPath);
            if (existing != null) return existing.GetComponent<SakuraPetal>();

            var root = new GameObject("SakuraPetal");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = SortingOrders.Vfx;
            root.AddComponent<SakuraPetal>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PetalPrefabPath);
            Object.DestroyImmediate(root);

            return prefab.GetComponent<SakuraPetal>();
        }

        /**
         * @brief 씬의 사무라이에 꽃잎 발생기를 붙이고 배선한다.
         *
         * BattleContentBuilder가 전투를 배선한 뒤에 부른다.
         */
        public static SakuraBurst Wire(Transform vfxParent)
        {
            BuildSheet();
            var petalPrefab = BuildPrefab();

            var samurai = GameObject.Find("Samurai");
            if (samurai == null)
            {
                Debug.LogError("[Onikiri] Samurai not found - cannot wire the sakura burst.");
                return null;
            }

            var burst = samurai.GetComponent<SakuraBurst>();
            if (burst == null) burst = samurai.AddComponent<SakuraBurst>();

            var so = new SerializedObject(burst);
            so.FindProperty("petalPrefab").objectReferenceValue = petalPrefab;
            so.FindProperty("petalParent").objectReferenceValue = vfxParent;

            var sprites = OrderedPetalSprites();
            var spriteArray = so.FindProperty("petalSprites");
            spriteArray.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                spriteArray.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];

            // 사양서의 값을 여기 기록한다. 스크립트 기본값에 맡기면 컴포넌트가 씬에
            // 있는 순간부터 코드를 고쳐도 반영되지 않는다
            so.FindProperty("paleColor").colorValue = new Color32(0xD9, 0xA7, 0xB0, 0xFF);
            so.FindProperty("brightColor").colorValue = new Color32(0xF0, 0xCD, 0xD3, 0xFF);
            so.FindProperty("minPetals").intValue = 6;
            so.FindProperty("maxPetals").intValue = 10;
            so.FindProperty("lifetimeRange").vector2Value = new Vector2(0.4f, 0.6f);
            so.FindProperty("lifetimeBudgetPerSecond").floatValue = 0.5f;
            // 예산 초과분은 수명이 아니라 개수로 흡수한다. 1~2px 조각은 0.25초보다
            // 짧게 살면 흩날림이 아니라 점멸로 보인다. SakuraBurst 참고
            so.FindProperty("minLifetime").floatValue = 0.25f;
            so.FindProperty("minPetalsFloor").intValue = 3;
            so.FindProperty("prewarm").intValue = 40;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (sprites.Count == 0)
                Debug.LogWarning("[Onikiri] Sakura sheet produced no sprites - petals will not appear.");

            return burst;
        }

        private static List<Sprite> OrderedPetalSprites()
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PetalSheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
