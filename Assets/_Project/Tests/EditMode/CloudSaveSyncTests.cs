using System;
using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 자동 동기화(60단계)의 **규칙** - 시계를 손에 쥐고 잰다.
     *
     * `CloudSaveSyncPolicy`는 Firebase도 유니티 시계도 없는 순수 함수라,
     * "120초 debounce"와 "urgent 2초"를 실제로 기다리지 않고 검사한다.
     * 실제 왕복(커밋·세션)은 Emulator와 PlayMode의 몫이다.
     */
    public class CloudSaveSyncTests
    {
        // ---------------------------------------------------------------- debounce

        /** 로컬 저장 30초 != 클라우드 쓰기. dirty가 돼도 120초를 묶는다 */
        [Test]
        public void ADirtySaveWaitsForTheDebounce()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(0f, -1f, 999f));
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(
                CloudSavePolicy.DebounceSeconds - 1f, -1f, 999f));
            Assert.IsTrue(CloudSaveSyncPolicy.ShouldCommit(
                CloudSavePolicy.DebounceSeconds, -1f, 999f));
        }

        [Test]
        public void NothingDirtyMeansNothingToCommit()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(-1f, -1f, 999f));
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(-1f, 999f, 999f),
                "urgent 표시가 있어도 올릴 것이 없으면 안 나간다");
        }

        /**
         * ★ urgent는 debounce를 기다리지 않는다.
         *
         * 귀문 승리·보석을 쓴 뽑기 직후의 120초는 **지불이 서버에 없는 창**이다.
         * 2초 유예만 두는 이유는 10연을 연달아 돌리는 손을 한 번에 묶기 위해서다.
         */
        [Test]
        public void AnUrgentMarkSkipsTheDebounce()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(5f, 0f, 999f),
                "유예(2초) 안에는 아직이다");
            Assert.IsTrue(CloudSaveSyncPolicy.ShouldCommit(
                5f, CloudSaveSyncPolicy.UrgentDelaySeconds, 999f));
        }

        /** 실패 직후의 즉시 재시도는 같은 실패를 반복한다 */
        [Test]
        public void AFreshFailureHoldsTheRetry()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(999f, 999f, 0f));
            Assert.IsFalse(CloudSaveSyncPolicy.ShouldCommit(
                999f, 999f, CloudSaveSyncPolicy.RetryDelaySeconds - 1f));
            Assert.IsTrue(CloudSaveSyncPolicy.ShouldCommit(
                999f, 999f, CloudSaveSyncPolicy.RetryDelaySeconds));
        }

        // ---------------------------------------------------------------- 쓰기 허용

        /**
         * ★★ **갈라진 상태에서 자동 쓰기는 없다.**
         *
         * Conflict에서 커밋이 나가면 그것이 곧 한쪽 브랜치의 자동 선택이다.
         * "나중에 결정"이 이 상태를 유지하고, 그동안 클라우드 쓰기가 멈춰
         * 있다는 것이 설정 줄("기록 선택 필요")에 그대로 읽힌다.
         */
        [Test]
        public void AConflictStopsAllCloudWrites()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Conflict));
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Blocked));
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Bootstrapping));

            Assert.IsTrue(CloudSaveSyncPolicy.MayWrite(CloudSaveState.LocalOnly));
            Assert.IsTrue(CloudSaveSyncPolicy.MayWrite(CloudSaveState.InSync));
            Assert.IsTrue(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Dirty));
        }

        // ---------------------------------------------------------------- 상태 전이

        /** 성공 둘만 InSync. 나머지는 로컬 dirty가 유지된다(57단계 pending 계약) */
        [Test]
        public void OnlyASyncedCommitReachesInSync()
        {
            foreach (CloudSaveStoreStatus status in Enum.GetValues(typeof(CloudSaveStoreStatus)))
            {
                CloudSaveState next = CloudSaveSyncPolicy.StateAfterCommit(status);

                switch (status)
                {
                    case CloudSaveStoreStatus.Committed:
                    case CloudSaveStoreStatus.AlreadyApplied:
                        Assert.AreEqual(CloudSaveState.InSync, next, status.ToString());
                        break;
                    case CloudSaveStoreStatus.Conflict:
                        Assert.AreEqual(CloudSaveState.Conflict, next);
                        break;
                    case CloudSaveStoreStatus.Invalid:
                        Assert.AreEqual(CloudSaveState.Blocked, next);
                        break;
                    default:
                        Assert.AreEqual(CloudSaveState.Dirty, next, status.ToString());
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- 네 문장

        /** 설계 §9: 설정 화면에는 정확히 네 문장만 있다. 다섯 번째는 없다 */
        [Test]
        public void TheSettingsLineSpeaksExactlyFourSentences()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (CloudSaveState state in Enum.GetValues(typeof(CloudSaveState)))
            {
                seen.Add(CloudSaveSyncPolicy.StatusLine(state, false));
                seen.Add(CloudSaveSyncPolicy.StatusLine(state, true));
            }

            Assert.AreEqual(4, seen.Count, string.Join(" / ", seen));

            Assert.AreEqual("클라우드 저장 완료",
                CloudSaveSyncPolicy.StatusLine(CloudSaveState.InSync, false));
            Assert.AreEqual("기기에 저장됨 · 연결되면 동기화",
                CloudSaveSyncPolicy.StatusLine(CloudSaveState.Dirty, false));
            Assert.AreEqual("기록 선택 필요",
                CloudSaveSyncPolicy.StatusLine(CloudSaveState.Conflict, false));
            Assert.AreEqual("다른 기기에서 플레이 중",
                CloudSaveSyncPolicy.StatusLine(CloudSaveState.InSync, true),
                "다른 기기가 살아 있으면 그것이 다른 무엇보다 먼저다");
        }

        // ---------------------------------------------------------------- 백업 순환

        /**
         * ★ S4-0-3: 한 벌이 아니라 **세 벌 순환**이다.
         *
         * 59단계의 한 벌은 두 번째 채택에서 덮였다 - 충돌을 두 번 겪은 사람이
         * 첫 번째 이전으로 돌아갈 길이 없었다.
         */
        [Test]
        public void PreCloudBackupsRotateThreeDeep()
        {
            // 백업/복원 대신 경로째 격리한다 (61단계 S5-0). Dispose가 실사용
            // 파일이 그대로인지까지 검사한다
            using (new SaveSandbox())
            {
                string save = SaveSystem.Path;

                // 세 번의 "클라우드 채택" - 각 시점의 원본이 한 칸씩 밀린다
                File.WriteAllText(save, "{\"version\":21,\"stage\":1}");
                CloudSaveCoordinator.KeepPreCloudBackup();

                File.WriteAllText(save, "{\"version\":21,\"stage\":2}");
                CloudSaveCoordinator.KeepPreCloudBackup();

                File.WriteAllText(save, "{\"version\":21,\"stage\":3}");
                CloudSaveCoordinator.KeepPreCloudBackup();

                StringAssert.Contains("\"stage\":3", File.ReadAllText(CloudSaveCoordinator.BackupPathAt(1)),
                    "1번 = 가장 최근에 덮인 것");
                StringAssert.Contains("\"stage\":2", File.ReadAllText(CloudSaveCoordinator.BackupPathAt(2)));
                StringAssert.Contains("\"stage\":1", File.ReadAllText(CloudSaveCoordinator.BackupPathAt(3)),
                    "3번 = 두 번 전의 원본이 살아 있다");

                // 네 번째 채택 - 가장 오래된 것만 버려진다
                File.WriteAllText(save, "{\"version\":21,\"stage\":4}");
                CloudSaveCoordinator.KeepPreCloudBackup();

                StringAssert.Contains("\"stage\":4", File.ReadAllText(CloudSaveCoordinator.BackupPathAt(1)));
                StringAssert.Contains("\"stage\":2", File.ReadAllText(CloudSaveCoordinator.BackupPathAt(3)));
            }
        }

        // ---------------------------------------------------------------- S4-0 빚

        /**
         * ★ S4-0-1: 테스트 seam이 **릴리스 빌드에 존재하지 않는다.**
         *
         * 에디터에서는 컴파일 변형을 만들 수 없으므로 소스를 직접 읽는다 -
         * seam 메서드들이 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에 있는지.
         * 56단계 판단 그대로다: 플래그는 뒤집히지만 컴파일에서 지운 코드는
         * 뒤집히지 않는다.
         */
        [Test]
        public void TheTestSeamsAreCompiledOutOfReleaseBuilds()
        {
            foreach (string file in new[]
            {
                "Assets/_Project/Scripts/Cloud/CloudSaveCoordinator.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSync.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveRecovery.cs",
                "Assets/_Project/Scripts/SaveGame/SaveSystem.cs",
                "Assets/_Project/Scripts/Widget/Popups/CloudConflictPanel.cs"
            })
            {
                string source = File.ReadAllText(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), file));

                foreach (string seam in new[] { "ForTests", "SuppressReload",
                                                 "EditorServerCheckAllowed", "EditorNetworkAllowed" })
                {
                    int index = source.IndexOf(seam, StringComparison.Ordinal);
                    while (index >= 0)
                    {
                        string before = source.Substring(0, index);
                        int guards = Count(before, "#if UNITY_EDITOR");
                        int ends = Count(before, "#endif");

                        Assert.Greater(guards, ends,
                            file + " 의 " + seam + " 이 컴파일 가드 밖에 있다 (offset " + index + ")");

                        index = source.IndexOf(seam, index + 1, StringComparison.Ordinal);
                    }
                }
            }
        }

        /** 정본을 단독 SetAsync로 쓰는 경로가 없다 - 트랜잭션(58단계)뿐이다 */
        [Test]
        public void TheSyncNeverWritesTheCanonicalOutsideTheTransaction()
        {
            string source = File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Scripts/Cloud/CloudSaveSync.cs"));

            // 주석은 그 단어를 말해도 된다 - 잡는 것은 **호출**이다
            StringAssert.DoesNotContain(".SetAsync(", source);
            StringAssert.DoesNotContain(".UpdateAsync(", source);
            StringAssert.Contains("CloudSaveStore.CommitAsync", source,
                "쓰기는 revision 트랜잭션 하나뿐이어야 한다");
        }

        private static int Count(string text, string token)
        {
            int count = 0, index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
            }
            return count;
        }
    }
}
