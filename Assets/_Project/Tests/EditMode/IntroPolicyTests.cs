using NUnit.Framework;
using Onikiri.Progression;
using Onikiri.UI;

namespace Onikiri.Tests
{
    /**
     * @brief 부팅 화면(인트로 스텝)에서 씬 없이 증명할 수 있는 전부.
     *
     * 이 스텝의 화면은 실기 GIF로 증명되지만, **분기는 여기서 증명한다** -
     * 전부 "첫 실행의 사람"이나 "세이브가 깨진 사람"에게만 드러나는 갈래라
     * 개발 중에는 아무도 안 밟기 때문이다(AccountLinkPolicy와 같은 사정).
     *
     * 지키는 계약 넷:
     *
     *   순서       스플래시는 탭이든 시간이든 앞으로만 간다. 타이틀은
     *              시간으로 지나가지 않는다 - 진입은 언제나 사람의 탭이다
     *   선택 1회   계정 선택은 처음 온 사람에게만 선다. 이미 고른 사람과
     *              이미 연동된 사람은 다시 묻지 않는다
     *   게이트     세이브 적용 전에는 못 들어간다. Firebase는 게이트에 없다
     *              (오프라인 안전 - 이 테스트가 그 부재의 증인이다)
     *   손상 보고  깨진 세이브는 조용히 새 게임이 되지 않는다 - 문구가 있다
     */
    public class IntroPolicyTests
    {
        // ---------------------------------------------------------------- 순서

        [Test]
        public void Splash_AdvancesByTime()
        {
            Assert.AreEqual(IntroPhase.Studio,
                IntroPolicy.Next(IntroPhase.Studio, tapped: false,
                                 IntroPolicy.StudioSeconds - 0.01f));
            Assert.AreEqual(IntroPhase.Brand,
                IntroPolicy.Next(IntroPhase.Studio, tapped: false, IntroPolicy.StudioSeconds));
            Assert.AreEqual(IntroPhase.Title,
                IntroPolicy.Next(IntroPhase.Brand, tapped: false, IntroPolicy.BrandSeconds));
        }

        [Test]
        public void Splash_TapSkipsImmediately()
        {
            // 재실행마다 보는 화면이다. 스킵을 막아서 얻는 것은 노출 몇 초,
            // 잃는 것은 매 실행의 짜증이다
            Assert.AreEqual(IntroPhase.Brand,
                IntroPolicy.Next(IntroPhase.Studio, tapped: true, 0f));
            Assert.AreEqual(IntroPhase.Title,
                IntroPolicy.Next(IntroPhase.Brand, tapped: true, 0f));
        }

        [Test]
        public void Title_NeverAdvancesByTimeOrTap()
        {
            // 타이틀 -> 게임은 이 함수의 일이 아니다. 로드 게이트를 지나는
            // 진입(IntroFlow.TryEnter)만이 그 문이다 - 시간 축에 진입 경로를
            // 만들면 게이트가 열리기 전에 게임이 드러나는 순서가 생긴다
            Assert.AreEqual(IntroPhase.Title,
                IntroPolicy.Next(IntroPhase.Title, tapped: true, 999f));
            Assert.AreEqual(IntroPhase.Entered,
                IntroPolicy.Next(IntroPhase.Entered, tapped: true, 999f));
        }

        // ---------------------------------------------------------------- 선택 1회

        [Test]
        public void FirstRun_ShowsAccountChoice()
        {
            Assert.IsTrue(IntroPolicy.ShowsAccountChoice(chosenBefore: false, linked: false));
        }

        [Test]
        public void ReturningUser_IsNotAskedAgain()
        {
            // 게스트로 골랐던 사람 - 매 실행 로그인을 묻는 것은 잔소리다.
            // 뒤늦은 연동 경로는 설정의 [구글 연동]이 상시로 연다
            Assert.IsFalse(IntroPolicy.ShowsAccountChoice(chosenBefore: true, linked: false));
        }

        [Test]
        public void LinkedUser_IsNotAskedEvenWithoutLocalRecord()
        {
            // 로컬 기록(PlayerPrefs)이 지워져도 Firebase 세션이 "연동됨"이라
            // 답하면 선택을 물을 이유가 없다 - 이미 답이 있다
            Assert.IsFalse(IntroPolicy.ShowsAccountChoice(chosenBefore: false, linked: true));
        }

        // ---------------------------------------------------------------- 게이트

        [Test]
        public void CannotEnter_BeforeSaveIsApplied()
        {
            // 로드 전에 들어가면 빈 초기 상태가 한 프레임 보인다 - 진행이
            // 날아간 것처럼 읽히는 최악의 첫인상이다
            Assert.IsFalse(IntroPolicy.CanEnter(saveLoaded: false, linkBusy: false));
            Assert.IsTrue(IntroPolicy.CanEnter(saveLoaded: true, linkBusy: false));
        }

        [Test]
        public void CannotEnter_WhileLinkDialogIsUp()
        {
            Assert.IsFalse(IntroPolicy.CanEnter(saveLoaded: true, linkBusy: true));
        }

        // Firebase가 게이트에 **없다**는 사실은 CanEnter의 시그니처가 증명한다 -
        // 인자 자체가 없어서, 넣으려면 이 파일이 먼저 깨진다 (오프라인 안전)

        // ---------------------------------------------------------------- 손상 보고

        [Test]
        public void BrokenSave_IsReportedNotSilent()
        {
            Assert.IsNotEmpty(IntroPolicy.WarningFor(SaveLoadOutcome.Corrupt));
            Assert.IsNotEmpty(IntroPolicy.WarningFor(SaveLoadOutcome.FutureVersion));

            // 두 문구는 달라야 한다. "읽지 못했다"와 "앱이 낡았다"는 사람이
            // 할 일이 다르다 - 전자는 문의, 후자는 업데이트
            Assert.AreNotEqual(IntroPolicy.WarningFor(SaveLoadOutcome.Corrupt),
                               IntroPolicy.WarningFor(SaveLoadOutcome.FutureVersion));
        }

        [Test]
        public void HealthySave_ShowsNoWarning()
        {
            Assert.IsEmpty(IntroPolicy.WarningFor(SaveLoadOutcome.NoFile));
            Assert.IsEmpty(IntroPolicy.WarningFor(SaveLoadOutcome.Loaded));
        }
    }
}
