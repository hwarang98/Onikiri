using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 68단계 소환 레벨을 **씬의 시스템으로** 돌린다.
     *
     * EditMode가 식(정규화·요구 XP·마이그레이션)을 잰다면, 여기는 씬에 선
     * 뽑기 시스템이 보석을 받고 경험치를 쌓는지, 레벨업 이벤트가 한 번만 오는지,
     * 결과 팝업의 제목 줄이 그 레벨업을 적는지를 본다.
     */
    public class SummonLevelPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        SaveSandbox sandbox;
        GachaSystem gacha;
        SkillGachaSystem skillGacha;
        GemWallet gems;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            // 세션을 떼어 저장·커밋 경로를 끊는다(StatPointResetPlayTests와 같은 이유)
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);

            gacha = GachaSystem.Instance;
            skillGacha = SkillGachaSystem.Instance;
            gems = GemWallet.Instance;
            Assert.IsNotNull(gacha, "씬에 GachaSystem이 없다");
            Assert.IsNotNull(skillGacha, "씬에 SkillGachaSystem이 없다");
            Assert.IsNotNull(gems, "씬에 GemWallet이 없다");

            // 두 배너의 보석 구매가 다 열린 최전선(st41)에 선다
            var stage = Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
            Assert.IsNotNull(stage, "씬에 StageProgress가 없다");
            int frontier = Mathf.Max(GachaCurve.UnlockStage, SkillGachaCurve.PullUnlockStage);
            stage.SetProgress(frontier, 0, frontier - 1, frontier);

            gacha.DebugReset();
            skillGacha.DebugReset();
            gems.SetBalance(100000L);
            yield return null;

            Assert.IsTrue(gacha.IsUnlocked, "최전선 st" + frontier + "인데 요도 뽑기가 잠겨 있다");
        }

        [TearDown]
        public void Restore()
        {
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        [UnityTest]
        public IEnumerator TenPull_AddsTenSummonXp_OnBothBanners()
        {
            long gemsBefore = gems.Gems;

            Assert.IsTrue(gacha.TryPull(GachaCurve.TenPullCount), "요도 10연이 안 돌았다");
            yield return null;

            Assert.AreEqual((long)GachaCurve.TenPullCount, gacha.SummonXp, "요도 10연이 경험치 10을 안 줬다");
            Assert.AreEqual(gemsBefore - GachaCurve.TenPullCostGems, gems.Gems, "10연 값이 225가 아니다");
            Assert.AreEqual(0L, skillGacha.SummonXp, "요도 뽑기가 오의 배너의 경험치를 올렸다 - 배너별이어야 한다");

            if (skillGacha.CanPull(SkillGachaCurve.TenPullCount))
            {
                Assert.IsTrue(skillGacha.TryPull(SkillGachaCurve.TenPullCount));
                yield return null;
                Assert.AreEqual((long)SkillGachaCurve.TenPullCount, skillGacha.SummonXp,
                    "오의 10연이 경험치 10을 안 줬다");
                Assert.AreEqual((long)GachaCurve.TenPullCount, gacha.SummonXp,
                    "오의 뽑기가 요도 배너의 경험치를 올렸다");
            }
        }

        [UnityTest]
        public IEnumerator OneLevelUp_RaisesExactlyOneEvent()
        {
            var heard = new List<int>();
            gacha.SummonLevelUp += heard.Add;
            try
            {
                gacha.DebugPushToLevelUp();
                Assert.AreEqual(1, gacha.SummonLevel);

                Assert.IsTrue(gacha.TryPull(1));
                yield return null;

                CollectionAssert.AreEqual(new[] { 2 }, heard, "레벨업 한 번에 이벤트가 한 건이 아니다");
                Assert.AreEqual(2, gacha.SummonLevel);
                Assert.AreEqual(2, gacha.LastBatchLevelUp);
            }
            finally { gacha.SummonLevelUp -= heard.Add; }
        }

        [UnityTest]
        public IEnumerator ResultPopup_TitleSaysTheLevelUp()
        {
            var popup = Object.FindFirstObjectByType<Onikiri.UI.GachaResultPopup>(FindObjectsInactive.Include);
            Assert.IsNotNull(popup, "씬에 결과 팝업이 없다 - ShopPanelBuilder를 다시 돌릴 것");

            var title = typeof(Onikiri.UI.GachaResultPopup)
                .GetField("titleLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(popup) as TMP_Text;
            Assert.IsNotNull(title, "결과 팝업의 제목 줄 배선이 비었다");

            List<GachaSystem.PullResult> last = null;
            System.Action<List<GachaSystem.PullResult>> grab = list => last = new List<GachaSystem.PullResult>(list);
            gacha.Pulled += grab;
            try
            {
                gacha.DebugPushToLevelUp();
                Assert.IsTrue(gacha.TryPull(1));
                yield return null;

                popup.Show(last, Object.FindFirstObjectByType<YodoSystem>(FindObjectsInactive.Include),
                           gacha.LastBatchLevelUp);
                yield return null;

                // 69단계: 꼬리는 마지막 타일이 열린 뒤에 붙는다(결정 4)
                popup.RevealAll();
                StringAssert.EndsWith(SummonLevelCurve.LevelUpText(2), title.text,
                    "레벨업이 난 뽑기의 제목 줄에 \"소환 Lv.2 달성\"이 없다");
                StringAssert.DoesNotContain("천장", title.text, "결과 팝업에 천장 꼬리표가 남았다");

                Assert.IsTrue(gacha.TryPull(1));
                yield return null;
                popup.Show(last, Object.FindFirstObjectByType<YodoSystem>(FindObjectsInactive.Include),
                           gacha.LastBatchLevelUp);
                yield return null;
                popup.RevealAll();
                StringAssert.DoesNotContain("달성", title.text, "레벨이 안 올랐는데 제목 줄에 레벨업이 붙었다");
            }
            finally { gacha.Pulled -= grab; }
        }
    }
}
