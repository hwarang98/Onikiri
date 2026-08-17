using System.Collections.Generic;
using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 다크 사무라이(Inimig 9 colo 2)의 **안 쓰던 40프레임 오의 블록**에서
     * 이펙트만 뜯어내 재사용 가능한 참격 시트로 만든다.
     *
     * ## 왜 이 블록인가
     *
     * 이 시트의 일곱 태그 중 `Tag_4`(52~91, 40프레임)는 어디에도 안 걸려 있었다.
     * 눈으로 확인해 보니 한 덩어리 애니가 아니라 **서로 다른 오의 네 개를 이어
     * 붙인 것**이고, 그 안에 이 게임이 아직 갖고 있지 않은 그림이 들어 있다 -
     * 붉은 초승달 참격, 피의 파도, 채찍처럼 휘어지는 갈고리.
     *
     * 요괴 하나의 연출로 40프레임을 통째로 재생하면 그것은 8초짜리 장판기이고,
     * 방치형 전투의 2초 주기에 들어갈 자리가 없다. 그래서 **통째로 쓰지 않고
     * 조각으로 뜯는다.** 뜯어낸 조각은 이 보스와 무관하게 어디서든 쓸 수 있다.
     *
     * ## 몸과 이펙트를 색으로 가른다
     *
     * 프레임에는 요괴의 몸이 함께 그려져 있어서 그대로 쓰면 참격이 나갈 때마다
     * 작은 사무라이가 하나 더 뜬다. 다행히 이 팩의 팔레트는 **일곱 색**뿐이고
     * 두 무리로 딱 갈라진다:
     *
     *   이펙트(붉은 계열)  #4D0A29  #90133B  #B32849   -> r-g = 67, 125, 139
     *   몸(자줏빛 회색)    #191627  #39344A  #58495E   -> r-g =  3,   5,  15
     *
     * 그래서 `r - g`가 임계값을 넘는 픽셀만 남기면 몸이 통째로 빠진다. 실측으로
     * 초승달 프레임은 몸 픽셀이 아예 0이고, 파도 프레임은 3162px 중 2442px이
     * 남는다(나머지가 몸이다). 알파 채널을 손으로 그릴 필요가 없었다.
     *
     * 경계값을 40으로 둔 이유는 두 무리 사이가 15와 67로 **비어 있기** 때문이다.
     * 그 사이 아무 값이나 같은 결과를 내고, 40은 양쪽에서 멀다.
     *
     * ## 배율은 굽지 않는다
     *
     * 보스 시트(YokaiSheetBaker)는 2배로 굽지만 이쪽은 원본 크기 그대로다.
     * 참격의 크기는 **부르는 쪽이 정하는 값**이고(PackSlash.Play의 scale),
     * 구운 배율과 런타임 배율이 둘 다 있으면 2배가 두 번 곱해진다. 팩 참격과
     * 같은 규칙으로 맞춰 둔다 - 시트는 1배, 화면 크기는 호출자가.
     */
    public static class YokaiVfxBaker
    {
        /**
         * 보스가 쓰는 것과 **같은 파일**이다. 색 변형본이 두 개 있는데
         * (`Inimig (9)`와 `(9) colo 2`) 화면에 서는 쪽에서 뜯어야 참격과 보스의
         * 붉은색이 같은 색이 된다. 실측으로 두 파일의 붉은 계열은 한 톤 다르다
         */
        public const string SourcePath = YokaiSheetBaker.SourcePath;

        /** 오의 블록 클립. 일곱 태그가 전부 이름이 `Tag`라 임포터가 붙인 순번이다 */
        private const string SuperClipName = "Tag_4";

        /** 이 클립의 프레임 수. 다르면 아래 프레임 번호가 전부 어긋난 것이다 */
        private const int SuperClipFrames = 40;

        /**
         * 대소문자가 기존 폴더와 **정확히** 같아야 한다(`VFX`, `Vfx` 아님).
         * 윈도우 파일 시스템은 둘을 같은 폴더로 보므로 여기서 틀려도 로컬에서는
         * 아무 일도 일어나지 않고, 대소문자를 가리는 빌드 머신에서만 경로가
         * 깨진다 - 처음에 `Vfx`로 적었다가 실제로 `VFX/Yokai`에 만들어졌다.
         */
        public const string OutputFolder = "Assets/_Project/Art/VFX/Yokai";

        /** 이 값을 넘는 `r - g`만 이펙트로 본다. 위 주석의 팔레트 표 참고 */
        private const int RedKey = 40;

        /**
         * @brief 뜯어낼 조각 하나.
         *
         * 프레임 번호는 **오의 클립 안에서의 번호**다. aseprite 파일 전체의 번호로
         * 적으면 태그가 하나 움직이는 순간 전부 틀리는데, 그것은 임포트 로그에
         * 아무것도 남기지 않고 조용히 엉뚱한 그림을 굽는다.
         */
        private struct Harvest
        {
            public string Id;
            public string FileName;

            /** 오의 클립 기준 시작·끝 (양끝 포함) */
            public int First;
            public int Last;

            /**
             * @brief 재생 기본값. 라이브러리 애셋의 씨앗이다.
             *
             * 여기 적어두는 이유는 조각마다 알맞은 값이 다르고, 그 값이
             * **뜯어낼 때 이미 정해지기** 때문이다 - 네 장짜리 초승달과 다섯
             * 장짜리 파도는 같은 속도로 돌리면 하나는 스치고 하나는 늘어진다.
             */
            public float FrameRate;
            public float Scale;
            public float Angle;
            public float ForwardOffset;
            public float HeightOffset;
        }

        /**
         * @brief 조각 셋. 전부 눈으로 확인하고 정했다.
         *
         * - 혈참(crescent): 칼이 지나간 자리에 뜨는 붉은 초승달. 꽉 찬 한 장 ->
         *   얇아진 잔상 -> 흩어지는 입자. 네 장이 그대로 참격 한 번이다.
         * - 혈파(wave): 아래에서 솟는 핏빛 장막. 뱀처럼 풀리며 방울로 떨어진다.
         * - 혈조(whip): 갈고리처럼 휘어 감기는 채찍. 셋 중 가장 크게 휜다.
         *
         * 오의 블록에는 이 셋 말고 수평 원반 쓸기(74~75)와 돌진 잔상(78~79)도
         * 있다. 지금 안 뜯는 이유는 쓸 자리가 없어서지 그림이 나빠서가 아니다 -
         * 필요해지면 이 표에 줄을 하나 더 적으면 된다.
         */
        private static readonly Harvest[] Harvests =
        {
            /**
             * @brief 자리는 **칼날에서 실측했다.** 눈대중으로 두 번 틀린 자리다.
             *
             * 요괴의 원점은 발밑이다(`Enemy.RestingY`가 발을 지면에 앉힌다).
             * 그래서 두 값은 "발밑에서 칼날까지"가 된다. 처음에 0.7로 뒀다가
             * 참격이 지면 띠에 파묻혔고, 1.4로 올렸다가 이번엔 머리 위로 떴다.
             * 눈대중을 두 번 반복하는 대신 원본 aseprite에서 쟀다.
             *
             * 공격 클립(Tag_0, 프레임 16~29)에서 **붉은 계열 픽셀만** 골라내면
             * 그것이 칼날이다(몸은 자줏빛 회색 - 팔레트 표 참고). 그 무리의
             * 중심을 재면:
             *
             *   발밑 위로   +11 ~ +26 px  (평균 +17)  -> x2 굽기 / 32 PPU = 1.06u
             *   몸 중심에서  +3.5 ~ +12 px (평균  +6)  -> x2 굽기 / 32 PPU = 0.38u
             *
             * (가로는 원본이 오른쪽을 보고 그려져 음수로 나오는데, 화면에서는
             *  뒤집혀 왼쪽을 보므로 부호가 뒤집혀 앞쪽이 된다.)
             *
             * 이 요괴의 그려진 키가 x2 굽기 뒤에도 3.25u뿐이라, 1.4는 몸 밖이었다.
             *
             * ## 높이는 칼날, 가로는 **칼날 + 호의 반지름**
             *
             * 세로는 잰 값(1.06)을 그대로 쓴다. 가로는 그렇지 않다.
             *
             * 참격 스프라이트의 피벗은 한가운데인데 그려진 것은 **초승달**이라,
             * 칼이 있던 자리는 그림의 한가운데가 아니라 **오목한 쪽 끝**이다.
             * 잰 값(0.38)을 그대로 쓰면 호의 한가운데가 칼날에 놓여 요괴가
             * 자기 참격에 파묻힌다 - 실제로 그렇게 나왔다.
             *
             * 그래서 호의 반지름만큼(폭 3.56u의 절반) 더 민다. 1.8이면 오목한
             * 끝이 칼날에 닿고 볼록한 쪽이 플레이어 쪽으로 뻗는다. 2.4까지
             * 밀면 요괴에서 떨어져 나가 참격이 아니라 날아가는 것으로 읽힌다.
             *
             * 16fps면 0.25초 - 보스의 2초 주기 안에서 한 번 번쩍이고 사라진다
             */
            new Harvest
            {
                Id = CrescentId, FileName = "CRESCENT.png", First = 14, Last = 17,
                FrameRate = 16f, Scale = 1f, Angle = 0f,
                ForwardOffset = 1.8f, HeightOffset = 1.06f
            },

            // 채찍은 크게 휘어 감기는 그림이라 조금 느리게 돌린다
            new Harvest
            {
                Id = "whip", FileName = "WHIP.png", First = 20, Last = 24,
                FrameRate = 14f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0.8f, HeightOffset = 1.3f
            },

            // 파도는 아래에서 솟는 그림이라 다른 둘보다 낮게 세운다
            new Harvest
            {
                Id = "wave", FileName = "WAVE.png", First = 32, Last = 36,
                FrameRate = 14f, Scale = 1f, Angle = 0f,
                ForwardOffset = 0.7f, HeightOffset = 1.1f
            },
        };

        /** 구운 조각의 셀 크기. 조각마다 다르므로 라이브러리 빌더가 읽어 간다 */
        public static readonly Dictionary<string, Vector2Int> BakedCells =
            new Dictionary<string, Vector2Int>();

        public static string PathFor(string id)
        {
            foreach (var harvest in Harvests)
                if (harvest.Id == id) return OutputFolder + "/" + harvest.FileName;
            return null;
        }

        public static IEnumerable<string> Ids
        {
            get
            {
                foreach (var harvest in Harvests) yield return harvest.Id;
            }
        }

        public const string LibraryPath = "Assets/_Project/Data/VfxLibrary_Yokai.asset";

        /**
         * @brief 붉은 초승달의 이름. **Inimig(9)의 시그니처다.**
         *
         * 상수로 두는 이유는 이 이름을 쓰는 곳이 둘이고, 둘이 어긋나면
         * 화면에서만 드러나기 때문이다:
         *
         *   - `Boss_RedEyeYokai`(= 화면 이름 "다크 사무라이")의 attackVfxId
         *   - 흑야 영체의 시그니처 참격 (YodoPanelBuilder)
         *
         * 둘은 **같은 요괴**다. 하나는 서 있는 보스이고 하나는 그 보스를 불러낸
         * 것이라, 화면에서 다른 참격을 뿜으면 같은 놈으로 안 읽힌다.
         *
         * 다른 보스는 이 이름을 쓰지 않는다 - 이것은 공용 참격이 아니라
         * 한 요괴의 서명이다.
         */
        public const string CrescentId = "crescent";

        [MenuItem("Onikiri/Art/Harvest Yokai VFX")]
        public static void HarvestMenu()
        {
            if (!BakeAll()) return;

            var library = BuildLibrary();
            Debug.Log(string.Format("[Onikiri] Yokai VFX harvested: {0} clip(s), library {1}.",
                BakedCells.Count, library != null ? "written" : "FAILED"));
        }

        /**
         * @brief 구운 시트를 읽어 `VfxLibrary` 애셋을 다시 쓴다.
         *
         * 씨앗만 깔고 손을 떼는 BossConfig와 달리 **매번 덮어쓴다.** 이 애셋에
         * 손으로 적을 것이 없기 때문이다 - 클립은 시트에서 나오고 배율·각도는
         * 위 표에서 나온다. 값을 바꾸고 싶으면 표를 고치는 것이 맞고, 그래야
         * "왜 이 값인가"가 주석과 함께 남는다.
         */
        public static VfxLibrary BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var clips = new List<VfxLibrary.Clip>();
            foreach (var harvest in Harvests)
            {
                string path = OutputFolder + "/" + harvest.FileName;
                var frames = OrderedSprites(path);
                if (frames.Length == 0)
                {
                    Debug.LogError("[Onikiri] Yokai VFX sheet has no sprites: " + path);
                    return null;
                }

                clips.Add(new VfxLibrary.Clip
                {
                    id = harvest.Id,
                    frames = frames,
                    frameRate = harvest.FrameRate,
                    scale = harvest.Scale,
                    angle = harvest.Angle,
                    forwardOffset = harvest.ForwardOffset,
                    heightOffset = harvest.HeightOffset
                });
            }

            var so = new SerializedObject(library);
            var array = so.FindProperty("clips");
            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = clips[i].id;
                element.FindPropertyRelative("frameRate").floatValue = clips[i].frameRate;
                element.FindPropertyRelative("scale").floatValue = clips[i].scale;
                element.FindPropertyRelative("angle").floatValue = clips[i].angle;
                element.FindPropertyRelative("forwardOffset").floatValue = clips[i].forwardOffset;
                element.FindPropertyRelative("heightOffset").floatValue = clips[i].heightOffset;

                var frames = element.FindPropertyRelative("frames");
                frames.arraySize = clips[i].frames.Length;
                for (int f = 0; f < clips[i].frames.Length; f++)
                    frames.GetArrayElementAtIndex(f).objectReferenceValue = clips[i].frames[f];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(library);

            // **디스크까지 밀어야 한다.** SetDirty는 메모리의 애셋에 표시만 하고,
            // 그 표시는 유니티가 알아서 저장할 때까지 파일에 닿지 않는다. 처음
            // 만들 때는 CreateAsset이 즉시 쓰기 때문에 멀쩡해 보이는데, **값을
            // 고쳐 다시 구우면 조용히 옛 값이 남는다** - 실제로 참격 높이를
            // 고치고 다시 구웠는데 파일이 그대로여서 드러났다
            AssetDatabase.SaveAssets();

            foreach (var clip in clips)
                Debug.Log(string.Format("[Onikiri] VFX clip '{0}': {1} frames @{2}fps, scale {3}.",
                    clip.id, clip.frames.Length, clip.frameRate, clip.scale));

            return library;
        }

        /** 시트에서 잘린 스프라이트를 시트 순서대로. BossContentBuilder와 같은 규칙 */
        private static Sprite[] OrderedSprites(string sheetPath)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                var sprite = asset as Sprite;
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites.ToArray();
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            int value;
            if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out value)) return value;
            return 0;
        }

        /**
         * @brief 세 조각을 다시 굽고 격자로 자른다. 소스가 그대로면 결과도 그대로다.
         *
         * @return 셋 다 굽고 잘랐으면 true
         */
        public static bool BakeAll()
        {
            var frames = SuperClipFramesOf();
            if (frames == null) return false;

            BakedCells.Clear();
            EnsureFolder(OutputFolder);

            var readableCache = new Dictionary<Texture2D, Color[]>();

            foreach (var harvest in Harvests)
            {
                if (!BakeOne(harvest, frames, readableCache)) return false;
            }

            AssetDatabase.Refresh();
            return true;
        }

        /** 오의 클립의 스프라이트 40장. 클립이 없거나 길이가 다르면 null */
        private static List<Sprite> SuperClipFramesOf()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SourcePath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Onikiri] Yokai VFX source missing: " + SourcePath);
                return null;
            }

            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip == null || clip.name != SuperClipName) continue;

                var frames = FramesOf(clip);

                // 길이가 다르면 태그 구성이 바뀐 것이다. 그대로 진행하면 엉뚱한
                // 프레임을 굽고, 그것은 다음에 화면을 볼 때까지 아무도 모른다
                if (frames.Count != SuperClipFrames)
                {
                    Debug.LogError(string.Format(
                        "[Onikiri] Yokai super clip '{0}' has {1} frames, expected {2}. " +
                        "The aseprite tags changed - re-check the frame ranges in YokaiVfxBaker.",
                        SuperClipName, frames.Count, SuperClipFrames));
                    return null;
                }
                return frames;
            }

            Debug.LogError("[Onikiri] Yokai super clip '" + SuperClipName + "' missing in " + SourcePath);
            return null;
        }

        private static bool BakeOne(Harvest harvest, List<Sprite> superFrames,
                                    Dictionary<Texture2D, Color[]> readableCache)
        {
            var frames = superFrames.GetRange(harvest.First, harvest.Last - harvest.First + 1);

            // 프레임들을 **하나의 캔버스에 피벗으로 정렬**한다. 임포터가 여백을
            // 잘라내고 그 위치를 피벗에 남기므로, 피벗을 한 점에 맞춰야 프레임
            // 사이에서 이펙트가 떨리지 않는다 (YokaiSheetBaker와 같은 규칙)
            float left = float.MaxValue, bottom = float.MaxValue;
            float right = float.MinValue, top = float.MinValue;
            foreach (var sprite in frames)
            {
                float x0 = -sprite.pivot.x;
                float y0 = -sprite.pivot.y;
                if (x0 < left) left = x0;
                if (y0 < bottom) bottom = y0;
                if (x0 + sprite.rect.width > right) right = x0 + sprite.rect.width;
                if (y0 + sprite.rect.height > top) top = y0 + sprite.rect.height;
            }

            int canvasW = Mathf.CeilToInt(right - left);
            int canvasH = Mathf.CeilToInt(top - bottom);

            var cells = new List<Color[]>();
            foreach (var sprite in frames)
            {
                var cell = new Color[canvasW * canvasH];
                Composite(sprite, cell, canvasW, canvasH, left, bottom, readableCache);
                KeepRedOnly(cell);
                cells.Add(cell);
            }

            // 몸을 걷어내고 남은 것의 경계로 다시 자른다. 걷어내기 전 캔버스는
            // 요괴가 서 있던 자리까지 포함하고 있어서, 그대로 두면 셀의 절반이
            // 빈 칸이 되고 참격의 중심이 그림 밖에 찍힌다
            int x0c = canvasW, y0c = canvasH, x1c = -1, y1c = -1;
            foreach (var cell in cells)
            {
                for (int y = 0; y < canvasH; y++)
                {
                    for (int x = 0; x < canvasW; x++)
                    {
                        if (cell[y * canvasW + x].a <= 0f) continue;
                        if (x < x0c) x0c = x;
                        if (x > x1c) x1c = x;
                        if (y < y0c) y0c = y;
                        if (y > y1c) y1c = y;
                    }
                }
            }

            if (x1c < 0)
            {
                Debug.LogError("[Onikiri] Yokai VFX '" + harvest.Id + "' keyed out to nothing - " +
                               "check the RedKey threshold against the palette.");
                return false;
            }

            int cellW = x1c - x0c + 1;
            int cellH = y1c - y0c + 1;

            // 한 줄 가로 스트립이다. 조각이 다섯 장을 넘지 않아 4096px 상한
            // (PixelArtImportSettings)에 닿지 않는다 - 보스 시트가 줄바꿈을
            // 해야 했던 것과 다른 점이다
            int sheetW = cellW * cells.Count;
            var sheet = new Color[sheetW * cellH];

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                for (int y = 0; y < cellH; y++)
                {
                    for (int x = 0; x < cellW; x++)
                        sheet[y * sheetW + i * cellW + x] = cell[(y0c + y) * canvasW + x0c + x];
                }
            }

            string path = OutputFolder + "/" + harvest.FileName;
            WritePng(path, sheet, sheetW, cellH);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // 피벗은 셀 한가운데다. 참격은 발이 땅에 닿는 그림이 아니라 허공에
            // 놓이는 그림이고, 부르는 쪽이 중심을 기준으로 자리를 잡는다
            if (!CharacterSpriteSlicer.SliceGrid(path, cellW, cellH, new Vector2(0.5f, 0.5f)))
            {
                Debug.LogError("[Onikiri] Yokai VFX '" + harvest.Id + "' failed to slice at "
                               + cellW + "x" + cellH + ".");
                return false;
            }

            BakedCells[harvest.Id] = new Vector2Int(cellW, cellH);

            Debug.Log(string.Format(
                "[Onikiri] Yokai VFX '{0}': {1} frames, cell {2}x{3} -> {4}",
                harvest.Id, cells.Count, cellW, cellH, path));

            return true;
        }

        /** 붉은 계열이 아닌 픽셀을 지운다. 위 주석의 팔레트 표 참고 */
        private static void KeepRedOnly(Color[] cell)
        {
            for (int i = 0; i < cell.Length; i++)
            {
                var pixel = cell[i];
                if (pixel.a <= 0f) continue;

                int red = Mathf.RoundToInt(pixel.r * 255f);
                int green = Mathf.RoundToInt(pixel.g * 255f);
                if (red - green < RedKey) cell[i] = default(Color);
            }
        }

        /** 클립의 스프라이트 키프레임. YokaiSheetBaker.FramesOf와 같은 규칙 */
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

        /** 프레임 하나를 캔버스에 피벗 정렬로 얹는다 */
        private static void Composite(Sprite sprite, Color[] cell, int cellW, int cellH,
                                      float left, float bottom,
                                      Dictionary<Texture2D, Color[]> readableCache)
        {
            Color[] atlas;
            if (!readableCache.TryGetValue(sprite.texture, out atlas))
            {
                atlas = ReadAtlas(sprite.texture);
                readableCache[sprite.texture] = atlas;
            }

            int atlasWidth = sprite.texture.width;
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
                    cell[ty * cellW + tx] = atlas[(srcY + y) * atlasWidth + srcX + x];
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
