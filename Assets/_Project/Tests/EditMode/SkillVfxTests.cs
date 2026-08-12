using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 신규 오의의 이펙트(49단계). **팩 색을 버리고 혈(血) 계열로 되물들였는가.**
     *
     * 그림이 예쁜지는 테스트가 못 본다. 볼 수 있는 것은 셋이다:
     *
     *   있는가       여덟 오의가 가리키는 클립이 실제로 라이브러리에 있는가
     *   같은 계열인가 구운 픽셀이 혈 램프 위에 있는가 (팩의 청록·금이 안 남았는가)
     *   맞는 크기인가 정수 배율 · 화면 폭 · 쿨다운 안에서 끝나는 길이
     *
     * 두 번째가 이 파일의 존재 이유다. 램프를 지나지 않은 프레임이 한 장이라도
     * 섞이면 화면에 청록 소용돌이가 뜨는데, 그것은 오의가 아니라 **다른 게임의
     * 이펙트**로 읽힌다 - 그리고 그 사고는 다시 구울 때까지 아무도 모른다.
     */
    public class SkillVfxTests
    {
        /**
         * 경로를 다시 적는 이유는 YokaiVfxTests와 같다 - 테스트 어셈블리가
         * 에디터 스크립트를 참조할 수 없다.
         */
        const string YokaiLibraryPath = "Assets/_Project/Data/VfxLibrary_Yokai.asset";
        const string PozacLibraryPath = "Assets/_Project/Data/VfxLibrary_Pozac.asset";

        const string PozacFolder = "Assets/_Project/Art/VFX/Pozac";

        /** 화면 폭 (월드 단위). 32 PPU · 216px 기준 해상도 */
        const float ScreenWidth = 6.75f;

        static VfxLibrary.Clip Find(string id)
        {
            foreach (var path in new[] { YokaiLibraryPath, PozacLibraryPath })
            {
                var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);
                if (library == null) continue;

                var clip = library.Find(id);
                if (clip != null) return clip;
            }
            return null;
        }

        /**
         * @brief 여덟 오의 중 이펙트를 가리키는 것들이 **전부 실재하는가.**
         *
         * 이름이 어긋나면 SkillPerformer가 참격 없이 시전한다 - 데미지는
         * 들어가고 화면에는 몸 동작만 남는다. 그것은 버그로 안 읽히고
         * "이 오의는 원래 밋밋한가 보다"로 읽힌다.
         */
        [Test]
        public void EverySkillEffect_ExistsAndHasFrames()
        {
            int wired = 0;

            foreach (var skill in SkillCatalog.Skills)
            {
                if (string.IsNullOrEmpty(skill.VfxId)) continue;
                wired++;

                var clip = Find(skill.VfxId);
                Assert.IsNotNull(clip, string.Format(
                    "'{0}'이 가리키는 이펙트 '{1}'이 라이브러리에 없다 - "
                    + "Onikiri/Art/Bake Pozac VFX 와 Harvest Yokai VFX 를 돌려라",
                    skill.DisplayName, skill.VfxId));

                Assert.IsNotNull(clip.frames, skill.DisplayName + "의 이펙트에 프레임 배열이 없다");
                Assert.Greater(clip.frames.Length, 0,
                    skill.DisplayName + "의 이펙트가 0프레임이다");

                foreach (var frame in clip.frames)
                    Assert.IsNotNull(frame, skill.DisplayName + "의 이펙트에 빈 프레임이 있다 - "
                                     + "시트를 다시 자르면 채워진다");
            }

            Assert.AreEqual(5, wired,
                "이펙트를 가리키는 오의가 다섯이 아니다 - 49단계의 신규 다섯이 그 몫이다");
        }

        /**
         * @brief 클립 한 번이 그 오의의 **쿨다운 안에서 끝나는가.**
         *
         * 넘치면 이전 이펙트가 사라지기 전에 다음 것이 뜨고, 화면에 붉은 것이
         * 계속 떠 있게 되어 "지금 친다"는 신호가 배경이 된다 - 요괴 참격에
         * 걸어둔 것과 같은 검사이고 같은 이유다(YokaiVfxTests).
         *
         * 여유를 크게 잡는다. 오의는 요괴와 달리 **여덟이 서로 다른 쿨다운으로
         * 겹쳐 도는데**, 가장 짧은 것이 6초이고 클립은 0.5초 안팎이라 이 검사는
         * 사실상 "실수로 60프레임을 물리지 않았는가"를 잡는다.
         */
        [Test]
        public void EverySkillEffect_FinishesWellInsideItsCooldown()
        {
            foreach (var skill in SkillCatalog.Skills)
            {
                if (string.IsNullOrEmpty(skill.VfxId)) continue;

                var clip = Find(skill.VfxId);
                if (clip == null) continue;

                Assert.Greater(clip.frameRate, 0f, skill.DisplayName + "의 재생 속도가 0이다");

                Assert.Less(clip.Seconds, skill.CooldownSeconds * 0.25d, string.Format(
                    "'{0}'의 이펙트가 {1:F2}초인데 쿨다운은 {2}초다 - 화면에 붉은 것이 "
                    + "계속 떠 있으면 그것은 신호가 아니라 배경이다",
                    skill.DisplayName, clip.Seconds, skill.CooldownSeconds));
            }
        }

        /**
         * @brief 배율이 **정수**이고 화면을 안 덮는가.
         *
         * 정수 배율은 11단계 픽셀 격자 규칙이다 - 소수를 곱하면 어떤 픽셀은
         * 7개, 어떤 픽셀은 8개로 그려져 격자가 눈에 띄게 일그러진다.
         *
         * 폭은 48단계가 초승달에서 두 번 물린 자리다. 팩의 128px 캔버스는
         * 참격 팩(64px)의 두 배라, 무심코 2배를 주면 화면 폭(6.75u)을 통째로
         * 덮는다.
         */
        [Test]
        public void EverySkillEffect_KeepsThePixelGridAndFitsTheScreen()
        {
            foreach (var skill in SkillCatalog.Skills)
            {
                if (string.IsNullOrEmpty(skill.VfxId)) continue;

                var clip = Find(skill.VfxId);
                if (clip == null || clip.frames == null || clip.frames.Length == 0) continue;

                Assert.AreEqual(Mathf.Round(clip.scale), clip.scale, 1e-4f, string.Format(
                    "'{0}'의 배율이 {1}이다 - 정수가 아니면 픽셀 격자가 일그러진다",
                    skill.DisplayName, clip.scale));

                Assert.GreaterOrEqual(clip.scale, 1f, skill.DisplayName + "의 배율이 1 미만이다");

                float width = clip.frames[0].rect.width * clip.scale
                              / clip.frames[0].pixelsPerUnit;

                Assert.LessOrEqual(width, ScreenWidth, string.Format(
                    "'{0}'의 이펙트가 {1:F2}u로 화면 폭 {2:F2}u를 덮는다 - 배율을 한 칸 내려라",
                    skill.DisplayName, width, ScreenWidth));
            }
        }

        /**
         * @brief 구운 Pozac 조각이 **혈 램프 위에 있는가.**
         *
         * 램프를 지났으면 모든 픽셀이 붉은 계열이다 - 원본이 청록(E22)이든
         * 금색(E31)이든 흰색(E30)이든 상관없이. 48단계가 몸과 이펙트를 색으로
         * 가를 때 쓴 것과 같은 자다: `r - g`가 충분히 큰가.
         *
         * 그 자를 여기서 다시 쓰는 이유는 두 자산이 **같은 화면에 함께 뜨기**
         * 때문이다. 하베스트 조각(#4D0A29~#B32849)은 r-g가 67~139이고, 램프의
         * 어느 마디도 그보다 낮지 않아야 한 계열로 읽힌다.
         *
         * 알파가 옅은 가장자리는 뺀다 - 팩은 가장자리를 반투명으로 그리는데
         * 그 픽셀의 RGB는 화면에서 배경과 섞이므로 계열을 정하지 않는다.
         */
        [Test]
        public void PozacEffects_AreRecoloredIntoTheBloodPalette()
        {
            string[] files = { "WAVE_RING.png", "BURST.png", "VORTEX.png" };

            foreach (var file in files)
            {
                string path = PozacFolder + "/" + file;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, "구운 Pozac 시트가 없다: " + path
                                 + " (Onikiri/Art/Bake Pozac VFX)");

                var pixels = ReadPixels(texture);
                int opaque = 0, blood = 0;
                int worstRed = 255, worstGreen = 0;

                foreach (var pixel in pixels)
                {
                    if (pixel.a < 0.6f) continue;
                    opaque++;

                    int red = Mathf.RoundToInt(pixel.r * 255f);
                    int green = Mathf.RoundToInt(pixel.g * 255f);
                    int blue = Mathf.RoundToInt(pixel.b * 255f);

                    // 붉은 계열의 정의: 적색이 녹색보다 확실히 크고, 청색이
                    // 적색을 넘지 않는다. 청록은 첫 조건에서, 금색은 둘째에서 걸린다
                    if (red - green >= 40 && blue <= red) blood++;
                    else if (red - green < worstRed - worstGreen) { worstRed = red; worstGreen = green; }
                }

                Assert.Greater(opaque, 100, file + ": 불투명 픽셀이 거의 없다 - 굽기가 비었다");

                float ratio = blood / (float)opaque;
                Assert.Greater(ratio, 0.98f, string.Format(
                    "{0}: 불투명 픽셀의 {1:P1}만 붉은 계열이다 (가장 먼 픽셀 R{2} G{3}). "
                    + "램프를 안 지난 프레임이 섞였다 - 화면에 다른 계열의 이펙트가 뜬다",
                    file, ratio, worstRed, worstGreen));
            }
        }

        /** 임포터가 읽기를 막아둔 텍스처를 RenderTexture 경유로 읽는다 */
        static Color[] ReadPixels(Texture2D texture)
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

        /**
         * @brief 신규 오의의 안무가 **씬에 배선돼 있는가.**
         *
         * 카탈로그에 줄을 더하고 빌더를 안 돌리면 그 오의는 시전이 거절된다
         * (SkillPerformer.Cast가 안무 없는 오의를 거절한다). 화면에서는
         * "그 오의만 안 나간다"로 나타나고, 쿨다운은 계속 도므로 원인이
         * 안 읽힌다.
         */
        [Test]
        public void EverySkill_HasChoreographyInTheScene()
        {
            // 씬이 열려 있으면 그것을 쓰고, 아니면 **덧붙여 연다.**
            // 테스트 러너는 씬을 내려둔 채로 도는 일이 있어서, 열려 있기만
            // 기다리면 이 검사가 조용히 건너뛰어진다 - 건너뛴 검사는 없는 검사다.
            //
            // Additive다. Single로 열면 저장 안 된 다른 빌더의 결과가 통째로
            // 날아간다(34단계에서 물린 자리 - BattleContentBuilder 주석)
            var performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            bool opened = false;

            if (performer == null)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    MainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                opened = scene.IsValid();
                performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            }

            Assert.IsNotNull(performer, "전투 씬에 SkillPerformer가 없다 - "
                             + "Onikiri/Scene/Build Combat Content 를 돌려라");

            var so = new SerializedObject(performer);
            var list = so.FindProperty("choreographies");

            Assert.AreEqual(SkillCatalog.Count, list.arraySize, string.Format(
                "안무가 {0}개인데 오의는 {1}개다 - Build Combat Content 를 돌려라",
                list.arraySize, SkillCatalog.Count));

            var ids = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                string id = element.FindPropertyRelative("id").stringValue;

                Assert.AreEqual(SkillCatalog.Skills[i].Id, id,
                    "안무 " + i + "번이 표와 다른 오의를 가리킨다");
                Assert.IsTrue(ids.Add(id), "안무에 '" + id + "'가 두 번 있다");

                Assert.Greater(element.FindPropertyRelative("clip").arraySize, 0,
                    SkillCatalog.Skills[i].DisplayName + "의 몸 클립이 비었다");
            }

            // 덧붙여 연 씬은 도로 닫는다. 안 닫으면 다음 검사가 오브젝트를
            // 두 벌 보게 되고, 그 사고는 순서에 따라 나타났다 사라진다
            if (opened)
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                    UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
        }

        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
    }
}
