using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 69단계 뽑기 UI.
     *
     *   배치     결과 타일 5열 · 줄마다 가운데 · 타일 수 = 결과 수
     *   출처     버튼 문구 · 바의 값이 곡선에서 나온다
     *   글자     새 문구가 전부 구운 아틀라스 안이다
     *   검산     빌더의 폭·세로 검산이 경고를 하나도 안 낸다
     */
    public class GachaUiTests
    {
        // ---------------------------------------------------------------- 배치

        [Test]
        public void TileLayout_OneTileSitsInTheMiddle()
        {
            var centers = GachaResultPopup.TileLayout(1, 5, 160f, 32f);
            Assert.AreEqual(1, centers.Length);
            Assert.AreEqual(Vector2.zero, centers[0], "1회 결과가 한가운데가 아니다");
        }

        [Test]
        public void TileLayout_TenPullsAreTwoRowsOfFive()
        {
            const float tile = 160f, gap = 32f, step = tile + gap;
            var centers = GachaResultPopup.TileLayout(GachaCurve.TenPullCount, 5, tile, gap);

            Assert.AreEqual(GachaCurve.TenPullCount, centers.Length, "타일 수가 결과 수와 다르다");

            var rows = centers.Select(c => c.y).Distinct().OrderByDescending(y => y).ToArray();
            Assert.AreEqual(2, rows.Length, "10연이 두 줄이 아니다");
            Assert.AreEqual(step * 0.5f, rows[0], 1e-3f);
            Assert.AreEqual(-step * 0.5f, rows[1], 1e-3f);

            for (int row = 0; row < 2; row++)
            {
                var xs = centers.Skip(row * 5).Take(5).Select(c => c.x).ToArray();
                Assert.AreEqual(-2f * step, xs[0], 1e-3f, "줄이 가운데 정렬이 아니다");
                Assert.AreEqual(2f * step, xs[4], 1e-3f, "줄이 가운데 정렬이 아니다");
                Assert.AreEqual(0f, xs.Sum(), 1e-3f);
            }

            Assert.AreEqual(2f * tile + gap, GachaResultPopup.GridHeight(GachaCurve.TenPullCount, 5, tile, gap), 1e-3f);
        }

        /** 남는 줄도 가운데다 - 7개면 아랫줄 둘이 가운데에 선다 */
        [Test]
        public void TileLayout_APartialRowIsCenteredToo()
        {
            const float step = 192f;
            var centers = GachaResultPopup.TileLayout(7, 5, 160f, 32f);
            Assert.AreEqual(-step * 0.5f, centers[5].x, 1e-3f);
            Assert.AreEqual(step * 0.5f, centers[6].x, 1e-3f);
        }

        /** 타일 다섯 개와 간격이 1080 폭 안에 들어간다 (여백 72 x 2) */
        [Test]
        public void TileGrid_FitsTheDesignWidth()
        {
            float width = 5 * 160f + 4 * 32f;
            Assert.LessOrEqual(width, Onikiri.Core.DisplayConfig.DesignWidth - 2f * 72f);
        }

        // ---------------------------------------------------------------- 출처

        [Test]
        public void ButtonTexts_ComeFromTheCurve()
        {
            Assert.AreEqual(GachaCurve.TenPullCount + "회 소환", ShopPanel.TenTitle);
            Assert.AreEqual("선물 " + SkillGachaCurve.IntroPullCount + "연", ShopPanel.IntroTenTitle);

            // 1회 / 10회는 그대로다 (69단계 확정 1 - 11/33은 범위 밖)
            Assert.AreEqual(10, GachaCurve.TenPullCount);
            Assert.AreEqual(25, GachaCurve.PullCostGems);
            Assert.AreEqual(225, GachaCurve.TenPullCostGems);
            Assert.AreEqual(GachaCurve.PullCostGems, SkillGachaCurve.PullCostGems);
            Assert.AreEqual(GachaCurve.TenPullCostGems, SkillGachaCurve.TenPullCostGems);
        }

        /** 바의 값 = 곡선 하나(SummonLevelCurve) - 왼쪽 라벨 · 바 안 숫자 · 채움 비율 */
        [Test]
        public void SummonBar_ReadsTheCurve()
        {
            foreach (long xp in new long[] { 0, 1, 19, 20, 43, 101, 122, 1775 })
            {
                int level = SummonLevelCurve.LevelFor(xp);
                long into = SummonLevelCurve.XpIntoLevel(xp);
                long next = SummonLevelCurve.XpToNext(level);

                Assert.AreEqual((float)into / next, ShopPanel.BarRatio(xp), 1e-6f, "XP " + xp + "의 채움");
                Assert.AreEqual("소환 Lv." + level, ShopPanel.LevelLabel(level));
                Assert.AreEqual(into + " / " + next, ShopPanel.BarText(into, next));
                Assert.That(ShopPanel.BarRatio(xp), Is.InRange(0f, 1f));
            }

            Assert.AreEqual(0f, ShopPanel.BarRatio(20), 1e-6f, "레벨이 막 오른 순간은 바가 비어야 한다");
        }

        /** 확률 팝업의 아랫줄은 표가 어느 레벨의 표인지 말한다 */
        [Test]
        public void RateFootnote_NamesTheLevel()
        {
            StringAssert.StartsWith("소환 Lv.7 기준 · ", GachaRatePopup.FootnoteFor(7));
            StringAssert.StartsWith("소환 Lv.1 기준", GachaRatePopup.FootnoteFor(0), "0 이하는 Lv.1이다");
            Assert.AreEqual("71.6%", GachaRatePopup.PercentText(GachaCurve.Chances[0]));
        }

        // ---------------------------------------------------------------- 글자

        /**
         * @brief 69단계의 새 문구가 **구운 아틀라스(472자)** 안이다 - 없는 글자는 □로 뜬다.
         *
         * 지시서 문구 셋(妖刀 · 카 · 막힘)은 아틀라스에 없어 문구를 바꿨다(결정 1).
         * 같은 원칙으로 루·건·쪽·커·높도 피했다. 문구는 UIStrings.txt에도 있어야 한다 -
         * 차셋을 다시 구우면 그 파일이 출처다.
         */
        [Test]
        public void Step69Strings_AreInTheBakedCharset()
        {
            string charset = File.ReadAllText("Assets/_Project/Data/FontCharset.txt");
            string strings = File.ReadAllText("Assets/_Project/Data/UIStrings.txt");

            var missing = new List<string>();
            foreach (var text in ShopPanel.Step69Strings)
            {
                foreach (char c in text)
                    if (c != ' ' && charset.IndexOf(c) < 0)
                        missing.Add("'" + c + "' (U+" + ((int)c).ToString("X4") + ") in \"" + text + "\"");
            }
            Assert.IsEmpty(missing, "아틀라스에 없는 글자: " + string.Join(", ", missing));

            foreach (var text in new[] { ShopPanel.YodoTitle, ShopPanel.YodoSubtitle, ShopPanel.SkillTitle,
                                         ShopPanel.SkillSubtitle, ShopPanel.SingleTitle, ShopPanel.TenTitle,
                                         ShopPanel.FreeSingleTitle, ShopPanel.FreeSingleNote, ShopPanel.IntroTenTitle,
                                         ShopPanel.RateButtonText, ShopPanel.AdButtonText, ShopPanel.SkillBuyLaterText,
                                         GachaRatePopup.YodoTitle, GachaRatePopup.SkillTitle, GachaRatePopup.FootnoteFor(1) })
                StringAssert.Contains(text, strings, "UIStrings.txt에 없다: " + text);
        }

        // ---------------------------------------------------------------- 검산

        /**
         * @brief 빌더의 검산이 **경고를 하나도 안 낸다** - 폭(VerifyTextFits) · 세로(VerifyRowsFit).
         *
         * 68단계까지 남아 있던 "희귀 스킬 XP 240"(270px / 칸 254px)이 69단계에 확률
         * 팝업의 넓은 칸(456px)으로 옮겨 가며 풀렸다. 빌더는 asmdef 없는 에디터
         * 어셈블리라 리플렉션으로 부른다.
         */
        [Test]
        public void Builder_ReportsNoOverflow()
        {
            var builder = FindType("Onikiri.EditorTools.ShopPanelBuilder");
            if (builder == null) Assert.Ignore("에디터 어셈블리가 없다 (에디터 밖 실행)");

            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var warnings = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.Contains("[Onikiri] Shop")) warnings.Add(message);
            };

            Application.logMessageReceived += capture;
            try
            {
                builder.GetMethod("VerifyRowsFit", flags).Invoke(null, null);
                builder.GetMethod("VerifyTextFits", flags).Invoke(null, null);
            }
            finally { Application.logMessageReceived -= capture; }

            Assert.IsEmpty(warnings, string.Join("\n", warnings));

            var fits = builder.GetProperty("BannerRowsFitTheViewport", BindingFlags.Public | BindingFlags.Static);
            Assert.IsTrue((bool)fits.GetValue(null, null), "배너 두 행이 스크롤 없이 안 선다");
        }

        private static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;
            }
            return null;
        }
    }
}
