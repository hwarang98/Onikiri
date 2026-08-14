using NUnit.Framework;
using Onikiri.Battle;

namespace Onikiri.Tests
{
    /**
     * @brief 보스 처치가 **클리어로 세어지는가**. 진행이 서 있는 자리다.
     *
     * 이 판정이 틀렸을 때의 증상은 예외도 경고도 아니고 **진행 정지**다.
     * 보스는 화면에서 멀쩡히 죽고, 골드도 경험치도 들어오고, 퀘스트 카운터도
     * 오른다(그 셋은 스포너가 준다). 스테이지만 안 오른다 - 무엇이 잘못됐는지
     * 화면에 아무 단서가 없는 종류의 고장이다.
     *
     * ## 무엇이 실제로 있었던 일인가
     *
     * BossFight는 처치를 `phase == Fighting`일 때만 받았다. 그런데 보스는
     * 전선에 닿아야 Fighting이 되고(x = 0.05), 사무라이의 칼은 그보다 앞까지
     * 닿는다(x = 0.8까지). 동료는 더 멀리 닿는다. 그 사이 구간에서 죽은 보스는
     * 처치가 통째로 버려졌고, **강한 빌드일수록 첫 타격이 거기 떨어졌다.**
     *
     * 그래서 이 테스트가 지키는 것은 숫자가 아니라 집합이다: 보스가 필드에
     * 서 있는 모든 상태에서 그 죽음이 클리어여야 한다.
     */
    public class BossClearTests
    {
        /**
         * 접근 중 처치 - 이 테스트가 존재하는 이유다. 여기가 false로 돌아가면
         * 앞서 나간 플레이어의 진행이 조용히 멈춘다
         */
        [Test]
        public void KillDuringApproach_CountsAsClear()
        {
            Assert.IsTrue(BossFight.CountsAsClear(BossFight.Phase.Approaching),
                "달려오는 중에 벤 보스가 클리어로 안 세어진다 - 스테이지가 안 오른다");
        }

        [Test]
        public void KillDuringFight_CountsAsClear()
        {
            Assert.IsTrue(BossFight.CountsAsClear(BossFight.Phase.Fighting));
        }

        /**
         * 보스가 필드에 없는 상태들. 여기서 참이 되면 한 번의 처치가 두 번
         * 세어질 수 있다 - Cleared는 이미 스테이지를 올린 뒤의 상태다
         */
        [Test]
        public void PhasesWithoutABossOnTheField_DoNotCount()
        {
            Assert.IsFalse(BossFight.CountsAsClear(BossFight.Phase.Farming), "파밍 중에는 보스가 없다");
            Assert.IsFalse(BossFight.CountsAsClear(BossFight.Phase.Intro), "연출 중에는 아직 스폰 전이다");
            Assert.IsFalse(BossFight.CountsAsClear(BossFight.Phase.Cleared), "이미 올린 스테이지를 또 올린다");
            Assert.IsFalse(BossFight.CountsAsClear(BossFight.Phase.Failed), "실패한 판이 클리어가 된다");
        }
    }
}
