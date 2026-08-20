using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 테스트 세이브 경로 격리 (61단계 S5-0) - **회귀 방지 검사.**
     *
     * 60단계 §9에서 TearDown이 못 돌아 실사용 세이브가 테스트 세이브로 덮였다.
     * 근본 수정은 검사가 실사용 경로를 아예 안 쓰는 것이고(SaveSandbox +
     * SaveSystem.Root), 이 파일은 그 격리가 다시 뚫리면 실패하는 자리다:
     *
     *   - 루트를 갈아 끼우면 세이브에서 파생되는 **모든** 경로가 따라오는가
     *   - 격리 안의 읽기/쓰기/삭제가 실사용 파일을 못 만지는가
     *   - 격리가 풀리면 실사용 경로로 정확히 돌아오는가
     *
     * SaveSandbox.Dispose 자체도 검사다 - 실사용 파일이 바이트 하나라도
     * 달라졌으면 예외를 던져 그 검사를 실패로 적는다. 여기의 검사들은 그
     * 장치가 서 있는지를 다시 잰다.
     */
    public class SavePathIsolationTests
    {
        /** 루트 하나를 갈아 끼우면 파생 경로 전부가 따라온다 - 하나라도 남으면 구멍이다 */
        [Test]
        public void OverridingTheRootMovesEveryDerivedPath()
        {
            string realRoot = Application.persistentDataPath;

            using (var sandbox = new SaveSandbox())
            {
                foreach (string path in new[]
                {
                    SaveSystem.Path,
                    SaveSystem.BackupPath,
                    CloudSaveSidecar.Path,
                    CloudSaveCoordinator.PreCloudBackupPath,
                    CloudSaveCoordinator.BackupPathAt(2),
                    CloudSaveCoordinator.BackupPathAt(3),
                    CloudSaveRecovery.PreRecoverBackupPath
                })
                {
                    StringAssert.StartsWith(Path.GetFullPath(sandbox.Root), Path.GetFullPath(path),
                        "격리 중인데 샌드박스 밖을 가리킨다: " + path);
                    Assert.IsFalse(Path.GetFullPath(path).StartsWith(Path.GetFullPath(realRoot)),
                        "격리 중인데 실사용 폴더를 가리킨다: " + path);
                }

                Assert.IsTrue(SaveSystem.IsRootOverridden);
            }

            // 격리가 풀리면 정확히 실사용 경로로 돌아온다
            Assert.IsFalse(SaveSystem.IsRootOverridden);
            StringAssert.StartsWith(Path.GetFullPath(realRoot), Path.GetFullPath(SaveSystem.Path));
            StringAssert.StartsWith(Path.GetFullPath(realRoot), Path.GetFullPath(CloudSaveSidecar.Path));
        }

        /**
         * ★ 격리 안에서 저장·불러오기·삭제·백업 순환을 전부 돌려도 실사용
         * 파일은 바이트 그대로다. Dispose가 그것을 검사한다 - 이 테스트가
         * 통과했다는 것 자체가 "실사용 파일 무접촉"의 증명이다.
         */
        [Test]
        public void EverySaveOperationInsideTheSandboxLeavesTheRealFilesAlone()
        {
            using (new SaveSandbox())
            {
                var data = SaveData.NewGame();
                data.stage = 61;
                data.maxStageReached = 61;

                SaveSystem.Save(data);
                Assert.IsTrue(SaveSystem.Exists, "샌드박스 안에는 저장돼야 한다");

                var loaded = SaveSystem.Load();
                Assert.AreEqual(61, loaded.maxStageReached);

                var sidecar = CloudSaveLocalState.NewFor("isolation-uid", CloudSaveIds.New());
                Assert.IsTrue(CloudSaveSidecar.Save(sidecar));
                Assert.IsNotNull(CloudSaveSidecar.Load());

                CloudSaveCoordinator.KeepPreCloudBackup();
                Assert.IsTrue(File.Exists(CloudSaveCoordinator.PreCloudBackupPath));

                SaveSystem.Delete();
                CloudSaveSidecar.Delete();
            }
            // Dispose가 실사용 파일 지문을 대조했다 - 달라졌으면 여기 오기 전에 던졌다
        }

        /** 격리 회귀의 마지막 그물: sandbox 밖에서도 만들고 버리는 짝이 맞는다 */
        [Test]
        public void DisposingTwiceIsHarmless()
        {
            var sandbox = new SaveSandbox();
            sandbox.Dispose();
            sandbox.Dispose();

            Assert.IsFalse(SaveSystem.IsRootOverridden);
        }
    }
}
