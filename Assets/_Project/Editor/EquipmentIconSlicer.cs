using System.Collections.Generic;
using Onikiri.Progression;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief Kyrise 아이템 시트에 칼과 방패 두 칸을 **덧붙여** 자른다.
     *
     * ## 왜 팩이 준 스프라이트를 그대로 못 쓰는가
     *
     * 이 시트는 손으로 대충 잘려 있다. 133개 중 스물세 개의 렉트가 두세 칸을
     * 덮고(가장 큰 것은 192x192), 무기와 방어구가 있는 아래쪽 두 줄이 정확히
     * 그 구역이다 - 129~131번 렉트 하나가 책 두 권과 검 두 자루를 함께 담고
     * 있다. 아이콘 자리에 넣으면 네 개가 한꺼번에 들어간다.
     *
     * ## 왜 통째로 다시 자르지 않는가
     *
     * `ISpriteEditorDataProvider.SetSpriteRects`는 **넘긴 목록으로 교체한다.**
     * 격자로 다시 자르면 기존 이름이 전부 사라지는데, 이 시트에는 31단계의
     * 보석(`spritesheet_16x16_74`)과 퀘스트 책(`_23`)이 살고 있다. 화면 두 곳이
     * 조용히 빈다.
     *
     * 그래서 **읽고, 두 개를 더하고, 다시 쓴다.** 이름표(ISpriteNameFileIdData
     * Provider)도 같이 유지한다 - 없으면 재슬라이싱마다 GUID가 바뀌어 기존
     * 참조가 깨진다(CharacterSpriteSlicer가 같은 이유로 유지한다).
     */
    public static class EquipmentIconSlicer
    {
        public const string SheetPath = UiIcons.ItemSheet;

        /**
         * @brief 시트에서 잘라낼 칸. 16px 격자의 좌하단 원점 좌표다.
         *
         * 눈으로 골랐고 좌표를 여기 적어 둔다. 팩이 바뀌면 이 값이 다른 그림을
         * 가리키게 되므로, 자르기 전에 그 칸이 **비어 있지 않은지** 확인한다 -
         * 빈 칸을 자르면 화면에 아무것도 없는 아이콘이 뜨고, 그것은 "아이콘이
         * 빠졌다"가 아니라 "아이콘이 있는데 안 보인다"로 읽힌다.
         */
        private struct Cut
        {
            public string Name;
            public int X, Y;
        }

        private const int Cell = 16;

        private static readonly Cut[] Cuts =
        {
            // 아래에서 첫 줄, 다섯째 칸. 흰 날 + 금 손잡이
            new Cut { Name = UiSprites.WeaponSprite, X = 64, Y = 0 },

            // 아래에서 넷째 줄, 셋째 칸. 붉은 원형 방패 (대장간 톤의 적)
            new Cut { Name = UiSprites.ArmorSprite, X = 32, Y = 48 }
        };

        [MenuItem("Onikiri/Art/Slice Equipment Icons")]
        public static void SliceMenu()
        {
            Slice();
        }

        /** 이미 있으면 아무것도 하지 않는다. 빌더가 매번 불러도 안전하다 */
        public static bool Slice()
        {
            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Onikiri] Item sheet missing: " + SheetPath);
                return false;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) return false;

            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>(provider.GetSpriteRects());
            var byName = new Dictionary<string, SpriteRect>();
            foreach (var rect in rects) byName[rect.name] = rect;

            bool changed = false;
            foreach (var cut in Cuts)
            {
                if (byName.ContainsKey(cut.Name)) continue;

                rects.Add(new SpriteRect
                {
                    name = cut.Name,
                    rect = new Rect(cut.X, cut.Y, Cell, Cell),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                });
                changed = true;
            }

            if (!changed) return true;

            provider.SetSpriteRects(rects.ToArray());

            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null)
            {
                var pairs = new List<SpriteNameFileIdPair>();
                foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                names.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();

            Debug.Log("[Onikiri] Equipment icons sliced into " + SheetPath
                      + " (" + Cuts.Length + " added, " + rects.Count + " total).");
            return true;
        }
    }
}
