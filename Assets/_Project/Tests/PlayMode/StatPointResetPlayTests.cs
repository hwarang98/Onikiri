using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 66단계 보석 초기화를 **씬의 버튼으로** 누른다.
     *
     * EditMode가 StatPointReset의 규칙(무료 1회 -> 150 보석, 한 번 차감)을 잰다면,
     * 여기는 빌더가 세운 버튼·팝업이 그 문에 실제로 닿는지와, 보석 차감이 60단계
     * urgent 동기화를 **한 번** 깨우는지를 본다(Cloud/ 무수정으로 잡혀야 한다).
     */
    public class StatPointResetPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string UrgentGemLog = "urgent 동기화 예약: 보석 소비";

        SaveSandbox sandbox;
        CharacterLevel character;
        GemWallet gems;
        Onikiri.UI.StatPointResetButton reset;
        readonly List<string> logs = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            // 세션을 떼어 저장·커밋 경로를 끊는다(HitRatingPlayTests와 같은 이유)
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);

            character = CharacterLevel.Instance;
            gems = GemWallet.Instance;

            // urgent 구독은 세션의 부팅 선택 뒤(BootWith)에 걸린다 - 첫 프레임에
            // 끝났다는 보장이 없어 같은 문을 직접 부른다. 멱등이다(중복 구독을
            // 스스로 지운다). Cloud 쪽 코드는 한 줄도 안 바뀐다
            Onikiri.Cloud.CloudSaveSync.Wire(null, null, null, gems);
            reset = Object.FindFirstObjectByType<Onikiri.UI.StatPointResetButton>(FindObjectsInactive.Include);

            Assert.IsNotNull(character, "씬에 CharacterLevel이 없다");
            Assert.IsNotNull(gems, "씬에 GemWallet이 없다");
            Assert.IsNotNull(reset, "씬에 초기화 버튼이 없다 - UpgradePanelBuilder를 다시 돌릴 것");

            // 성장 탭을 연 상태로 만든다. 버튼은 꺼진 페이지 안에 저장돼 있어서,
            // 조상까지 켜야 Start(리스너 연결)가 돈다 - 탭을 누르는 것과 같은 일이다
            for (var t = reset.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            yield return null;
            Assert.IsTrue(reset.isActiveAndEnabled, "초기화 버튼이 켜지지 않았다");

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

        void Capture(string message, string stack, LogType type)
        {
            logs.Add(message);
        }

        int UrgentGemLogs()
        {
            int n = 0;
            foreach (var line in logs) if (line.Contains(UrgentGemLog)) n++;
            return n;
        }

        Button Field(string name)
        {
            var field = typeof(Onikiri.UI.StatPointResetButton).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            var button = field.GetValue(reset) as Button;
            Assert.IsNotNull(button, name + " 배선이 비었다");
            return button;
        }

        /** 다섯 축에 4점씩 찍은 상태. 포인트는 레벨을 부어 만든다 */
        void SpendTwenty(int resetCount)
        {
            // 경험치 축은 Lv.15에 열린다(StatPointCurve.ExpUnlockLevel) - 다섯 축에
            // 다 찍으려면 그 레벨까지 올라 있어야 한다
            while (character.UnspentPoints < 20 || character.Level < StatPointCurve.ExpUnlockLevel)
            {
                character.AddExp(character.ExpRequired);
                character.ClaimLevelUps();
            }

            character.Restore(character.Level, character.Exp, 0, 0, 0, 0, 0, resetCount);
            foreach (var axis in CharacterLevel.AxisIds) Assert.AreEqual(4, character.TrySpendPoints(axis, 4));
        }

        [UnityTest]
        public IEnumerator PaidReset_ReturnsPoints_SpendsGemsOnce_AndRequestsUrgentSync()
        {
            SpendTwenty(1);   // 무료는 이미 썼다
            gems.SetBalance(500L);
            yield return null;

            int total = character.UnspentPoints + character.SpentPoints;
            logs.Clear();

            Field("openButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(reset.IsPopupOpen, "버튼을 눌렀는데 확인 팝업이 안 떴다");
            Assert.AreEqual(20, character.SpentPoints, "팝업만 열었는데 포인트가 돌아왔다");

            Field("confirmButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(StatPointReset.Result.ResetPaid, reset.LastResult);
            Assert.AreEqual(0, character.SpentPoints);
            Assert.AreEqual(total, character.UnspentPoints, "포인트가 전부 미배분으로 돌아오지 않았다");
            Assert.AreEqual(500L - StatPointCurve.ResetGemCost, gems.Gems);
            Assert.IsFalse(reset.IsPopupOpen, "초기화 뒤에도 팝업이 남았다");

            Assert.AreEqual(1, UrgentGemLogs(), "보석 차감이 urgent 동기화를 한 번 깨우지 않았다:\n"
                                                + string.Join("\n", logs));
        }

        [UnityTest]
        public IEnumerator FreeReset_ReturnsPoints_WithoutTouchingGems()
        {
            SpendTwenty(0);
            gems.SetBalance(500L);
            yield return null;
            logs.Clear();

            Field("openButton").onClick.Invoke();
            yield return null;
            Field("confirmButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(StatPointReset.Result.ResetFree, reset.LastResult);
            Assert.AreEqual(0, character.SpentPoints);
            Assert.AreEqual(500L, gems.Gems, "무료 초기화가 보석을 뺐다");
            Assert.AreEqual(0, UrgentGemLogs(), "보석이 안 줄었는데 urgent가 걸렸다");
            Assert.IsFalse(character.IsNextResetFree);
        }

        [UnityTest]
        public IEnumerator ShortOfGems_KeepsPopupOpen_AndChangesNothing()
        {
            SpendTwenty(1);
            gems.SetBalance(StatPointCurve.ResetGemCost - 1);
            yield return null;
            logs.Clear();

            Field("openButton").onClick.Invoke();
            yield return null;

            Assert.IsFalse(Field("confirmButton").interactable, "보석이 모자란데 확인 버튼이 눌린다");

            reset.Confirm();   // 눌리지 않는 버튼을 우회해도 문이 막는다
            yield return null;

            Assert.AreEqual(StatPointReset.Result.NotEnoughGems, reset.LastResult);
            Assert.AreEqual(20, character.SpentPoints);
            Assert.AreEqual(StatPointCurve.ResetGemCost - 1, gems.Gems);
            Assert.IsTrue(reset.IsPopupOpen, "보석 부족이 팝업을 닫아 버렸다 - 이유가 화면에서 사라진다");
            Assert.AreEqual(0, UrgentGemLogs());
        }
    }
}
