using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 73단계 — 충돌 화면에서 요약이 머리를 덮던 것 (71.1 §8.4-2 · 72 §4-2).
     *
     * 머리("클라우드 기록")와 요약("최고 43층 · Lv.65 · 로닌 · 보석 8717")이 한 줄에
     * 가로로 나눠 서 있었다. 머리는 왼쪽 45%, 요약은 **전체 폭에 오른쪽 정렬**이라
     * 요약이 길어지면 왼쪽으로 뻗어 머리 위를 지났다. Ellipsis가 켜져 있어도 기준
     * 폭이 100%라 잘릴 일이 없었다.
     *
     * **폭으로는 막을 수 없는 종류다.** 요약은 값에서 나오고 상한이 없다 - 전직
     * 이름만 해도 `로닌`과 `진 데몬사무라이`가 174px 차이고, 보석은 자릿수가 는다.
     * 그래서 세로로 나눴다. 아래 검사들이 지키는 것은 "지금 값이 안 겹친다"가 아니라
     * **"어떤 값이어도 겹칠 수 없다"**는 구조다.
     */
    public class CloudConflictLayoutStep73Tests
    {
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        const string PanelPath = "CloudConflictPanel/Window";

        /**
         * @brief 머리와 요약이 **세로로** 갈라져 있다. 이것이 겹침 불가의 근거다.
         *
         * 두 라벨이 같은 가로 폭을 통째로 쓰고 세로 칸만 다르면, 글자가 아무리
         * 길어져도 서로의 칸에 들어갈 수 없다. 가로로 나눠 쓰는 순간(`anchorMax.x`가
         * 1이 아닌 라벨이 생기는 순간) 다시 폭 다툼이 시작되므로 그것까지 막는다.
         */
        [TestCase("LocalCard")]
        [TestCase("CloudCard")]
        public void Card_StacksTheSummaryUnderTheHead(string cardName)
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                Assert.IsNotNull(safeArea, "씬에 SafeArea가 없다");
                var card = safeArea.Find(PanelPath + "/" + cardName) as RectTransform;
                Assert.IsNotNull(card, "충돌 화면에 " + cardName + "이 없다");

                var head = (RectTransform)card.Find("Head");
                var summary = (RectTransform)card.Find("Summary");
                Assert.IsNotNull(head, cardName + "에 머리가 없다");
                Assert.IsNotNull(summary, cardName + "에 요약이 없다");

                // 세로로 갈렸다 - 요약의 천장이 머리의 바닥보다 높지 않다
                Assert.LessOrEqual(summary.anchorMax.y, head.anchorMin.y + 1e-4f, string.Format(
                    "{0}: 요약 천장({1})이 머리 바닥({2})을 넘는다 - 같은 줄에 선다",
                    cardName, summary.anchorMax.y, head.anchorMin.y));

                // 가로는 둘 다 통째로 쓴다 - 폭을 나눠 쓰면 값이 자랄 때 다시 겹친다
                foreach (var pair in new[] {
                    new KeyValuePair<string, RectTransform>("머리", head),
                    new KeyValuePair<string, RectTransform>("요약", summary) })
                {
                    Assert.AreEqual(0f, pair.Value.anchorMin.x, 1e-4f,
                        cardName + ": " + pair.Key + "가 가로를 나눠 쓴다");
                    Assert.AreEqual(1f, pair.Value.anchorMax.x, 1e-4f,
                        cardName + ": " + pair.Key + "가 가로를 나눠 쓴다 - 71.1에서 겹치던 배치다");
                }
            }
            finally { CloseIfOpened(opened); }
        }

        /** 제 칸을 넘는 글자는 Ellipsis가 **그 칸 안에서** 자른다 - 옆으로 번지지 않는다 */
        [TestCase("LocalCard")]
        [TestCase("CloudCard")]
        public void Summary_ClipsInsideItsOwnBox(string cardName)
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                var summary = safeArea.Find(PanelPath + "/" + cardName + "/Summary")
                                      .GetComponent<TMP_Text>();
                Assert.IsNotNull(summary);
                Assert.AreEqual(TextOverflowModes.Ellipsis, summary.overflowMode,
                                cardName + ": 요약이 칸을 넘어도 안 잘린다");
            }
            finally { CloseIfOpened(opened); }
        }

        /**
         * @brief **있을 법한 최장형**이 요약 칸에 든다.
         *
         * 최악(보석 아홉 자리 + `진 데몬사무라이`)까지 넣는 것은 목표가 아니다 - 그건
         * Ellipsis의 몫이고, 잘려도 머리를 덮지는 않는다(위 두 검사). 여기서 재는 것은
         * **실제로 닿을 수 있는 값**이다: 71.1 실기의 43층 계정과, 전직을 끝내고 보석을
         * 쌓아 둔 플레이어.
         */
        [TestCase("최고 43층 · Lv.65 · 로닌 · 보석 8717", TestName = "실기 71.1의 43층 계정")]
        [TestCase("최고 200층 · Lv.300 · 데몬사무라이 · 보석 30000", TestName = "전직 5 · 보석 3만")]
        public void Summary_FitsTheReachableWorstCase(string line)
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                var summary = safeArea.Find(PanelPath + "/CloudCard/Summary")
                                      .GetComponent<TMP_Text>();
                float box = ((RectTransform)summary.transform).rect.width;
                float need = summary.GetPreferredValues(line).x;

                Assert.LessOrEqual(need, box, string.Format(
                    "요약 [{0}]이 {1:F0}px인데 칸은 {2:F0}px - 잘린다", line, need, box));
            }
            finally { CloseIfOpened(opened); }
        }

        /** 전직 이름이 요약 폭을 얼마나 흔드는지 - 폭 배분이 왜 안 되는지의 근거다 */
        [Test]
        public void EvolutionNames_SwingTheSummaryWidth()
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                var summary = safeArea.Find(PanelPath + "/CloudCard/Summary")
                                      .GetComponent<TMP_Text>();

                float shortest = float.MaxValue, longest = 0f;
                for (int tier = 0; tier <= 6; tier++)
                {
                    float w = summary.GetPreferredValues(EvolutionCatalog.NameOf(tier)).x;
                    if (w < shortest) shortest = w;
                    if (w > longest) longest = w;
                }

                Assert.Greater(longest - shortest, 100f, string.Format(
                    "전직 이름의 폭 차이가 {0:F0}px뿐이다 - 이 검사의 전제(이름만으로도 폭이 "
                    + "크게 흔들린다)가 깨졌으면 가로 배분을 다시 따져볼 수 있다",
                    longest - shortest));
            }
            finally { CloseIfOpened(opened); }
        }

        /**
         * @brief 카드가 두 칸을 먹어도 **행 격자가 어긋나지 않는다**.
         *
         * 72단계가 묶은 "마지막 행 아래 48px"은 `RowBottom`의 식에 기대고 있다. 두 칸짜리
         * 행이 칸 사이 간격을 제 몫으로 삼키지 않으면 그 식이 어긋나 창 높이가 틀어진다.
         */
        [Test]
        public void TwoSpanCard_KeepsTheRowGrid()
        {
            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                var window = safeArea.Find(PanelPath) as RectTransform;
                var card = window.Find("LocalCard") as RectTransform;
                var next = window.Find("LocalUse") as RectTransform;

                float cardBottom = -card.anchoredPosition.y + card.sizeDelta.y;
                float nextTop = -next.anchoredPosition.y;

                Assert.Greater(card.sizeDelta.y, next.sizeDelta.y, "카드가 한 칸짜리다");
                Assert.AreEqual(6f, nextTop - cardBottom, 0.01f,
                    "카드 아래 간격이 행 간격(6)이 아니다 - 두 칸을 먹으며 격자가 어긋났다");
            }
            finally { CloseIfOpened(opened); }
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

        static void CloseIfOpened(bool opened)
        {
            if (!opened) return;
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
        }
    }
}
