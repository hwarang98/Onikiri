using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using Onikiri.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 72단계 UI 잔챙이를 **씬의 실물로** 본다.
     *
     *   레벨업 272개 쌓임   -> 버튼 글자 "레벨업 99+" · 쌓인 개수는 그대로 272
     *   넘친 혼 정수 타일   -> 결과 판 아래 설명 줄 1개 ("혼 정수 초과 · 파편 +40")
     *
     * `SaveSandbox`로 실사용 세이브를 안 건드린다.
     */
    public class UiLeftoversStep72PlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        SaveSandbox sandbox;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);
        }

        [TearDown]
        public void Restore()
        {
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        static void Open(Component component)
        {
            for (var t = component.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        }

        /**
         * 레벨 1에서 n번 오를 만큼의 경험치. 곡선의 필요량을 더하고 다음 레벨의 절반을
         * 얹는다 - 딱 맞춘 합은 부동소수 반올림으로 n-1에 걸린다(첫 실행 271)
         */
        static BigDouble ExpFor(int levels)
        {
            var total = BigDouble.Zero;
            for (int level = 1; level <= levels; level++) total = total + ExpCurve.RequiredForLevel(level);
            return total + ExpCurve.RequiredForLevel(levels + 1) / (BigDouble)2d;
        }

        [UnityTest]
        public IEnumerator LevelUpButton_Shows99Plus_ButKeepsTheRealCount()
        {
            var character = CharacterLevel.Instance;
            Assert.IsNotNull(character, "씬에 CharacterLevel이 없다");

            var hud = Object.FindFirstObjectByType<LevelHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(hud, "씬에 LevelHud가 없다");
            var label = typeof(LevelHud).GetField("levelUpLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(hud) as TMP_Text;
            Assert.IsNotNull(label, "레벨업 버튼 글자 배선이 비었다");

            Open(hud);
            yield return null;
            yield return null;

            character.Restore(1, BigDouble.Zero, 0, 0);
            character.AddExp(ExpFor(272));
            yield return null;

            Assert.AreEqual(272, character.PendingLevelUps, "표시만 바꿨는데 쌓인 개수가 달라졌다");
            Assert.AreEqual("레벨업 99+", label.text, "세 자리가 그대로 적혔다");

            // "99+"도 44pt로는 칸(210)을 넘는다 - 캡션으로 내려가 칸 안에 든다
            Assert.IsTrue(hud.LevelUpDemoted, "99+가 본 글꼴 그대로다 - 칸을 넘는다");
            Assert.LessOrEqual(label.GetPreferredValues(label.text).x, label.rectTransform.rect.width,
                               "99+가 칸 밖으로 넘친다");

            character.Restore(1, BigDouble.Zero, 0, 0);
            character.AddExp(ExpFor(99));
            yield return null;

            Assert.AreEqual(99, character.PendingLevelUps);
            Assert.AreEqual("레벨업 99", label.text, "99는 그대로 99여야 한다");
            Assert.IsFalse(hud.LevelUpDemoted, "두 자리는 본 글꼴로 칸에 든다 - 캡션으로 내려가면 안 된다");
        }

        static GachaSystem.PullResult Overflow()
        {
            return new GachaSystem.PullResult
            {
                Outcome = GachaCurve.Outcome.SoulEssence,
                Grade = GachaCurve.GradeFor(GachaCurve.Outcome.SoulEssence),
                Shards = YodoCurve.ShardsPerOverflowSoul,
                BladeIndex = -1,
                LegendaryIndex = -1
            };
        }

        static GachaSystem.PullResult Plain()
        {
            return new GachaSystem.PullResult
            {
                Outcome = GachaCurve.Outcome.ShardSmall,
                Grade = GachaCurve.GradeFor(GachaCurve.Outcome.ShardSmall),
                Shards = GachaCurve.ShardsFor(GachaCurve.Outcome.ShardSmall),
                BladeIndex = -1,
                LegendaryIndex = -1
            };
        }

        [UnityTest]
        public IEnumerator OverflowTile_GetsOneNoteLine()
        {
            var popup = Object.FindFirstObjectByType<GachaResultPopup>(FindObjectsInactive.Include);
            Assert.IsNotNull(popup, "씬에 결과 판이 없다 - Onikiri/Build Shop Panel");
            Open(popup);
            yield return null;

            popup.Show(new List<GachaSystem.PullResult> { Overflow() }, YodoSystem.Instance);
            popup.RevealAll();
            yield return null;

            var lines = popup.NoteTexts();
            Assert.AreEqual(1, lines.Count, "넘침 타일 하나에 설명 줄이 한 줄이 아니다: " + string.Join(" | ", lines));
            Assert.AreEqual("혼 정수 초과 · 파편 +40", lines[0]);
            popup.Close();
            yield return null;

            // 10연에 셋이 넘쳐도 한 줄 - 합계가 적힌다
            var ten = new List<GachaSystem.PullResult>();
            for (int i = 0; i < GachaCurve.TenPullCount; i++) ten.Add(i % 3 == 0 && i < 9 ? Overflow() : Plain());
            popup.Show(ten, YodoSystem.Instance);
            popup.RevealAll();
            yield return null;

            lines = popup.NoteTexts();
            Assert.AreEqual(1, lines.Count, "넘침 셋이 여러 줄로 갈렸다: " + string.Join(" | ", lines));
            Assert.AreEqual(GachaResultPopup.OverflowNote(YodoCurve.ShardsPerOverflowSoul * 3), lines[0]);
            popup.Close();
        }
    }
}
