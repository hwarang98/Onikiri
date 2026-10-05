using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 인트로 진입(IntroFlow.Entered)이 **죽은 구독자에 걸려 넘어지지 않는다** (69단계).
     *
     * 도메인 리로드를 끈 에디터에서 PlayMode 테스트를 돌린 뒤 Play해 인트로를
     * 건너뛰면 MissingReferenceException이 났다. 꺼진 계층의 OfflineRewardPopup이
     * Show로 구독만 하고 한 번도 Awake되지 않은 채 파괴되면 OnDestroy가 불리지
     * 않아(유니티 규칙) 정적 이벤트에 죽은 객체가 남았다.
     *
     * 그 모양을 그대로 만든다 - 꺼진 객체에 붙인 팝업이 구독하고, 깨어나지 않은 채
     * 파괴되고, 인트로가 들어온다. 예외가 0이어야 하고, 같은 이벤트의 뒤
     * 구독자까지 불려야 한다.
     */
    public class IntroSubscriberPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        SaveSandbox sandbox;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;
        }

        [TearDown]
        public void Restore()
        {
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        [UnityTest]
        public IEnumerator Entering_SkipsAPopupDestroyedBeforeItWoke()
        {
            var intro = UnityEngine.Object.FindFirstObjectByType<IntroFlow>(FindObjectsInactive.Include);
            Assert.IsNotNull(intro, "씬에 인트로가 없다");
            Assert.IsFalse(IntroFlow.HasEntered, "씬을 막 열었는데 이미 들어온 상태다 - 재현의 전제가 깨졌다");

            // 꺼진 객체에 붙인다 - Awake가 안 돈다
            var host = new GameObject("DeadOfflinePopup");
            host.SetActive(false);
            var popup = host.AddComponent<OfflineRewardPopup>();
            popup.Show(BigDouble.FromDouble(123d), TimeSpan.FromHours(2), false);

            // 깨어나지 않은 채 파괴 - OnDestroy가 불리지 않는다
            UnityEngine.Object.DestroyImmediate(host);
            yield return null;

            bool laterSubscriberRan = false;
            Action later = () => laterSubscriberRan = true;
            IntroFlow.Entered += later;
            try
            {
                // 세션 로드를 기다리지 않고 진입만 태운다(SkipToGame은 로드 전이면 미룬다)
                typeof(IntroFlow).GetMethod("Enter", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(intro, null);
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail("인트로 진입이 죽은 구독자에서 넘어졌다: " + e.InnerException);
            }
            finally
            {
                IntroFlow.Entered -= later;
            }

            Assert.IsTrue(IntroFlow.HasEntered);
            Assert.IsTrue(laterSubscriberRan, "죽은 구독자 뒤의 구독자가 불리지 않았다");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
