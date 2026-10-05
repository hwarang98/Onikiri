using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 69단계 뽑기 UI를 **씬의 버튼으로** 누른다.
     *
     *   1회 / 10회 / 무료 / 온보딩 10연   -> 결과 타일 수 · 등급 색
     *   결과 판의 재뽑기(10회)            -> 보석 225 한 번 · urgent 동기화 로그 한 건
     *   레벨업                            -> 판이 닫힌 뒤 배너 바 연출 한 번 · 제목 꼬리
     *
     * EditMode가 배치·출처·글자를 재고, 여기는 빌더가 세운 버튼이 시스템에 닿고
     * 시스템의 결과가 판에 서는지를 본다. `SaveSandbox`로 실사용 세이브를 안 건드린다.
     */
    public class GachaUiPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string UrgentGemLog = "urgent 동기화 예약: 보석 소비";

        SaveSandbox sandbox;
        GachaSystem gacha;
        SkillGachaSystem skillGacha;
        GemWallet gems;
        ShopPanel shop;
        GachaResultPopup popup;
        readonly List<string> logs = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);

            gacha = GachaSystem.Instance;
            skillGacha = SkillGachaSystem.Instance;
            gems = GemWallet.Instance;
            shop = Object.FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
            popup = Object.FindFirstObjectByType<GachaResultPopup>(FindObjectsInactive.Include);

            Assert.IsNotNull(gacha, "씬에 GachaSystem이 없다");
            Assert.IsNotNull(skillGacha, "씬에 SkillGachaSystem이 없다");
            Assert.IsNotNull(shop, "씬에 상점이 없다 - Onikiri/Build Shop Panel");
            Assert.IsNotNull(popup, "씬에 결과 판이 없다 - Onikiri/Build Shop Panel");

            var stage = Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
            int frontier = Mathf.Max(GachaCurve.UnlockStage, SkillGachaCurve.PullUnlockStage);
            stage.SetProgress(frontier, 0, frontier - 1, frontier);

            gacha.DebugReset();
            skillGacha.DebugReset();
            gems.SetBalance(100000L);

            // 상점 화면을 연다 - 버튼은 꺼진 패널 안에 저장돼 있어 조상까지 켜야
            // Start(리스너 연결)가 돈다. 탭을 누르는 것과 같은 일이다
            for (var t = shop.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            yield return null;
            yield return null;

            Onikiri.Cloud.CloudSaveSync.Wire(null, null, null, gems);
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void Restore()
        {
            Application.logMessageReceived -= Capture;
            logs.Clear();
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        void Capture(string message, string stack, LogType type) { logs.Add(message); }

        ShopPanel.BannerView Banner(bool skill)
        {
            var field = typeof(ShopPanel).GetField(skill ? "skillBanner" : "yodoBanner",
                                                   BindingFlags.NonPublic | BindingFlags.Instance);
            var view = field.GetValue(shop) as ShopPanel.BannerView;
            Assert.IsNotNull(view, "배너 배선이 비었다");
            return view;
        }

        Color[] GradeColors()
        {
            var field = typeof(GachaResultPopup).GetField("gradeColors", BindingFlags.NonPublic | BindingFlags.Instance);
            return (Color[])field.GetValue(popup);
        }

        void AssertTiles(int expected, string what)
        {
            Assert.IsTrue(popup.IsOpen, what + ": 결과 판이 안 떴다");
            Assert.AreEqual(expected, popup.VisibleTileCount, what + ": 타일 수가 결과 수와 다르다");

            var colors = GradeColors();
            for (int i = 0; i < expected; i++)
            {
                int grade = popup.TileGrade(i);
                Assert.That(grade, Is.InRange(0, GachaCurve.GradeCount - 1));
                Assert.AreEqual(colors[grade], popup.TileFrameColor(i), what + ": 타일 " + i + "의 테두리가 등급 색이 아니다");
            }
        }

        int UrgentGemLogs()
        {
            int n = 0;
            foreach (var line in logs) if (line.Contains(UrgentGemLog)) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator PaidPulls_FillOneOrTenTiles()
        {
            var view = Banner(false);

            // 일일 무료를 먼저 써 둔다 - 그래야 1회 버튼이 보석 버튼이다
            Assert.IsTrue(gacha.TryFreePull());
            popup.Close();
            yield return null;

            Assert.AreEqual(ShopPanel.SingleTitle, view.singleTitle.text, "무료를 쓴 뒤에도 1회 버튼이 무료다");
            Assert.AreEqual(GachaCurve.PullCostGems.ToString(), view.singleCost.text, "1회 값이 곡선과 다르다");
            Assert.AreEqual(GachaCurve.TenPullCostGems.ToString(), view.tenCost.text, "10회 값이 곡선과 다르다");

            long before = gems.Gems;
            view.singleButton.onClick.Invoke();
            yield return null;
            AssertTiles(1, "1회");
            Assert.AreEqual(before - GachaCurve.PullCostGems, gems.Gems);

            popup.Close();
            view.tenButton.onClick.Invoke();
            yield return null;
            AssertTiles(GachaCurve.TenPullCount, "10회");
            Assert.AreEqual(before - GachaCurve.PullCostGems - GachaCurve.TenPullCostGems, gems.Gems);
        }

        [UnityTest]
        public IEnumerator FreePull_IsTheSingleButtonAndCostsNothing()
        {
            var view = Banner(false);
            gacha.DebugResetFreePull();
            yield return null;

            Assert.AreEqual(ShopPanel.FreeSingleTitle, view.singleTitle.text, "무료가 남았는데 1회 버튼이 무료로 안 바뀌었다");

            long before = gems.Gems;
            view.singleButton.onClick.Invoke();
            yield return null;

            AssertTiles(1, "무료 1회");
            Assert.AreEqual(before, gems.Gems, "무료 뽑기가 보석을 썼다");
            Assert.IsFalse(gacha.HasFreePull);
        }

        [UnityTest]
        public IEnumerator IntroTenPull_IsTheSkillTenButtonAndCostsNothing()
        {
            var view = Banner(true);
            skillGacha.DebugResetIntro();
            yield return null;

            Assert.AreEqual(ShopPanel.IntroTenTitle, view.tenTitle.text, "온보딩 10연이 남았는데 10회 버튼이 선물로 안 바뀌었다");

            long before = gems.Gems;
            view.tenButton.onClick.Invoke();
            yield return null;

            AssertTiles(SkillGachaCurve.IntroPullCount, "온보딩 10연");
            Assert.AreEqual(before, gems.Gems, "선물 10연이 보석을 썼다");
            Assert.IsFalse(skillGacha.CanClaimIntro);
            Assert.AreEqual(ShopPanel.TenTitle, view.tenTitle.text, "선물을 받은 뒤에도 10회 버튼이 선물이다");
        }

        [UnityTest]
        public IEnumerator Repull_SpendsOnceAndWakesUrgentSyncOnce()
        {
            var view = Banner(false);
            view.tenButton.onClick.Invoke();
            yield return null;
            popup.RevealAll();

            long before = gems.Gems;
            logs.Clear();

            var repull = typeof(GachaResultPopup).GetField("repullButton", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(popup) as Button;
            Assert.IsNotNull(repull, "재뽑기 버튼 배선이 비었다");
            Assert.IsTrue(repull.interactable, "보석이 넉넉한데 재뽑기가 죽어 있다");

            repull.onClick.Invoke();
            yield return null;

            Assert.AreEqual(before - GachaCurve.TenPullCostGems, gems.Gems, "재뽑기가 225를 한 번 쓰지 않았다");
            AssertTiles(GachaCurve.TenPullCount, "재뽑기");
            Assert.AreEqual(1, UrgentGemLogs(), "재뽑기의 보석 차감이 urgent 동기화를 한 번 깨우지 않았다:\n"
                                                + string.Join("\n", logs));

            // 못 사면 죽는다
            gems.SetBalance(GachaCurve.TenPullCostGems - 1);
            yield return null;
            Assert.IsFalse(repull.interactable, "보석이 모자란데 재뽑기가 눌린다");
        }

        [UnityTest]
        public IEnumerator LevelUp_PlaysTheBarOnceAfterThePopupCloses()
        {
            var view = Banner(false);

            // 무료를 써서 1회 버튼을 보석 버튼으로 만들고, 레벨업 직전으로 민다
            Assert.IsTrue(gacha.TryFreePull());
            popup.Close();
            yield return null;
            gacha.DebugPushToLevelUp();
            int before = shop.YodoLevelUpsPlayed;

            view.singleButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, gacha.SummonLevel);

            // 판이 떠 있는 동안은 바가 기다린다
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(before, shop.YodoLevelUpsPlayed, "결과 판이 떠 있는데 바 연출이 돌았다");

            popup.RevealAll();
            var title = typeof(GachaResultPopup).GetField("titleLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(popup) as TMPro.TMP_Text;
            StringAssert.EndsWith(SummonLevelCurve.LevelUpText(2), title.text, "제목 꼬리가 없다");

            popup.Close();
            float waited = 0f;
            float worstOverflow = 0f;
            var labelRect = (RectTransform)view.levelLabel.transform;
            while (shop.LevelUpAnimating && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
                // 펀치로 커진 라벨이 제 칸(바 왼쪽)을 넘어 바를 덮지 않는다
                float drawn = view.levelLabel.preferredWidth * labelRect.localScale.x;
                worstOverflow = Mathf.Max(worstOverflow, drawn - labelRect.rect.width);
            }
            Assert.LessOrEqual(worstOverflow, 0.5f, "펀치 중 라벨이 칸을 넘어 바를 덮었다");

            Assert.AreEqual(before + 1, shop.YodoLevelUpsPlayed, "판이 닫힌 뒤 바 연출이 정확히 한 번이 아니다");
            Assert.AreEqual(ShopPanel.LevelLabel(2), view.levelLabel.text, "연출 뒤 라벨이 새 레벨이 아니다");
            Assert.AreEqual(ShopPanel.BarRatio(gacha.SummonXp), view.barFill.anchorMax.x, 1e-4f, "연출 뒤 바가 곡선 값이 아니다");
            Assert.AreEqual(ShopPanel.BarText(SummonLevelCurve.XpIntoLevel(gacha.SummonXp), SummonLevelCurve.XpToNext(2)),
                            view.barText.text, "연출 뒤 바 숫자가 곡선 값이 아니다");
        }

        /** 확률 팝업의 표 = 그 배너의 지금 레벨 곡선 - Lv.1 표가 아니다 */
        [UnityTest]
        public IEnumerator RatePopup_ShowsTheCurrentLevelTable()
        {
            var view = Banner(false);
            Assert.IsTrue(gacha.TryFreePull());
            popup.Close();
            gacha.DebugPushToLevelUp();
            view.singleButton.onClick.Invoke();
            yield return null;
            popup.RevealAll();
            popup.Close();
            Assert.AreEqual(2, gacha.SummonLevel);

            var rates = Object.FindFirstObjectByType<GachaRatePopup>(FindObjectsInactive.Include);
            Assert.IsNotNull(rates, "씬에 확률 팝업이 없다");

            view.rateButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(rates.gameObject.activeInHierarchy, "확률 버튼이 팝업을 안 열었다");
            Assert.IsFalse(rates.ShowingSkill);
            Assert.AreEqual(2, rates.ShownLevel);
            var chances = SummonLevelCurve.ChancesAt(2);
            for (int i = 0; i < chances.Length; i++)
                Assert.AreEqual(GachaRatePopup.PercentText(chances[i]), rates.ChanceText(i), "칸 " + i + "이 지금 레벨 곡선이 아니다");
            Assert.AreEqual(GachaRatePopup.FootnoteFor(2), rates.FootnoteText);

            // 오의 쪽은 오의 배너의 레벨(아직 1)
            rates.gameObject.SetActive(false);
            Banner(true).rateButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(rates.ShowingSkill);
            Assert.AreEqual(skillGacha.SummonLevel, rates.ShownLevel);
            var skillChances = SummonLevelCurve.ChancesAt(skillGacha.SummonLevel);
            for (int i = 0; i < skillChances.Length; i++)
                Assert.AreEqual(GachaRatePopup.PercentText(skillChances[i]), rates.ChanceText(i));
        }

        /** 연출 중 바 숫자는 라벨과 같은 순간을 말한다 - 채움 동안은 옛 레벨이 꽉 찬 값 */
        [UnityTest]
        public IEnumerator LevelUp_BarTextMatchesTheLabelWhileFilling()
        {
            var view = Banner(false);
            Assert.IsTrue(gacha.TryFreePull());
            popup.Close();
            yield return null;
            gacha.DebugPushToLevelUp();

            view.singleButton.onClick.Invoke();
            yield return null;
            popup.RevealAll();
            popup.Close();
            yield return null;

            Assert.IsTrue(shop.LevelUpAnimating, "레벨업 연출이 시작되지 않았다");
            long full = SummonLevelCurve.XpToNext(1);
            Assert.AreEqual(ShopPanel.LevelLabel(1), view.levelLabel.text, "채움 동안 라벨이 옛 레벨이 아니다");
            Assert.AreEqual(ShopPanel.BarText(full, full), view.barText.text, "채움 동안 바 숫자가 옛 레벨의 꽉 찬 값이 아니다");
        }
    }
}
