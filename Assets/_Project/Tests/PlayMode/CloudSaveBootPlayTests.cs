using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 59단계의 ★ 계약을 **실제 부팅에서** 잰다.
     *
     *     클라우드를 채택했을 때 방치 보상이 **정확히 한 번**,
     *     그리고 그 한 번이 **클라우드 기준**인가.
     *
     * 순수 함수로는 못 재는 것이 이것이다. 이중 지급은 "고르기와 적용의 순서"가
     * 만드는 사고라, 실제 씬에서 `GameSession`이 도는 것을 봐야 한다 - 예전
     * 구조(즉시 Apply 후 클라우드 덮어쓰기)에서는 로컬 기준으로 한 번, 클라우드
     * 기준으로 또 한 번 나갔다.
     *
     * 서버는 가짜다(`CloudSaveCoordinator.UseFetchForTests`). 이 검사가 필요로
     * 하는 것은 네트워크가 아니라 **다른 세이브 한 벌**이기 때문이고, 그래서
     * 운영 프로젝트에 한 번도 닿지 않는다.
     *
     * ⚠️ 실사용 세이브를 밟지 않는다 - 백업/복원이 아니라 **경로째 격리**다
     * (61단계 S5-0, SaveSandbox). `GameSession`은 여전히 TearDown에서 파괴한다 -
     * `OnApplicationQuit`이 TearDown **뒤에** 오고, 그 시점에는 루트가 이미
     * 실사용으로 돌아가 있기 때문이다(PromotionTrialPlayTests가 그 순서에 두 번 물렸다).
     */
    public class CloudSaveBootPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        const string TestUid = "test-uid-59";
        const double LocalGoldPerSecond = 100d;
        const double CloudGoldPerSecond = 1000d;
        const int CloudFrontier = 171;

        static readonly TimeSpan Away = TimeSpan.FromHours(3d);

        SaveSandbox sandbox;
        string savePath;

        GameSession session;

        [UnitySetUp]
        public IEnumerator BootWithAnAdvancedCloudSave()
        {
            sandbox = new SaveSandbox();
            savePath = SaveSystem.Path;

            // ---- 로컬: 3시간 전에 껐고, 그때 초당 100을 벌고 있었다
            SaveData local = Local();
            SaveSystem.Save(local);

            // ---- sidecar: 이 기기는 서버 rev 5까지 동기화했고 그 뒤로 논 적이 없다
            var sidecar = CloudSaveLocalState.NewFor(TestUid, CloudSaveIds.New());
            sidecar.MarkSynced(5L,
                               CloudSaveFingerprint.HashOf(CloudSaveFingerprint.Serialize(local)),
                               CloudSaveFingerprint.StateHashOf(local), 0L);
            CloudSaveSidecar.Save(sidecar);

            // ---- 서버: 다른 기기가 rev 6까지 갔다 (171층 · 초당 1000)
            CloudSaveEnvelope envelope = CloudSaveEnvelope.ForUpload(
                Cloud(), 5L, CloudSaveIds.New(), CloudSaveIds.New(), CloudSaveIds.New());

            Assert.IsNotNull(envelope, "테스트용 클라우드 봉투를 만들지 못했다");

            CloudSaveCoordinator.ResetForTests();
            CloudSaveCoordinator.UseIdentityForTests(TestUid);
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = envelope
            }));

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            session = UnityEngine.Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            Assert.IsNotNull(session, "씬에 GameSession이 없다");

            // 부팅은 코루틴이다(서버 확인을 기다린다). 게이트가 열릴 때까지 기다린다
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!session.IsLoaded && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(session.IsLoaded, "제한 시간 안에 부팅이 끝나지 않았다");
        }

        [TearDown]
        public void Restore()
        {
            // **시간·오디오 잔재를 여기서 끊는다.** 이 클래스의 씬들이 세운
            // IntroFlow가 배속 0과 오디오 정지를 걸고, 이 클래스의 대기는 전부
            // realtime이라 그 잔재를 스스로는 못 느낀다 - 그대로 두면 다음
            // 클래스(귀문)의 게임 시간이 안 흘러 장시간 검사 다섯이 무너진다
            // (배치 전량 실행에서 실측으로 물렸다).
            Time.timeScale = 1f;
            AudioListener.pause = false;

            // 컴포넌트를 먼저 죽인다 - 살아 있으면 플레이 모드를 나가는 순간
            // 이 테스트의 상태가 실사용 세이브 위에 저장된다 (루트가 이미
            // 실사용으로 돌아간 뒤라 sandbox도 못 막는다)
            if (session != null) UnityEngine.Object.DestroyImmediate(session);

            CloudSaveCoordinator.ResetForTests();

            // 루트를 되돌리고 실사용 파일이 그대로인지 검사한다 (S5-0)
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        /** ★ 부팅 적용은 한 번, 방치 보상도 한 번 */
        [Test]
        public void TheBootAppliesOnceAndPaysOnce()
        {
            Assert.AreEqual(1, session.BootApplyCount, "부팅 적용이 한 번이 아니다");
            Assert.AreEqual(1, session.OfflineRewardGrants, "방치 보상이 한 번이 아니다");
        }

        /**
         * ★★ 지급된 보상이 **클라우드 기준**이다.
         *
         * 로컬 기준으로 먼저 한 번 나갔다면 이 값이 1/10이거나, 두 번 나갔다면
         * 합계가 더 크다. 금액 하나가 순서 전체를 증언한다.
         */
        [Test]
        public void TheOfflinePayoutUsedTheAdoptedSave()
        {
            double expected = IdleIncome.Reward(CloudGoldPerSecond, Away).ToDouble();
            double localWould = IdleIncome.Reward(LocalGoldPerSecond, Away).ToDouble();

            Assert.AreEqual(expected, session.LastOfflineReward.ToDouble(), expected * 0.02d,
                "클라우드의 초당 수입으로 지급돼야 한다");
            Assert.Greater(session.LastOfflineReward.ToDouble(), localWould * 5d,
                "로컬 기준으로 지급됐다면 이 값이 나올 수 없다");
        }

        /** 채택된 진행이 실제로 시스템에 올라갔다 */
        [Test]
        public void TheAdoptedSaveIsTheOneRunningInTheScene()
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);

            Assert.IsNotNull(stage, "씬에 StageProgress가 없다");
            Assert.AreEqual(CloudFrontier, stage.MaxStageReached, "클라우드의 최전선이어야 한다");

            Assert.IsNotNull(CloudSaveCoordinator.LastChoice);
            Assert.IsTrue(CloudSaveCoordinator.LastChoice.fromCloud);
            Assert.AreEqual(CloudSaveState.InSync, CloudSaveCoordinator.State);
        }

        /** 클라우드를 얹기 전에 로컬 원본이 옆에 남았다 */
        [Test]
        public void TheLocalSaveWasBackedUpBeforeBeingOverwritten()
        {
            Assert.IsTrue(File.Exists(CloudSaveCoordinator.PreCloudBackupPath),
                "덮이기 직전의 로컬 세이브가 없다 - 클라우드 채택이 되돌릴 수 없는 동작이 된다");

            var backup = JsonUtility.FromJson<SaveData>(
                File.ReadAllText(CloudSaveCoordinator.PreCloudBackupPath));

            Assert.IsNotNull(backup);
            Assert.AreEqual(12, backup.maxStageReached, "백업은 **로컬** 기록이어야 한다");
        }

        /** 부팅이 끝나면 고른 한 벌이 곧바로 디스크의 정본이다 */
        [Test]
        public void TheChosenSaveIsOnDiskRightAfterBoot()
        {
            var onDisk = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));

            Assert.IsNotNull(onDisk);
            Assert.AreEqual(CloudFrontier, onDisk.maxStageReached);
        }

        // ---------------------------------------------------------------- 도구

        static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            data.characterLevel = 7;
            data.goldPerSecond = LocalGoldPerSecond;
            data.expPerSecond = 0d;
            data.lastQuitUtcTicks = DateTime.UtcNow.Subtract(Away).Ticks;
            return data;
        }

        static SaveData Cloud()
        {
            var data = SaveData.NewGame();
            data.stage = CloudFrontier;
            data.maxStageReached = CloudFrontier;
            data.characterLevel = 48;
            data.goldPerSecond = CloudGoldPerSecond;
            data.expPerSecond = 0d;
            data.lastQuitUtcTicks = DateTime.UtcNow.Subtract(Away).Ticks;
            return data;
        }
    }
}
