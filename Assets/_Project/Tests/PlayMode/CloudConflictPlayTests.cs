using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 충돌 세 갈래(60단계)와 S4-0-2 취약점 폐쇄를 **실제 씬에서** 잰다.
     *
     * 서버는 가짜다(fetch override) - 이 검사가 필요로 하는 것은 네트워크가
     * 아니라 "갈라진 두 세이브"이고, 그래서 운영 프로젝트에 닿지 않는다.
     *
     * 세 갈래의 파일·sidecar 효과는 `SuppressReloadForTests`로 재로드를 멈춘 채
     * 재고, **재로드가 계약을 지키는가**(ApplyBoot 1회·보상 1회)는 마지막
     * 검사가 실제 LoadScene으로 잰다.
     */
    public class CloudConflictPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string TestUid = "test-uid-60";

        SaveSandbox sandbox;
        string savePath;

        GameSession session;
        CloudConflictPanel panel;
        CloudSaveEnvelope serverEnvelope;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // 실사용 세이브와의 접촉을 끊는다 (61단계 S5-0). 백업/복원이 아니라
            // 경로째 격리라, 이 검사가 도중에 죽어도 실사용 파일은 그대로다
            sandbox = new SaveSandbox();
            savePath = SaveSystem.Path;

            // ---- 갈라진 상태: 로컬 12층 / 서버 rev 6·171층, sidecar는 rev 5
            SaveData local = Local();
            SaveSystem.Save(local);

            var sidecar = CloudSaveLocalState.NewFor(TestUid, CloudSaveIds.New());
            sidecar.MarkSynced(5L, CloudSaveFingerprint.HashOf("old-payload"),
                               CloudSaveFingerprint.HashOf("old-state"), 0L);
            CloudSaveSidecar.Save(sidecar);

            serverEnvelope = CloudSaveEnvelope.ForUpload(
                Cloud(), 5L, CloudSaveIds.New(), CloudSaveIds.New(), CloudSaveIds.New());
            Assert.IsNotNull(serverEnvelope);

            CloudSaveCoordinator.ResetForTests();
            CloudSaveCoordinator.UseIdentityForTests(TestUid);
            CloudSaveSync.ResetForTests();
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = serverEnvelope
            }));
            CloudConflictPanel.SuppressReloadForTests = true;

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            session = UnityEngine.Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            panel = UnityEngine.Object.FindFirstObjectByType<CloudConflictPanel>(FindObjectsInactive.Include);

            Assert.IsNotNull(session, "씬에 GameSession이 없다");
            Assert.IsNotNull(panel, "씬에 CloudConflictPanel이 없다 - Build Hud Screens를 돌렸는가");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!session.IsLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(session.IsLoaded);
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

            if (session != null) UnityEngine.Object.DestroyImmediate(session);

            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudConflictPanel.SuppressReloadForTests = false;

            // 루트를 되돌리고 **실사용 파일이 그대로인지 검사한다** - 격리가
            // 뚫렸으면 여기서 실패로 적힌다 (S5-0 회귀 방지)
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ---------------------------------------------------------------- 부팅

        /** 갈라진 부팅은 로컬로 들어가고, 자동으로 아무것도 쓰지 않는다 */
        [Test]
        public void AConflictedBootEntersLocallyWithoutWriting()
        {
            Assert.AreEqual(CloudSaveState.Conflict, CloudSaveCoordinator.State);
            Assert.AreEqual(1, session.BootApplyCount);

            var stage = UnityEngine.Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
            Assert.AreEqual(12, stage.MaxStageReached, "로컬(12층)로 들어가야 한다");

            var onDisk = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
            Assert.AreEqual(12, onDisk.maxStageReached, "디스크도 로컬 그대로다");
        }

        /** 갈라진 상태에서는 debounce·urgent 모두 커밋이 나가지 않는다 */
        [Test]
        public void AConflictStopsTheAutoSync()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State));
        }

        // ---------------------------------------------------------------- 세 갈래

        /** 클라우드 채택: 로컬은 3벌 순환 백업으로, 디스크는 클라우드로, sidecar는 서버 head로 */
        [UnityTest]
        public IEnumerator ChoosingCloudAdoptsTheServerBranch()
        {
            Assert.IsTrue(panel.Show(serverEnvelope));
            panel.ChooseCloud();
            yield return null;

            var onDisk = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
            Assert.AreEqual(171, onDisk.maxStageReached, "디스크가 클라우드 기록이 됐다");

            var backup = JsonUtility.FromJson<SaveData>(
                File.ReadAllText(CloudSaveCoordinator.BackupPathAt(1)));
            Assert.AreEqual(12, backup.maxStageReached, "덮이기 전의 로컬이 백업으로 남았다");

            var sidecar = CloudSaveSidecar.Load();
            Assert.IsNotNull(sidecar);
            Assert.AreEqual(6L, sidecar.baseRevision, "sidecar가 서버 head(rev 6)를 채택했다");
        }

        /** 현재 기기 채택: 디스크는 그대로, sidecar만 서버 head 위로 - 다음 커밋이 rev 7 */
        [UnityTest]
        public IEnumerator ChoosingLocalStacksOnTopOfTheServerHead()
        {
            Assert.IsTrue(panel.Show(serverEnvelope));
            panel.ChooseLocal();
            yield return null;

            var onDisk = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
            Assert.AreEqual(12, onDisk.maxStageReached, "디스크는 로컬 그대로다");

            var sidecar = CloudSaveSidecar.Load();
            Assert.AreEqual(6L, sidecar.baseRevision,
                "다음 커밋이 rev 7로 나간다 - 기존 서버 정본은 그 트랜잭션이 백업 문서로 옮긴다");

            Assert.IsTrue(File.Exists(CloudSaveCoordinator.BackupPathAt(1)),
                "이 갈래도 선택 전 로컬을 백업으로 남긴다");
        }

        /** 나중에 결정: 화면만 닫히고, Conflict 상태가 유지돼 클라우드 쓰기가 멈춰 있다 */
        [Test]
        public void ChoosingLaterKeepsPlayingLocallyWithWritesStopped()
        {
            Assert.IsTrue(panel.Show(serverEnvelope));
            panel.ChooseLater();

            Assert.IsFalse(panel.gameObject.activeSelf);
            Assert.AreEqual(CloudSaveState.Conflict, CloudSaveCoordinator.State);
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State),
                "보류 = 로컬 플레이만, 클라우드 쓰기 정지");
            Assert.AreEqual("기록 선택 필요", CloudSaveSync.StatusLine);
        }

        /**
         * ★ 선택 뒤 **씬 재로드**가 계약을 지킨다 - ApplyBoot 1회 + 방치 보상 1회.
         *
         * 전투 중 부분 Restore가 없는 이유가 이 재로드다. 새 부팅이 처음부터
         * 다시 지나므로 59단계의 단일 Apply 계약이 그대로 성립한다.
         */
        [UnityTest]
        public IEnumerator TheReloadAfterChoosingBootsExactlyOnce()
        {
            Assert.IsTrue(panel.Show(serverEnvelope));
            CloudConflictPanel.SuppressReloadForTests = false;

            // 재로드 뒤의 부팅은 "로컬(=클라우드 채택본) == 서버" 라 InSync다
            panel.ChooseCloud();
            yield return null;   // LoadScene은 다음 프레임에 뜬다
            yield return null;

            session = UnityEngine.Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            Assert.IsNotNull(session, "재로드된 씬에 GameSession이 없다");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!session.IsLoaded && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(1, session.BootApplyCount, "재로드 부팅도 정확히 1회다");
            Assert.LessOrEqual(session.OfflineRewardGrants, 1, "방치 보상도 1회를 넘지 않는다");

            var stage = UnityEngine.Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
            Assert.AreEqual(171, stage.MaxStageReached, "재로드 뒤에는 채택된 기록으로 서 있다");
        }

        // ---------------------------------------------------------------- S4-0-2

        /**
         * ★ 부팅 코루틴이 도중에 죽어도 **세이브는 유실되지 않는다.**
         *
         * 59단계 §10이 남긴 취약점의 폐쇄 증명이다. fetch가 영영 안 끝나는
         * 상태에서 GameSession을 파괴하면 - loaded가 false라 어떤 저장 경로도
         * 파일을 건드리지 못하고, 디스크는 바이트 그대로다.
         */
        [UnityTest]
        public IEnumerator DestroyingTheSessionMidBootLosesNothing()
        {
            // 이 검사만의 부팅을 다시 만든다: 영영 안 끝나는 서버 확인
            var never = new TaskCompletionSource<CloudSaveFetchResult>();
            CloudSaveCoordinator.UseFetchForTests(uid => never.Task);

            byte[] before = File.ReadAllBytes(savePath);

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;   // 부팅 코루틴이 fetch를 기다리는 중이다

            session = UnityEngine.Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            Assert.IsNotNull(session);
            Assert.IsFalse(session.IsLoaded, "아직 서버를 기다리는 중이어야 한다");

            // 부팅 도중 파괴 - 코루틴이 함께 죽는다
            UnityEngine.Object.DestroyImmediate(session);
            session = null;
            yield return null;

            byte[] after = File.ReadAllBytes(savePath);
            Assert.AreEqual(Convert.ToBase64String(before), Convert.ToBase64String(after),
                "부팅이 끊겨도 세이브 파일은 바이트 그대로여야 한다");
        }

        // ---------------------------------------------------------------- 도구

        static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            data.characterLevel = 7;
            data.gems = 320L;
            data.goldPerSecond = 100d;
            data.lastQuitUtcTicks = DateTime.UtcNow.AddMinutes(-30d).Ticks;
            return data;
        }

        static SaveData Cloud()
        {
            var data = SaveData.NewGame();
            data.stage = 171;
            data.maxStageReached = 171;
            data.characterLevel = 48;
            data.evolutionTier = 3;
            data.gems = 540L;
            data.goldPerSecond = 1000d;
            data.lastQuitUtcTicks = DateTime.UtcNow.AddMinutes(-30d).Ticks;
            return data;
        }
    }
}
