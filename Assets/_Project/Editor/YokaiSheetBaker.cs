using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 붉은눈 요괴(Inimig 9 colo 2)의 aseprite 클립을 **림 라이트를 구워서**
     * 시트형 보스용 PNG로 만든다.
     *
     * ## 왜 굽는가 — 런타임 셰이더가 아니라
     *
     * 이 요괴는 원본이 흑자줏빛이라 어두운 배경에서 실루엣이 통째로 묻힌다.
     * 로드맵이 rim light를 명시한 이유다 - 자리가 지역 3(사쿠라 밤)으로
     * 확정된 뒤에도 림이 분홍/자주 배경과 실루엣을 갈라준다.
     *
     * 런타임 셰이더로 얹으면 스프라이트 렌더링 경로가 이 보스만 달라진다 - 픽셀
     * 아트 파이프라인(Pixel Perfect, 틴트, 피격 플래시)이 전부 "스프라이트 한 장"
     * 전제 위에 있는데 거기에 예외를 하나 만드는 것이다. 가을숲 지면을 굽는 시점에
     * 한 장으로 만든 것과 같은 판단으로, **빛도 굽는 시점에 픽셀로 넣는다.**
     * 결과물은 평범한 시트라 기존 BossConfig(Sheets) 경로를 그대로 탄다.
     *
     * ## 왜 2배로 굽는가
     *
     * 시트형 보스는 SpawnScale이 1이다("아트가 이미 보스 크기다" -
     * BossConfig.SpawnScale). 이 팩의 원본은 39px로 잡몹 크기라, 그대로 세우면
     * 피날레의 무게가 없다. 확대는 정수만 허용되므로(픽셀 격자 규칙) 2배를
     * **굽는 시점에** 넣는다 - 림 1px도 함께 2px이 되어 아트 픽셀 격자와 맞는다.
     *
     * ## 프레임 정렬은 피벗으로
     *
     * aseprite 임포터는 프레임을 여백 없이 잘라내고(트리밍) 캔버스 위치를 피벗에
     * 남긴다 - 실측으로 피벗이 트림 좌표에서 (16,-56)처럼 나오는 이유다. 런타임
     * 렌더러도 피벗으로 프레임을 놓으므로, **피벗을 한 점에 맞춰 합성하면 화면과
     * 같은 정렬**이 나온다. 캔버스 아래쪽 ~56px의 빈 띠는 격자에 넣지 않는다 -
     * 넣으면 셀 높이의 절반이 여백이 되고 발밑 측정만 헷갈린다.
     */
    public static class YokaiSheetBaker
    {
        public const string SourcePath =
            "Assets/ThirdParty/Enemies/FeudalJapan/Inimig (9) colo 2.aseprite";

        public const string OutputFolder = "Assets/_Project/Art/Bosses/RedEyeYokai";

        public const string IdlePath = OutputFolder + "/IDLE.png";
        public const string WalkPath = OutputFolder + "/WALK.png";
        public const string HurtPath = OutputFolder + "/HURT.png";
        public const string DeathPath = OutputFolder + "/DEATH.png";
        public const string AttackPath = OutputFolder + "/ATTACK.png";

        /**
         * @brief 추가 공격 셋. 오의 블록에 서로 다른 공격이 넷 들어 있다.
         *
         * 한 벌만 쓰면 2초마다 같은 그림이 돌아 금방 벽지가 된다. `Enemy`가
         * 매 주기에 넷 중 하나를 고른다(EnemyDefinition.attackVariants).
         */
        public const string WhipPath = OutputFolder + "/ATTACK_WHIP.png";
        public const string DiscPath = OutputFolder + "/ATTACK_DISC.png";
        public const string WavePath = OutputFolder + "/ATTACK_WAVE.png";

        /** 추가 공격의 시트 경로. BossConfigBuilder가 순서대로 물린다 */
        public static readonly string[] AttackVariantPaths = { WhipPath, DiscPath, WavePath };

        /** 정수만. 픽셀 격자 규칙(11단계)은 굽는 배율에도 똑같이 적용된다 */
        public const int Scale = 2;

        /**
         * @brief 림 색. 지옥 톤의 따뜻한 잔광이다.
         *
         * 차가운 달빛(청백)도 실루엣은 갈라주지만, 지역 4의 광원은 배경의
         * 핏빛이라 빛의 색이 배경과 다른 계열이면 "어디서 오는 빛인가"가 없는
         * 스티커 테두리로 읽힌다. 몸통(흑자줏빛)보다 밝고 배경(짙은 적흑)보다도
         * 밝은 주황 계열로, 눈·칼날의 채도 높은 적과도 구분된다.
         */
        private static readonly Color RimColor = new Color32(0xFF, 0x9A, 0x7A, 0xFF);

        /**
         * @brief 원본 픽셀을 림 색으로 미는 비율.
         *
         * 1.0이면 윤곽이 원본과 무관한 단색 선이 되어 아트가 "테두리 친 그림"이
         * 된다. 원본의 명암을 조금 남겨야 윤곽선이 형태를 따라 굵어지고 얇아진다.
         */
        private const float RimBlend = 0.78f;

        /** 이보다 진하면 불투명으로 본다. 반투명 가장자리에 림이 붙으면 지저분하다 */
        private const float OpaqueAlpha = 0.5f;

        /** 굽고 난 셀 크기 (배율 포함). BossConfigBuilder가 config에 옮겨 적는다 */
        public static int CellWidth { get; private set; }
        public static int CellHeight { get; private set; }

        /**
         * @brief 클립 -> 시트 대응.
         *
         * 태그 이름이 Tag/Tag_0.. 뿐이라 정체는 프레임을 눈으로 확인해 정했다:
         * Tag=대기(15f), Tag_0=내려베기(14f), Tag_1=피격(2f), Tag_2=소멸(6f),
         * Tag_3=활공 돌진(6f), Tag_4=장판기(40f, 안 씀), Tag_5=등장(20f, 안 씀).
         *
         * **걷기는 활공 돌진이다.** 이 요괴는 다리가 없다 - 서서 미끄러지는 것보다
         * 수평으로 날아드는 쪽이 형태에 맞고, 시트형 보스의 걷기 칸은 테스트가
         * 비워두는 것을 막는다(SheetBosses_HaveWalkAndAttackArt).
         */
        private struct ClipBake
        {
            public string ClipName;
            public string OutputPath;

            /**
             * @brief 클립 안에서 잘라 쓸 구간 (양끝 포함). 음수면 클립 전체.
             *
             * 오의 블록(Tag_4)만 이 칸을 쓴다. 40프레임 통째는 8초라 2초 주기에
             * 들어가지 않고, 애초에 **서로 다른 오의 넷을 이어 붙인 것**이라
             * 통째로 돌리면 한 동작으로 읽히지도 않는다.
             */
            public int First;
            public int Last;
        }

        /**
         * @brief 공격에 쓰는 구간 (Tag_4 = 원본 52~91 안에서의 번호).
         *
         * 원본 프레임 64~71에 해당한다. 눈으로 확인한 내용:
         *
         *   64,65  몸이 붉은 줄기로 흩어지기 시작한다 (예비 동작)
         *   66,67  **붉은 초승달이 나간다.** 이 두 장에는 몸이 아예 없다
         *   68,69  흩어진 입자
         *   70,71  칼을 든 몸이 다시 맺힌다 (마무리)
         *
         * 몸이 사라졌다 맺히는 것이 이 여덟 장의 요점이다 - 축지법으로 베고
         * 돌아오는 동작이고, 그래서 초승달이 몸과 겹치지 않는다.
         */
        private const int AttackFirstFrame = 12;   // 52 + 12 = 64
        private const int AttackLastFrame = 19;    // 52 + 19 = 71

        private static readonly ClipBake[] Bakes =
        {
            new ClipBake { ClipName = "Tag",   OutputPath = IdlePath,  First = -1, Last = -1 },
            new ClipBake { ClipName = "Tag_3", OutputPath = WalkPath,  First = -1, Last = -1 },
            new ClipBake { ClipName = "Tag_1", OutputPath = HurtPath,  First = -1, Last = -1 },
            new ClipBake { ClipName = "Tag_2", OutputPath = DeathPath, First = -1, Last = -1 },

            /**
             * 공격은 **오의 블록에서 잘라 온다.** Tag_0(내려베기 14장)이 아니다.
             *
             * Tag_0도 분명한 공격이다 - 칼을 위로 들었다가 호를 그리며 내려벤다.
             * 문제는 그 열넷 어디에도 **이펙트가 그려져 있지 않다**는 것이다.
             * 회색 몸과 붉은 칼날뿐이라 화면에서 "벴다"가 약하고, 그래서 한동안
             * 참격 이펙트를 따로 얹어 보완했다(VfxBurst) - 그림 위에 그림을
             * 겹치는 방식이라 크기와 자리를 계속 손으로 맞춰야 했다.
             *
             * 오의 블록에는 원화가가 **몸과 이펙트를 같은 프레임에 함께** 그려
             * 뒀다. 겹칠 것이 없고 어긋날 것도 없다.
             */
            new ClipBake
            {
                ClipName = "Tag_4", OutputPath = AttackPath,
                First = AttackFirstFrame, Last = AttackLastFrame
            },

            /**
             * 같은 블록의 나머지 셋. 원본 프레임 번호와 그림:
             *
             *   72~77  혈조 - 갈고리처럼 휘감는 채찍. 넷 중 가장 크게 휜다
             *   78~83  혈륜 - 수평으로 쓸고 호를 그린 뒤 칼날이 번쩍인다
             *   84~90  혈파 - 발밑에서 핏빛 장막이 솟아 방울로 떨어진다
             *
             * 혈참과 달리 이 셋은 **몸이 내내 보인다.** 그래서 넷을 섞으면
             * "사라졌다 나타나는" 혈참이 특별해 보이는 효과도 함께 생긴다.
             */
            new ClipBake { ClipName = "Tag_4", OutputPath = WhipPath, First = 20, Last = 25 },
            new ClipBake { ClipName = "Tag_4", OutputPath = DiscPath, First = 26, Last = 31 },
            new ClipBake { ClipName = "Tag_4", OutputPath = WavePath, First = 32, Last = 38 },
        };

        [MenuItem("Onikiri/Art/Bake Red-Eye Yokai Sheets")]
        public static void BakeMenu()
        {
            if (BakeAll())
                Debug.Log(string.Format("[Onikiri] Red-eye yokai sheets baked: cell {0}x{1} (x{2}).",
                    CellWidth, CellHeight, Scale));
        }

        /**
         * @brief 다섯 시트를 다시 굽는다. 소스가 그대로면 결과도 그대로다.
         *
         * @return 다섯 클립이 모두 잡혔으면 true
         */
        public static bool BakeAll()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SourcePath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Onikiri] Yokai source missing: " + SourcePath);
                return false;
            }

            var clips = new Dictionary<string, List<Sprite>>();
            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip == null) continue;
                clips[clip.name] = FramesOf(clip);
            }

            // 셀은 **실제로 굽는 프레임들**의 합집합이다. 오의 블록은 여덟 장만
            // 잘라 쓰므로 나머지 서른둘을 셀 계산에 넣으면 안 된다 - 넣으면
            // 안 쓰는 장판기와 파도까지 셀에 들어가 칸이 두 배가 되고, 그 여백이
            // 위치 기준을 통째로 밀어버린다(SpiritSummon에서 한 번 겪었다)
            var selected = new Dictionary<string, List<Sprite>>();
            var used = new List<Sprite>();
            foreach (var bake in Bakes)
            {
                List<Sprite> frames;
                if (!clips.TryGetValue(bake.ClipName, out frames) || frames.Count == 0)
                {
                    Debug.LogError("[Onikiri] Yokai clip missing: " + bake.ClipName + " in " + SourcePath);
                    return false;
                }

                if (bake.First >= 0)
                {
                    if (bake.Last >= frames.Count)
                    {
                        Debug.LogError(string.Format(
                            "[Onikiri] Yokai clip '{0}' has {1} frames - cannot slice {2}..{3}. " +
                            "The aseprite tags changed.",
                            bake.ClipName, frames.Count, bake.First, bake.Last));
                        return false;
                    }
                    frames = frames.GetRange(bake.First, bake.Last - bake.First + 1);
                }

                selected[bake.OutputPath] = frames;
                used.AddRange(frames);
            }

            float left = float.MaxValue, right = float.MinValue;
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var sprite in used)
            {
                // 피벗 공간에서의 아트 범위. 피벗은 트림 좌표라 음수가 나온다
                float x0 = -sprite.pivot.x;
                float y0 = -sprite.pivot.y;
                if (x0 < left) left = x0;
                if (y0 < bottom) bottom = y0;
                if (x0 + sprite.rect.width > right) right = x0 + sprite.rect.width;
                if (y0 + sprite.rect.height > top) top = y0 + sprite.rect.height;
            }

            int cellW = Mathf.CeilToInt(right - left);
            int cellH = Mathf.CeilToInt(top - bottom);
            CellWidth = cellW * Scale;
            CellHeight = cellH * Scale;

            var readableCache = new Dictionary<Texture2D, Color[]>();
            var sizeCache = new Dictionary<Texture2D, Vector2Int>();

            EnsureFolder(OutputFolder);

            /**
             * 한 줄 스트립이 아니라 **여러 줄 격자**로 굽는다.
             *
             * 임포트 상한이 4096px이라(PixelArtImportSettings) 대기 15프레임
             * x 310px = 4650px짜리 한 줄은 임포터가 리샘플해버리고, 그 순간
             * 셀 나눗셈이 어긋나 SliceGrid가 거부한다. 실제로 그렇게 걸렸다.
             *
             * SliceGrid는 격자를 위에서 아래, 왼쪽에서 오른쪽으로 읽고 빈 칸을
             * 버리므로(참격 5x2 시트와 같은 경로) 줄바꿈이 프레임 순서를 바꾸지
             * 않는다.
             */
            int columns = Mathf.Max(1, 4096 / (cellW * Scale));

            foreach (var bake in Bakes)
            {
                // 위에서 구간까지 잘라둔 것을 쓴다. clips[..]를 다시 읽으면
                // 오의 블록이 마흔 장 통째로 구워진다
                var frames = selected[bake.OutputPath];

                int cols = Mathf.Min(columns, frames.Count);
                int rows = (frames.Count + cols - 1) / cols;
                int sheetW = cols * cellW * Scale;
                int sheetH = rows * cellH * Scale;
                var sheet = new Color[sheetW * sheetH];

                for (int i = 0; i < frames.Count; i++)
                {
                    var cell = new Color[cellW * cellH];
                    Composite(frames[i], cell, cellW, cellH, left, bottom, readableCache, sizeCache);
                    ApplyRim(cell, cellW, cellH);

                    // 시트는 위에서 아래로 읽지만 텍스처 원점은 좌하단이다
                    int row = i / cols;
                    int column = i % cols;
                    BlitScaled(cell, cellW, cellH, sheet, sheetW,
                               column * cellW * Scale, (rows - 1 - row) * cellH * Scale);
                }

                WritePng(bake.OutputPath, sheet, sheetW, sheetH);
            }

            AssetDatabase.Refresh();
            return true;
        }

        /** 클립의 스프라이트 키프레임. BattleContentBuilder.FramesFromClip과 같은 규칙 */
        private static List<Sprite> FramesOf(AnimationClip clip)
        {
            var frames = new List<Sprite>();
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                for (int i = 0; i < keys.Length; i++)
                {
                    var sprite = keys[i].value as Sprite;
                    if (sprite == null) continue;
                    // aseprite는 마지막 프레임 유지용 중복 키를 끝에 하나 더 쓴다
                    if (frames.Count > 0 && frames[frames.Count - 1] == sprite && i == keys.Length - 1)
                        continue;
                    frames.Add(sprite);
                }
            }
            return frames;
        }

        /** 프레임 하나를 셀 버퍼에 피벗 정렬로 얹는다 */
        private static void Composite(Sprite sprite, Color[] cell, int cellW, int cellH,
                                      float left, float bottom,
                                      Dictionary<Texture2D, Color[]> readableCache,
                                      Dictionary<Texture2D, Vector2Int> sizeCache)
        {
            Color[] atlas;
            if (!readableCache.TryGetValue(sprite.texture, out atlas))
            {
                atlas = ReadAtlas(sprite.texture);
                readableCache[sprite.texture] = atlas;
                sizeCache[sprite.texture] = new Vector2Int(sprite.texture.width, sprite.texture.height);
            }
            var size = sizeCache[sprite.texture];

            int srcX = (int)sprite.rect.x;
            int srcY = (int)sprite.rect.y;
            int w = (int)sprite.rect.width;
            int h = (int)sprite.rect.height;

            int dstX = Mathf.RoundToInt(-sprite.pivot.x - left);
            int dstY = Mathf.RoundToInt(-sprite.pivot.y - bottom);

            for (int y = 0; y < h; y++)
            {
                int ty = dstY + y;
                if (ty < 0 || ty >= cellH) continue;
                for (int x = 0; x < w; x++)
                {
                    int tx = dstX + x;
                    if (tx < 0 || tx >= cellW) continue;
                    cell[ty * cellW + tx] = atlas[(srcY + y) * size.x + srcX + x];
                }
            }
        }

        /**
         * @brief 위쪽이 뚫린 불투명 픽셀을 림 색으로 민다.
         *
         * 위 방향 한 겹만 쓴다. 좌우까지 두르면 윤곽 전체가 같은 굵기의 선이 되어
         * 스티커처럼 읽히고, 위만 밝히면 "위에서 오는 빛"이 된다. 좌우 반전(flipX)
         * 에도 위는 위라서 방향이 뒤집히지 않는다 - 굽는 시점에 방향을 넣을 수
         * 있는 유일한 축이다.
         */
        private static void ApplyRim(Color[] cell, int cellW, int cellH)
        {
            for (int y = 0; y < cellH; y++)
            {
                for (int x = 0; x < cellW; x++)
                {
                    var pixel = cell[y * cellW + x];
                    if (pixel.a < OpaqueAlpha) continue;

                    bool openAbove = y == cellH - 1 || cell[(y + 1) * cellW + x].a < OpaqueAlpha;
                    if (!openAbove) continue;

                    cell[y * cellW + x] = Color.Lerp(pixel, RimColor, RimBlend);
                }
            }
        }

        /** 1배 셀을 시트의 (dstX, dstY) 자리에 Scale배 최근접으로 얹는다. dst는 배율 좌표다 */
        private static void BlitScaled(Color[] cell, int cellW, int cellH,
                                       Color[] sheet, int sheetW, int dstX, int dstY)
        {
            for (int y = 0; y < cellH; y++)
            {
                for (int x = 0; x < cellW; x++)
                {
                    var pixel = cell[y * cellW + x];
                    for (int sy = 0; sy < Scale; sy++)
                    {
                        int ty = dstY + y * Scale + sy;
                        for (int sx = 0; sx < Scale; sx++)
                            sheet[ty * sheetW + dstX + x * Scale + sx] = pixel;
                    }
                }
            }
        }

        /** 임포터가 읽기를 막아둔 아틀라스를 RenderTexture 경유로 읽는다 */
        private static Color[] ReadAtlas(Texture2D texture)
        {
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = copy.GetPixels();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        private static void WritePng(string path, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
