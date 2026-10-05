using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 씬의 팝업이 **전부 팝업 층위(20) 위에** 그려진다 (70단계).
     *
     * 66단계 포인트 초기화 팝업이 층위 없이 세워져 가이드 카드 · EXP 띠(층위 10)
     * 아래에 깔렸다(70단계 실기). 규칙은 에디터의 `PopupLayerAudit` 한 곳에 있고,
     * 이 검사와 빌더 경고가 같은 규칙을 쓴다 - 다음 팝업도 여기 걸린다.
     * 빌더는 asmdef 없는 에디터 어셈블리라 리플렉션으로 부른다.
     */
    public class PopupLayerTests
    {
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        [Test]
        public void EveryPopup_DrawsOnThePopupLayer()
        {
            var audit = FindType("Onikiri.EditorTools.PopupLayerAudit");
            if (audit == null) Assert.Ignore("에디터 어셈블리가 없다 (에디터 밖 실행)");

            bool opened;
            var safeArea = FindSafeArea(out opened);
            try
            {
                Assert.IsNotNull(safeArea, "씬에 SafeArea가 없다");

                var problems = (List<string>)audit.GetMethod("Problems").Invoke(null, new object[] { safeArea });
                Assert.IsEmpty(problems, string.Join("\n", problems));

                // 70단계에 잡힌 셋이 규칙의 "팝업"에 들어간다 - 규칙이 그 자리를 못 보면 검사가 빈다
                var isPopup = audit.GetMethod("IsPopup");
                foreach (var name in new[] { "StatResetPopup", "TrialNotice", "TrialResult",
                                             "GachaResultPopup", "GachaRatePopup", "SettingsPanel" })
                {
                    var child = safeArea.Find(name);
                    Assert.IsNotNull(child, "씬에 " + name + "이 없다");
                    Assert.IsTrue((bool)isPopup.Invoke(null, new object[] { child }), name + "을 팝업으로 못 본다");
                }

                // 화면(패널)은 팝업이 아니다 - 층위를 올리면 하단 탭 위로 올라간다
                foreach (var name in new[] { "ShopPanel", "GrowthPanel", "PanelBackdrop" })
                {
                    var child = safeArea.Find(name);
                    if (child == null) continue;
                    Assert.IsFalse((bool)isPopup.Invoke(null, new object[] { child }), name + "을 팝업으로 본다");
                }
            }
            finally
            {
                if (opened)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                        UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath), true);
            }
        }

        /** 씬이 열려 있으면 그것을, 아니면 덧붙여 연다(SkillVfxTests와 같은 규칙) */
        private static Transform FindSafeArea(out bool opened)
        {
            opened = false;
            var found = FindIn(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainScenePath));
            if (found != null) return found;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            opened = scene.IsValid();
            return FindIn(scene);
        }

        private static Transform FindIn(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var safe = root.transform.Find("SafeArea");
                if (safe != null) return safe;
            }
            return null;
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
