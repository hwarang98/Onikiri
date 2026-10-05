using System;
using System.Collections;
using System.Collections.Generic;
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
     * @brief 다른 uid의 사이드카를 든 채 **실제 씬이 부팅한다** (71단계).
     *
     * 70단계 실기의 모양: 자동 백업이 세이브와 옛 uid의 사이드카를 되살렸고
     * 앱은 새 uid로 떴다. 여기서 재는 계약은 셋이다.
     *
     *   - 부팅 적용 1회 · 방치 보상 1회 (59단계) - 입구가 부팅을 두 번 돌리지 않는다
     *   - 부팅이 끝나면 새 uid의 사이드카가 디스크에 있고 기기 id가 게이트와 같다
     *   - **다음 실행의 세션 게이트가 묻지 않는다** - 같은 폰의 이전 실행이 아직
     *     살아 있는 것으로 보여도(release 없이 죽음) 디스크의 기기 id가 같으므로
     *     인수 확인 팝업 0회. 70단계에서는 이 자리에서 거의 매 부팅 팝업이 떴다.
     *
     * 서버·세션은 가짜다. 운영 프로젝트에 닿지 않는다.
     */
    public class CloudForeignSidecarPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        const string OldUid = "old-anon-uid-70";
        const string NewUid = "new-anon-uid-71";

        static readonly TimeSpan Away = TimeSpan.FromHours(3d);

        SaveSandbox sandbox;
        GameSession session;
        CloudTakeoverPanel takeoverPanel;

        [UnitySetUp]
        public IEnumerator BootWithAForeignSidecar()
        {
            sandbox = new SaveSandbox();

            SaveData local = Local();
            SaveSystem.Save(local);

            var old = CloudSaveLocalState.NewFor(OldUid, CloudSaveIds.New());
            old.MarkSynced(7L, CloudSaveFingerprint.HashOf("old-payload"),
                           CloudSaveFingerprint.StateHashOf(local), 0L);
            Assert.IsTrue(CloudSaveSidecar.Save(old));

            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveTakeover.ResetForTests();
            CloudSavePlayLock.ResetForTests();
            CloudSaveRecovery.ResetForTests();

            CloudSaveCoordinator.UseIdentityForTests(NewUid);
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Missing
            }));

            CloudTakeoverPanel.SuppressReloadForTests = true;

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            session = UnityEngine.Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            takeoverPanel = UnityEngine.Object.FindFirstObjectByType<CloudTakeoverPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(session, "씬에 GameSession이 없다");
            Assert.IsNotNull(takeoverPanel, "씬에 CloudTakeoverPanel이 없다");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!session.IsLoaded && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(session.IsLoaded, "제한 시간 안에 부팅이 끝나지 않았다");
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (session != null) UnityEngine.Object.DestroyImmediate(session);

            CloudSavePlayLock.ResetForTests();
            CloudSaveTakeover.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudSaveRecovery.ResetForTests();

            CloudTakeoverPanel.SuppressReloadForTests = false;

            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        /** ★ 입구가 있어도 부팅 적용 1회 · 방치 보상 1회 */
        [Test]
        public void TheBootAppliesOnceAndPaysOnce()
        {
            Assert.AreEqual(1, CloudSaveRecovery.BootEntries, "복구 입구는 정확히 한 번");
            Assert.AreEqual(1, session.BootApplyCount, "부팅 적용이 한 번이 아니다");
            Assert.AreEqual(1, session.OfflineRewardGrants, "방치 보상이 한 번이 아니다");
            Assert.AreEqual(CloudSaveState.Dirty, CloudSaveCoordinator.State,
                "새 uid의 서버에 없다 - 로컬이 첫 정본(③)");
        }

        /** ★ 부팅이 끝나면 새 uid의 사이드카가 디스크에 있고, 기기 id는 이 실행의 것이다 */
        [Test]
        public void TheNewSidecarIsOnDiskWithThisRunsDeviceId()
        {
            CloudSaveLocalState disk = CloudSaveSidecar.Load();

            Assert.IsNotNull(disk);
            Assert.AreEqual(NewUid, disk.ownerUid);
            Assert.AreEqual(CloudSaveSession.DeviceIdFor(NewUid), disk.deviceId);
            Assert.IsTrue(System.IO.File.Exists(CloudSaveSidecar.PreRecoverPath), "옛 사이드카 보존");
        }

        /**
         * ★★ **다음 실행은 묻지 않는다** - 인수 확인 팝업 0회 (단일 기기).
         *
         * 이전 실행이 release 없이 죽어 세션 문서가 아직 살아 있다(heartbeat 5초 전).
         * owner 기기 id = 디스크의 사이드카. 새 실행(세션 정적 상태 비움)의 게이트가
         * 같은 기기로 읽고 그냥 잡아야 한다.
         */
        [UnityTest]
        public IEnumerator TheNextRunsGateDoesNotAskTheSamePhone()
        {
            string diskDevice = CloudSaveSidecar.Load().deviceId;

            // ---- 새 프로세스: 이 실행의 세션 id·메모리 기기 id가 비어 있다
            CloudSaveSession.ResetForTests();
            CloudSaveTakeover.ResetForTests();

            var phases = new List<CloudSaveTakeoverPhase>();
            CloudSaveTakeover.PhaseChanged += phase => phases.Add(phase);

            CloudSaveSession.UsePeekForTests(uid => new CloudSaveSessionSnapshot
            {
                exists = true,
                ownerSessionId = new string('7', 32),     // 이전 실행
                ownerDeviceId = diskDevice,               // 같은 폰
                released = false,
                generation = 1L,
                takeoverSessionId = string.Empty,
                takeoverDeviceId = string.Empty,
                secondsSinceHeartbeat = 5f,
                secondsSinceTakeoverRequest = -1f
            });
            CloudSaveSession.UseWriteForTests(mode => CloudSaveSessionStatus.TookOver);

            string gateDevice = CloudSaveSession.DeviceIdFor(NewUid);
            Assert.AreEqual(diskDevice, gateDevice, "게이트가 디스크의 기기 id를 읽어야 한다");

            session.StartCoroutine(CloudSaveTakeover.EnsureSession(NewUid, gateDevice));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (CloudSaveTakeover.Phase != CloudSaveTakeoverPhase.Acquired
                   && CloudSaveTakeover.Phase != CloudSaveTakeoverPhase.AwaitingConfirm
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            yield return null;

            CollectionAssert.DoesNotContain(phases, CloudSaveTakeoverPhase.AwaitingConfirm,
                "★ 같은 폰에게 인수를 물었다 - 70단계 증상");
            Assert.AreEqual(CloudSaveTakeoverPhase.Acquired, CloudSaveTakeover.Phase);
            Assert.IsFalse(takeoverPanel.IsShowing, "인수 확인 팝업이 떴다");
        }

        static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            data.characterLevel = 7;
            data.goldPerSecond = 100d;
            data.expPerSecond = 0d;
            data.lastQuitUtcTicks = DateTime.UtcNow.Subtract(Away).Ticks;
            return data;
        }
    }
}
