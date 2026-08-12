using NUnit.Framework;
using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 무기 참격 티어 (51단계). **순수 연출이 순수 연출로 남아 있는가.**
     *
     * 그림이 화려한지는 테스트가 못 본다. 볼 수 있는 것은 넷이다:
     *
     *   램프가 서 있는가   티어가 오르면 겹·알파가 줄지 않는다 (업그레이드 보상)
     *   금지 색이 없는가   초록(팩 color1)은 어느 티어에도 안 선다
     *   시트가 은백인가    스파크 굽기가 램프를 안 지났는가 (틴트가 성립하는 조건)
     *   씬에 배선됐는가    귀참의 티어 다섯 벌 + 스파크 라이브러리 참조
     *
     * 밸런스 검사가 없는 것이 이 스텝의 요점이다 - WeaponVfxTier는 색·알파·겹
     * 수만 내놓고, 그 값은 어느 데미지 경로에도 곱해지지 않는다. 세이브 검사도
     * 없다 - 티어는 장비 등급에서 매번 파생되므로 저장할 것이 없다.
     */
    public class WeaponVfxTierTests
    {
        const string PozacLibraryPath = "Assets/_Project/Data/VfxLibrary_Pozac.asset";
        const string PozacFolder = "Assets/_Project/Art/VFX/Pozac";
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        /** 강제 스위치는 프로세스 전역이라, 남으면 다음 테스트가 다른 티어를 본다 */
        [TearDown]
        public void ClearForcedState()
        {
            WeaponVfxTier.DebugForcedTier = 0;
            WeaponVfxTier.DebugForcedPremium = -1;
        }

        // ---------------------------------------------------------------- 램프

        /**
         * @brief 티어가 오르면 겹과 알파가 **줄지 않는다.**
         *
         * 한 칸이라도 내려가면 그 등급업의 순간 화면이 수수해지고, 이 스텝이
         * 팔던 "등급을 올렸다 = 화면이 화려해졌다"가 그 자리에서 거짓말이 된다.
         */
        [Test]
        public void SparkAndGlow_NeverShrinkAsTierRises()
        {
            for (int tier = WeaponVfxTier.MinTier; tier < WeaponVfxTier.MaxTier; tier++)
            {
                Assert.LessOrEqual(
                    WeaponVfxTier.SparkLayers(tier, false),
                    WeaponVfxTier.SparkLayers(tier + 1, false),
                    "티어 " + tier + " -> " + (tier + 1) + "에서 스파크 겹이 줄었다");

                Assert.LessOrEqual(
                    WeaponVfxTier.GlowAlpha(tier, false),
                    WeaponVfxTier.GlowAlpha(tier + 1, false),
                    "티어 " + tier + " -> " + (tier + 1) + "에서 평타 오라가 옅어졌다");

                Assert.LessOrEqual(
                    WeaponVfxTier.StreakBlend(tier),
                    WeaponVfxTier.StreakBlend(tier + 1),
                    "티어 " + tier + " -> " + (tier + 1) + "에서 섬광 틴트가 옅어졌다");
            }
        }

        /** 티어1이 수수해야 위 검사가 뜻을 가진다 - 시작점은 "지금까지의 화면"이다 */
        [Test]
        public void TierOne_IsTheGameBeforeThisStep()
        {
            Assert.AreEqual(0, WeaponVfxTier.SparkLayers(1, false), "티어1에 스파크가 있다");
            Assert.AreEqual(0f, WeaponVfxTier.GlowAlpha(1, false), "티어1에 평타 오라가 있다");
            Assert.AreEqual(0f, WeaponVfxTier.StreakBlend(1), "티어1이 섬광을 물들인다");
        }

        /** 오니키리 완성은 언제나 한 겹을 더 얹는다 - 프리미엄의 정의다 */
        [Test]
        public void Premium_AlwaysAddsExactlyOneLayer()
        {
            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                Assert.AreEqual(
                    WeaponVfxTier.SparkLayers(tier, false) + 1,
                    WeaponVfxTier.SparkLayers(tier, true),
                    "티어 " + tier + "에서 프리미엄이 정확히 한 겹을 더하지 않는다");
            }
        }

        /**
         * @brief 상시 연출(평타 오라)의 절제 상한. 0.6을 넘으면 타격점이
         *        "찍힌다"가 아니라 "번진다"로 읽힌다 (W-3의 절제 규칙).
         */
        [Test]
        public void GlowAlpha_StaysRestrained()
        {
            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                Assert.LessOrEqual(WeaponVfxTier.GlowAlpha(tier, false), 0.6f);
                Assert.LessOrEqual(WeaponVfxTier.GlowAlpha(tier, true), 0.6f);
            }
        }

        // ---------------------------------------------------------------- 색

        /**
         * @brief 초록(팩 color1)은 어느 티어에도 안 선다.
         *
         * ImpactSpark가 못 박은 규칙이다 - 초록은 먹빛·적·벚꽃 팔레트와
         * 충돌한다. 팩에 다섯 색이 있다고 다섯을 다 쓰는 것이 아니다.
         */
        [Test]
        public void SlashColors_NeverUseGreen()
        {
            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                int color = WeaponVfxTier.SlashColorOf(tier);
                Assert.AreNotEqual(1, color, "티어 " + tier + "가 초록(color1)을 쓴다");
                Assert.IsTrue(color >= 2 && color <= 5,
                    "티어 " + tier + "의 색 번호 " + color + "가 팩 범위(2~5) 밖이다");
            }
        }

        /** 꼭대기는 흑적(color2)이다 - "등급5 = 진한 적/먹"이 이 스텝의 사양이다 */
        [Test]
        public void TopTier_IsTheBlackRedOfThisGame()
        {
            Assert.AreEqual(2, WeaponVfxTier.SlashColorOf(WeaponVfxTier.MaxTier));
        }

        /**
         * @brief 스파크 틴트에 검은 계열이 없다. **곱 틴트는 밝기를 못 만든다** -
         *        어두운 틴트를 곱하면 은백 시트가 통째로 꺼진다.
         */
        [Test]
        public void SparkTints_AreBrightEnoughToRead()
        {
            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                var tint = WeaponVfxTier.SparkTint(tier, false);
                float max = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                Assert.GreaterOrEqual(max, 0.8f,
                    "티어 " + tier + "의 틴트가 어둡다 - 은백 시트가 꺼진다");
            }

            var premium = WeaponVfxTier.PremiumTint;
            Assert.GreaterOrEqual(Mathf.Max(premium.r, Mathf.Max(premium.g, premium.b)), 0.8f);
        }

        // ---------------------------------------------------------------- 강제 스위치

        /** 테스트 패널의 강제가 실제로 이긴다 - 이것이 없으면 GIF 비교가 불가능하다 */
        [Test]
        public void ForcedTier_OverridesTheRealGrade()
        {
            WeaponVfxTier.DebugForcedTier = 4;
            Assert.AreEqual(4, WeaponVfxTier.CurrentTier());

            WeaponVfxTier.DebugForcedPremium = 1;
            Assert.IsTrue(WeaponVfxTier.IsPremium());

            WeaponVfxTier.DebugForcedPremium = 0;
            Assert.IsFalse(WeaponVfxTier.IsPremium());
        }

        /** 씬 없는 기본값 - 전투 전용 테스트 씬에서 참격이 사라지면 안 된다 */
        [Test]
        public void WithoutSystems_FallsToTierOne()
        {
            // EquipmentSystem.Instance가 없는 에디트 모드가 곧 그 상황이다
            if (Onikiri.Progression.EquipmentSystem.Instance != null)
                Assert.Ignore("씬에 EquipmentSystem이 떠 있다 - 이 검사는 빈 씬에서만 뜻이 있다");

            Assert.AreEqual(WeaponVfxTier.MinTier, WeaponVfxTier.CurrentTier());
        }

        // ---------------------------------------------------------------- 굽기

        /**
         * @brief 스파크 두 조각이 라이브러리에 있고, 이름이 SkillPerformer의
         *        상수와 같다. 갈리면 스파크 겹이 조용히 안 뜬다.
         */
        [Test]
        public void SparkClips_ExistUnderTheNamesThePerformerUses()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(PozacLibraryPath);
            Assert.IsNotNull(library, "Pozac 라이브러리가 없다 - Onikiri/Art/Bake Pozac VFX");

            foreach (var id in new[] { SkillPerformer.WeaponGlowSparkBurst,
                                       SkillPerformer.WeaponGlowSparkRay })
            {
                var clip = library.Find(id);
                Assert.IsNotNull(clip, "스파크 클립 '" + id + "'이 라이브러리에 없다 - "
                                 + "Onikiri/Art/Bake Pozac VFX 를 다시 돌려라");
                Assert.Greater(clip.frames.Length, 0, id + "가 0프레임이다");
                Assert.AreEqual(Mathf.Round(clip.scale), clip.scale, 1e-4f,
                    id + "의 배율이 정수가 아니다");
            }
        }

        /**
         * @brief 스파크 시트가 **은백인가.** 회색이 아닌 픽셀이 섞였다는 것은
         *        혈 램프를 지났다는 뜻이고, 그러면 티어1의 청·티어2의 보라를
         *        영영 못 낸다 - 틴트가 성립하는 조건 그 자체를 검사한다.
         */
        [Test]
        public void SparkSheets_AreBakedSilver()
        {
            string[] files = { "SPARK_BURST.png", "SPARK_RAY.png" };

            foreach (var file in files)
            {
                string path = PozacFolder + "/" + file;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, "스파크 시트가 없다: " + path
                                 + " (Onikiri/Art/Bake Pozac VFX)");

                var pixels = ReadPixels(texture);
                int opaque = 0, gray = 0;

                foreach (var pixel in pixels)
                {
                    if (pixel.a < 0.6f) continue;
                    opaque++;

                    float spread = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b))
                                 - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                    if (spread < 0.06f) gray++;
                }

                Assert.Greater(opaque, 30, file + ": 불투명 픽셀이 거의 없다 - 굽기가 비었다");
                Assert.Greater(gray / (float)opaque, 0.98f,
                    file + ": 회색이 아닌 픽셀이 섞였다 - 은백 굽기(RecolorSilver)를 안 지났다");
            }
        }

        /** SkillVfxTests와 같은 우회 - 임포터가 읽기를 막아둔 텍스처를 읽는다 */
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

        // ---------------------------------------------------------------- 씬 배선

        /**
         * @brief 귀참의 티어 다섯 벌과 스파크 라이브러리가 씬에 배선됐는가.
         *
         * 배선이 빠져도 화면은 안 깨진다(SlashFramesFor가 기본 참격으로
         * 떨어진다) - 그래서 화면이 못 잡고 테스트가 잡아야 한다. 티어별
         * 시트가 실제로 다른 색인지는 스프라이트 이름의 색 번호로 확인한다.
         */
        [Test]
        public void OniCleave_HasFiveTierSlashesInTheScene()
        {
            var performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            bool opened = false;

            if (performer == null)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    MainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                opened = scene.IsValid();
                performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            }

            Assert.IsNotNull(performer, "전투 씬에 SkillPerformer가 없다 - Build Combat Content");

            var so = new SerializedObject(performer);

            Assert.IsNotNull(so.FindProperty("glowLibrary").objectReferenceValue,
                "SkillPerformer에 스파크 라이브러리가 배선되지 않았다 - Build Combat Content");

            var list = so.FindProperty("choreographies");
            SerializedProperty oniCleave = null;

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("id").stringValue
                    == Onikiri.Progression.SkillCatalog.OniCleaveId)
                {
                    oniCleave = element;
                    break;
                }
            }

            Assert.IsNotNull(oniCleave, "귀참의 안무가 씬에 없다 - Build Combat Content");

            var tiers = oniCleave.FindPropertyRelative("slashTierFrames");
            Assert.AreEqual(WeaponVfxTier.MaxTier, tiers.arraySize,
                "귀참의 티어 변형이 다섯 벌이 아니다 - Build Combat Content");

            for (int tier = WeaponVfxTier.MinTier; tier <= WeaponVfxTier.MaxTier; tier++)
            {
                var frames = tiers.GetArrayElementAtIndex(tier - 1).FindPropertyRelative("frames");
                Assert.Greater(frames.arraySize, 0, "티어 " + tier + "의 참격이 0프레임이다");

                var first = frames.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
                Assert.IsNotNull(first, "티어 " + tier + "의 첫 프레임이 비었다");

                string expected = "_color" + WeaponVfxTier.SlashColorOf(tier) + "_";
                StringAssert.Contains(expected, first.name, string.Format(
                    "티어 {0}의 참격이 '{1}'이다 - 램프({2})와 다른 색 시트가 배선됐다",
                    tier, first.name, expected));
            }

            if (opened)
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                    UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
        }
    }
}
