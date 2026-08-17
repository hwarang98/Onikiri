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
            new Cut { Name = UiSprites.ArmorSprite, X = 32, Y = 48 },

            // ---------------------------------------------------------- 44단계: 요도
            //
            // 시트는 아이콘 하나를 **다섯 칸의 색 변형**으로 늘어놓는다(실측:
            // 격자 인덱스 5개마다 같은 그림). 맨 아랫줄이 칼 두 벌 x 다섯 색이라,
            // 요도 넷은 새 아트 없이 같은 칼의 네 색으로 선다 - 35단계 지역 4가
            // 배경을 재틴트로 세운 것과 같은 판단이다.
            //
            // 색을 요괴에 맞췄다. 흑야도만 검은 날이 없어서 틴트로 만든다
            // (YodoSpec.IconTint) - 은색 날에 짙은 보라를 곱한다.
            new Cut { Name = YodoSprites.LanternSprite,      X = 176, Y = 0 },   // 금 (등불)
            new Cut { Name = YodoSprites.ExecutionerSprite,  X = 144, Y = 0 },   // 은 (강철)
            new Cut { Name = YodoSprites.RedEyeSprite,       X = 192, Y = 0 },   // 적 (붉은눈)
            new Cut { Name = YodoSprites.DarkSamuraiSprite,  X = 160, Y = 0 },   // 은 + 청 손잡이

            // 혼 = 보랏빛 보석. 상단 바의 보석(청 다이아)과 형태는 같고 색이
            // 다르다 - 같은 "재화"라는 것이 형태로, 다른 재화라는 것이 색으로
            // 읽힌다(31단계 보석 아이콘과 같은 줄에서 골랐다)
            new Cut { Name = YodoSprites.SoulSprite,  X = 112, Y = 208 },

            // 파편 = 회색 팔면체. 보석·혼과 달리 광택이 없는 돌덩이라
            // "재료"로 읽힌다
            new Cut { Name = YodoSprites.ShardSprite, X = 208, Y = 192 },

            // ---------------------------------------------------- 47단계: 전설 妖刀
            //
            // **아랫줄의 다른 두 모양이다.** 사양의 "새 아트 최소(기존 시트
            // 재활용·틴트)"가 이 자리를 가리키고, 44단계가 요도 넷을 같은
            // 자리에서 세운 방법을 그대로 잇는다.
            //
            // ## 좌표를 눈이 아니라 **잉크 픽셀 수로** 골랐다
            //
            // 44단계 주석이 "아랫줄이 칼 두 벌 x 다섯 색"이라고 적었는데,
            // 실측해 보니 세 벌이다. 셀마다 알파가 있는 픽셀을 세면 모양이
            // 그대로 갈린다:
            //
            //     x=0,16,32          잉크 88   모양 A (3색)
            //     x=48..112          잉크 116  모양 B (5색) - 64는 장비 무기
            //     x=128..192         잉크 103  모양 C (5색) - 44단계 요도 넷 + 128
            //     x=208              잉크 140  **칼이 아니다**
            //
            // 처음에 128과 208을 골랐다가 실기 캡처에서 물렸다 - 208이
            // 두루마리 같은 원통이라 도감에 **칼이 아닌 것**이 섰다. 44단계
            // 주석의 "두 벌"을 믿고 남은 칸을 세었던 것이 원인이고, 25단계의
            // "채움 색은 언제나 렌더 실측"이 여기서도 그대로다.
            //
            // ## 둘 다 은색 바탕을 고른 이유
            //
            // 곱연산 틴트는 **원본에 없는 채널을 못 만든다**(35단계). 백면의
            // 창백한 청백도 천수의 자주도 R·G·B가 다 필요하므로 금색이나
            // 붉은 바탕에서는 안 나온다 - 흑야도가 은색 날에 보라를 곱해
            // 검푸른 날이 된 것과 같은 자리다.
            //
            // 모양 A와 B의 은색을 하나씩 쓰면 색과 형태가 **동시에** 갈리고,
            // 도감에서 전설 두 줄이 보스 요도 넷(전부 모양 C)과 다른 계열로
            // 읽힌다.
            new Cut { Name = LegendaryYodoSprites.WhiteMaskSprite,    X = 0,  Y = 0 },
            new Cut { Name = LegendaryYodoSprites.ThousandHandSprite, X = 80, Y = 0 }
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
                var wanted = new Rect(cut.X, cut.Y, Cell, Cell);

                SpriteRect existing;
                if (byName.TryGetValue(cut.Name, out existing))
                {
                    // **좌표가 다르면 고친다.** 47단계 전에는 "있으면 건너뛴다"
                    // 뿐이라 한 번 잘못 자른 칸을 코드로 되돌릴 방법이 없었다 -
                    // 실제로 전설 요도 하나가 칼이 아닌 칸을 물었고, 상수를
                    // 고쳐도 화면이 안 바뀌었다.
                    //
                    // spriteID는 유지한다. 새로 만들면 GUID가 바뀌어 그 스프라이트를
                    // 가리키던 씬 참조가 통째로 끊긴다(CharacterSpriteSlicer가
                    // 이름표를 유지하는 것과 같은 이유).
                    if (existing.rect == wanted) continue;

                    existing.rect = wanted;
                    changed = true;
                    continue;
                }

                rects.Add(new SpriteRect
                {
                    name = cut.Name,
                    rect = wanted,
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
