using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onikiri.DevTools;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 72단계 UI 잔챙이 (70단계 실기 기록 항목).
     *
     *   1  "레벨업 n" 세 자리 넘침    -> 100 이상이면 "레벨업 99+" (표시만)
     *   2  인수 확인 팝업 아래 여백    -> 클라우드 팝업 셋의 창 = 내용 + 고정 여백
     *   3  희귀 "+40" 설명 줄          -> 넘친 혼 정수는 "등급 하락"이 아닌 다른 줄
     *   4  "소환 중..."                -> 아틀라스 안의 글자
     *   5  프리셋 v23 필드             -> 소환 경험치 둘을 찍고 감사한다
     */
    public class UiLeftoversStep72Tests
    {
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        const string CharsetPath = "Assets/_Project/Data/FontCharset.txt";
        const string StringsPath = "Assets/_Project/Data/UIStrings.txt";

        // ---------------------------------------------------------------- 1 레벨업 99+

        [Test]
        public void LevelUpText_CapsAtNinetyNinePlus()
        {
            Assert.AreEqual("레벨업", LevelHud.LevelUpText(1), "하나면 개수를 안 적는다");
            Assert.AreEqual("레벨업 2", LevelHud.LevelUpText(2));
            Assert.AreEqual("레벨업 99", LevelHud.LevelUpText(99), "99는 그대로 99다");
            Assert.AreEqual("레벨업 99+", LevelHud.LevelUpText(100), "100부터 99+");
            Assert.AreEqual("레벨업 99+", LevelHud.LevelUpText(272), "70단계 실기의 272");
        }

        /**
         * 칸 폭은 그대로(210)이고 "레벨업 99+"는 44pt로 231px라 넘는다("272"와 같은 폭).
         * 씬의 버튼에 캡션 글꼴이 배선돼 있고, 캡션으로는 칸에 든다.
         */
        [Test]
        public void LevelUpButton_HasACaptionFallbackThatFits()
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                Assert.IsNotNull(safeArea, "씬에 SafeArea가 없다");
                var hud = safeArea.root.GetComponentInChildren<LevelHud>(true);
                Assert.IsNotNull(hud, "씬에 LevelHud가 없다");

                var so = new UnityEditor.SerializedObject(hud);
                var label = so.FindProperty("levelUpLabel").objectReferenceValue as TMPro.TMP_Text;
                var caption = so.FindProperty("levelUpCaptionFont").objectReferenceValue as TMPro.TMP_FontAsset;
                Assert.IsNotNull(label, "레벨업 글자 배선이 비었다");
                Assert.IsNotNull(caption, "레벨업 캡션 글꼴이 비었다 - Build Combat Content(또는 72단계 배선)");

                float room = label.rectTransform.rect.width;
                Assert.AreEqual(210f, room, 0.5f, "레벨업 칸 폭이 바뀌었다");
                Assert.LessOrEqual(label.GetPreferredValues(LevelHud.LevelUpText(99)).x, room,
                                   "두 자리가 본 글꼴로 칸을 넘는다");

                var font = label.font;
                float size = label.fontSize;
                try
                {
                    label.font = caption;
                    label.fontSize = PixelFontSizes.GalmuriCaption;
                    Assert.LessOrEqual(label.GetPreferredValues(LevelHud.LevelUpText(272)).x, room,
                                       "캡션으로 내려도 99+가 칸을 넘는다");
                }
                finally
                {
                    label.font = font;
                    label.fontSize = size;
                }
            }
            finally
            {
                if (opened)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                        UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
            }
        }

        // ---------------------------------------------------------------- 3 넘침 줄

        static GachaSystem.PullResult Overflow()
        {
            return new GachaSystem.PullResult
            {
                Outcome = GachaCurve.Outcome.SoulEssence,
                Grade = GachaCurve.GradeFor(GachaCurve.Outcome.SoulEssence),
                Shards = YodoCurve.ShardsPerOverflowSoul,
                BladeIndex = -1,
                LegendaryIndex = -1,
                Downgraded = false
            };
        }

        [Test]
        public void OverflowNote_IsNotTheDowngradeLine()
        {
            string note = GachaResultPopup.OverflowNote(YodoCurve.ShardsPerOverflowSoul);
            Assert.AreEqual("혼 정수 초과 · 파편 +40", note);
            Assert.IsFalse(note.StartsWith(GachaResultPopup.DowngradePrefix),
                           "넘침은 하락이 아니다(68단계) - 다른 문구여야 한다");
        }

        [Test]
        public void OnlyAHomelessEssence_IsAnOverflow()
        {
            Assert.IsTrue(GachaResultPopup.IsEssenceOverflow(Overflow()), "갈 곳 없는 혼 정수 -> 파편 +40");

            var landed = Overflow();
            landed.BladeIndex = 0;
            landed.Shards = 0;
            Assert.IsFalse(GachaResultPopup.IsEssenceOverflow(landed), "요도에 들어간 혼 정수는 넘침이 아니다");

            var slid = Overflow();
            slid.Outcome = GachaCurve.Outcome.SoulRarity;
            slid.Downgraded = true;
            slid.Shards = 80;
            Assert.IsFalse(GachaResultPopup.IsEssenceOverflow(slid), "상위 혼의 미끄러짐은 등급 하락 줄이 맡는다");
        }

        // ---------------------------------------------------------------- 1·3·4 글자

        /** 72단계에 화면에 서는 문구. 4는 바꾸지 않았다 - 70단계 문구 그대로 아틀라스 안인지만 본다 */
        static string[] Step72Strings()
        {
            return new[]
            {
                LevelHud.LevelUpText(272),
                LevelHud.LevelUpText(99),
                GachaResultPopup.OverflowNote(YodoCurve.ShardsPerOverflowSoul),
                GachaResultPopup.RevealingText
            };
        }

        [Test]
        public void Step72Strings_AreInTheBakedCharsetAndTheRegistry()
        {
            string charset = File.ReadAllText(CharsetPath);
            string strings = File.ReadAllText(StringsPath);

            var missing = new List<string>();
            foreach (var text in Step72Strings())
                foreach (char c in text)
                    if (c != ' ' && charset.IndexOf(c) < 0)
                        missing.Add("'" + c + "' (U+" + ((int)c).ToString("X4") + ") in \"" + text + "\"");
            Assert.IsEmpty(missing, "아틀라스에 없는 글자: " + string.Join(", ", missing));

            foreach (var text in Step72Strings())
                StringAssert.Contains(text, strings, "UIStrings.txt에 없다: " + text);
        }

        [Test]
        public void RevealingText_KeepsThreeDots()
        {
            // 72단계 판단: "..."는 마침표 셋이고 마침표는 아틀라스에 있다. 한 글자 "…"로
            // 바꾸면 픽셀 글꼴에서 점이 한 칸에 몰려 더 작게 읽힌다 - 바꾸지 않았다
            Assert.AreEqual("소환 중...", GachaResultPopup.RevealingText);
        }

        // ---------------------------------------------------------------- 2 클라우드 팝업 창

        /**
         * @brief 창이 **고정 px**이고 마지막 행 아래 여백이 고정값이다. 폭은 그대로다.
         *
         * 70단계 실기(`a02_qa_boot`): 창 높이가 세이프 영역의 비율(0.34)이라 22:9에서
         * 버튼 아래가 500px 가까이 비었다. 69단계 확률 팝업과 같은 처리다.
         */
        [TestCase("CloudTakeoverPanel", new[] { "Message", "Cancel", "Retry" })]
        [TestCase("CloudEvictedPanel", new[] { "Message", "Confirm" })]
        [TestCase("CloudConflictPanel", new[] { "LocalCard", "LocalUse", "CloudCard", "CloudStamp", "CloudUse", "Later" })]
        public void CloudPopupWindow_FitsItsRows(string panelName, string[] rowNames)
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                Assert.IsNotNull(safeArea, "씬에 SafeArea가 없다");
                var root = safeArea.Find(panelName);
                Assert.IsNotNull(root, "씬에 " + panelName + "이 없다");
                var window = root.Find("Window") as RectTransform;
                Assert.IsNotNull(window, panelName + "에 창이 없다");

                // 높이는 비율이 아니라 px - 두 앵커가 한 점(가운데)에 모인다
                Assert.AreEqual(0.5f, window.anchorMin.y, 1e-4f, panelName + ": 창 높이가 아직 세이프 영역의 비율이다");
                Assert.AreEqual(0.5f, window.anchorMax.y, 1e-4f, panelName + ": 창 높이가 아직 세이프 영역의 비율이다");

                // 폭은 그대로 - 좌우 36px 물러난 풀폭
                Assert.AreEqual(0f, window.anchorMin.x, 1e-4f);
                Assert.AreEqual(1f, window.anchorMax.x, 1e-4f);
                Assert.AreEqual(36f, window.offsetMin.x, 0.01f, panelName + ": 창 폭이 바뀌었다");
                Assert.AreEqual(-36f, window.offsetMax.x, 0.01f, panelName + ": 창 폭이 바뀌었다");

                float height = window.offsetMax.y - window.offsetMin.y;
                float lowest = 0f;
                foreach (var name in rowNames)
                {
                    var row = window.Find(name) as RectTransform;
                    Assert.IsNotNull(row, panelName + "에 행 " + name + "이 없다");
                    float bottom = -row.anchoredPosition.y + row.sizeDelta.y;   // 피벗·앵커가 위
                    Assert.LessOrEqual(bottom, height + 0.01f, panelName + ": 행 " + name + "이 창 밖으로 나간다");
                    lowest = Mathf.Max(lowest, bottom);
                }

                Assert.AreEqual(48f, height - lowest, 0.01f, string.Format(
                    "{0}: 마지막 행 아래 여백이 {1:F0}px - 내용에 맞춘 고정 여백(48)이 아니다",
                    panelName, height - lowest));
            }
            finally
            {
                if (opened)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                        UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
            }
        }

        /** 다시 확인은 취소·이어하기와 같은 줄이다 - 둘은 함께 서지 않는다(CloudTakeoverPanel.Draw) */
        [Test]
        public void TakeoverRetry_SharesTheButtonRow()
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                var window = safeArea.Find("CloudTakeoverPanel/Window");
                Assert.IsNotNull(window);
                var cancel = (RectTransform)window.Find("Cancel");
                var retry = (RectTransform)window.Find("Retry");
                Assert.AreEqual(cancel.anchoredPosition.y, retry.anchoredPosition.y, 0.01f,
                                "다시 확인이 버튼 줄 아래 따로 서 있다 - 평소 화면에 빈 줄이 남는다");
            }
            finally
            {
                if (opened)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                        UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
            }
        }

        // ---------------------------------------------------------------- 5 프리셋 v23

        [Test]
        public void EveryPreset_StampsBothSummonXpFieldsAtZero()
        {
            var bad = new List<string>();
            foreach (var profile in new[] { TrialPresetForge.Profile.Floor, TrialPresetForge.Profile.CurveFollower })
            {
                var rows = profile == TrialPresetForge.Profile.Floor
                    ? PromotionTrialFixture.GemFloor()
                    : PromotionTrialFixture.Lead();

                for (int gate = 1; gate <= PromotionTrialFixture.GateStages.Length; gate++)
                {
                    var row = rows[PromotionTrialFixture.GateStages[gate - 1] - 1];
                    var save = TrialPresetForge.Build(row, gate);
                    string json = TrialPresetForge.ToJson(save);
                    string name = TrialPresetForge.NameOf(profile, gate);

                    if (save.version != SaveData.CurrentVersion) bad.Add(name + ": v" + save.version);
                    if (!json.Contains("\"yodoSummonXp\": 0")) bad.Add(name + ": yodoSummonXp 0이 없다");
                    if (!json.Contains("\"skillSummonXp\": 0")) bad.Add(name + ": skillSummonXp 0이 없다");

                    // 감사도 그 둘을 본다 - 0이 아니면 어긋난 축으로 나온다
                    save.yodoSummonXp = 7L;
                    bool caught = false;
                    foreach (var m in TrialPresetForge.Audit(save, row, gate))
                        if (m.Field == "요도 소환 경험치") caught = true;
                    if (!caught) bad.Add(name + ": 감사가 소환 경험치를 안 본다");
                }
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ---------------------------------------------------------------- 씬

        static Transform FindSafeArea(out bool opened)
        {
            opened = false;
            var found = FindIn(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath));
            if (found != null) return found;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            opened = scene.IsValid();
            return FindIn(scene);
        }

        static Transform FindIn(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var safe = root.transform.Find("SafeArea");
                if (safe != null) return safe;
            }
            return null;
        }
    }
}
