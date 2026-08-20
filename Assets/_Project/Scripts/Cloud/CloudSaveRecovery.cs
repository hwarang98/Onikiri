using System;
using System.IO;
using System.Threading.Tasks;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 계정 복구(uid 교체)에서 세이브를 잇는 오케스트레이터 (61단계 S5-2).
     *
     * 55단계 `AccountLink.Recover`는 **랭킹 문서만** 복구했다 - 재설치한 사람은
     * 순위는 돌아오는데 진행은 안 돌아왔다. 이 클래스가 그 구멍을 막는다.
     * 순서는 설계 §8.2에 못박혀 있다:
     *
     *     ① uid 교체 **전** - 현재 로컬을 복구 후보로 보존 (메모리 + 백업 파일)
     *     ② 복구 로그인 성공 뒤 - 기존 uid의 playerSaves를 서버에서 읽음
     *     ③ 기존 세이브 없음      -> 현재 로컬이 새 계정의 revision 1
     *     ④ 있고 지문이 같음      -> 조용히 서버 head 채택
     *     ⑤ 다름                  -> 60단계 충돌 화면 (자동 병합 없음)
     *     ⑥ 버려진 익명 문서는 클라이언트가 지우지 않음 (규칙 4-B 계승)
     *
     * ## 판정은 여기서 새로 짜지 않는다
     *
     * `CloudSaveRecoveryPolicy`가 갈래를 정하고, 여기는 그 갈래의 파일·상태
     * 효과만 실행한다 - CloudSaveCoordinator가 CloudSavePolicy를 부르기만
     * 하는 것과 같은 구도다.
     *
     * ## 실패해도 익명 uid를 잃지 않는 이유가 이 클래스 **밖**에 있다
     *
     * 여기는 복구 로그인이 **이미 성공한 뒤**에만 불린다(AccountLink). 취소·
     * 네트워크·AlreadyInUse가 아닌 오류는 전부 그 전에 걸러져 uid가 그대로다.
     * 그리고 이 클래스는 로컬 세이브 파일을 **읽고 복사만** 한다 - 어느 갈래도
     * 세이브를 덮지 않으므로, 복구 도중 앱이 죽어도 로컬 진행은 그대로다
     * (⑤에서 덮는 것은 사람이 고른 뒤의 CloudConflictPanel이고, 그 직전에
     * 3벌 순환 백업이 남는다).
     */
    public static class CloudSaveRecovery
    {
        private const string Tag = "[CloudSave]";

        /** ①의 백업 파일. 복구가 어그러졌을 때 이 한 벌이 증거이자 되돌릴 길이다 */
        public static string PreRecoverBackupPath
        {
            get { return SaveSystem.Path + ".prerecover"; }
        }

        /** 마지막 복구가 밟은 갈래. 테스트 패널·보고가 읽는다 */
        public static CloudRecoveryPlan LastPlan { get; private set; }
            = CloudRecoveryPlan.WaitForServer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static Func<string, Task<CloudSaveFetchResult>> fetchOverride;

        /** 서버 읽기를 가짜로 바꾼다. §8.2의 여섯 순서를 Firebase 없이 지나게 한다 */
        public static void UseFetchForTests(Func<string, Task<CloudSaveFetchResult>> fetch)
        {
            fetchOverride = fetch;
        }

        public static void ResetForTests()
        {
            fetchOverride = null;
            LastPlan = CloudRecoveryPlan.WaitForServer;
        }
#endif

        /**
         * @brief ① uid 교체 **전에** 부른다. 현재 로컬을 복구 후보로 보존한다.
         *
         * 살아 있는 GameSession이 있으면 먼저 저장을 한 번 돌린다 - 디스크의
         * 세이브는 마지막 자동 저장(30초) 이전일 수 있고, 복구 후보가 30초
         * 낡아 있으면 ⑤의 충돌 화면이 방금 딴 것을 안 보여준다.
         *
         * 백업 복사는 최선의 노력이다(SaveSystem의 손상 백업과 같은 규칙) -
         * 실패해도 복구를 멈추지 않는다. 메모리의 한 벌은 이미 손에 있다.
         *
         * @return 복구 후보. 로컬이 없으면 새 게임 한 벌 (null이 아니다)
         */
        public static SaveData PrepareCandidate()
        {
            var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (session != null && session.IsLoaded) session.Save();

            try
            {
                if (File.Exists(SaveSystem.Path))
                    File.Copy(SaveSystem.Path, PreRecoverBackupPath, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Tag + " 복구 전 백업 실패(계속 진행): " + exception.Message);
            }

            return SaveSystem.Load();
        }

        /**
         * @brief ②~⑤. 복구 로그인이 **성공한 뒤** 새 uid로 부른다.
         *
         * 어느 갈래든 옛(익명) uid의 문서에는 손대지 않는다 - 읽지도 쓰지도
         * 지우지도 않는다(⑥). 이 함수가 만지는 서버 문서는 새 uid의 것뿐이다.
         *
         * @return 밟은 갈래. AskTheHuman이면 충돌 화면의 재료까지 채워져 있다
         */
        public static async Task<CloudRecoveryPlan> RunAsync(string recoveredUid, SaveData candidate)
        {
            if (string.IsNullOrEmpty(recoveredUid))
            {
                LastPlan = CloudRecoveryPlan.WaitForServer;
                return LastPlan;
            }

            if (candidate == null) candidate = SaveData.NewGame();

            CloudSaveFetchResult fetch = await Fetch(recoveredUid);

            string localSha = CloudSaveFingerprint.StateHashOf(candidate);
            string cloudSha = fetch.envelope != null ? fetch.envelope.stateSha256 : string.Empty;

            CloudRecoveryPlan plan = CloudSaveRecoveryPolicy.PlanFor(fetch.status, localSha, cloudSha);
            LastPlan = plan;

            Debug.Log(Tag + " ★ 복구 세이브 판정 = " + plan + " (서버 " + fetch.status
                      + " / 로컬 " + Short(localSha) + " / 서버지문 " + Short(cloudSha) + ")");

            switch (plan)
            {
                case CloudRecoveryPlan.KeepLocalAsFirstRevision:
                    KeepLocal(recoveredUid, candidate);
                    break;

                case CloudRecoveryPlan.AdoptServerQuietly:
                    AdoptQuietly(recoveredUid, fetch.envelope);
                    break;

                case CloudRecoveryPlan.AskTheHuman:
                    // 자동으로 어느 쪽도 쓰지 않는다. 재료만 챙기면 60단계의
                    // 길이 그대로 이어진다 - 설정 줄 "기록 선택 필요", 충돌
                    // 화면, 선택 뒤 씬 재로드(ApplyBoot 1회 + 방치 보상 1회)
                    CloudSaveSync.NoteConflict(fetch.envelope);
                    CloudSaveCoordinator.NoteSyncResult(CloudSaveState.Conflict);
                    break;

                case CloudRecoveryPlan.Blocked:
                    CloudSaveCoordinator.NoteSyncResult(CloudSaveState.Blocked);
                    break;

                default:
                    // 서버를 못 봤다. 아무것도 정하지 않는다 - 옛 uid의 sidecar는
                    // 이미 없는 것으로 읽히므로(SidecarAppliesTo), 다음 부팅·다음
                    // 커밋이 새 uid로 서버를 다시 보고 그때 정한다
                    CloudSaveCoordinator.NoteSyncResult(CloudSaveState.LocalOnly);
                    break;
            }

            return plan;
        }

        // ---------------------------------------------------------------- 갈래

        /** ③ 새 계정의 첫 정본이 될 준비. 실제 revision 1은 다음 커밋이 만든다 */
        private static void KeepLocal(string uid, SaveData candidate)
        {
            var sidecar = CloudSaveLocalState.NewFor(uid, CloudSaveSidecar.NewDeviceId());
            if (sidecar == null || !CloudSaveSidecar.Save(sidecar))
            {
                // sidecar가 디스크에 못 남으면 서버 쓰기도 없다(57단계 계약).
                // 로컬 dirty로 남고 다음 커밋 준비에서 다시 만든다
                Debug.LogWarning(Tag + " 복구: 새 sidecar를 남기지 못했습니다. 다음 동기화가 다시 시도합니다.");
            }

            CloudSaveCoordinator.NoteSyncResult(CloudSaveState.Dirty);

            // 잃으면 계정 교체가 사라지는 순간이다. 120초를 기다리지 않는다
            CloudSaveSync.NoteSaved(candidate);
            CloudSaveSync.RequestUrgent("계정 복구 - 새 계정의 첫 업로드");
        }

        /** ④ 같은 기록. 서버 revision만 채택한다 - 쓰기도 물음도 없다 */
        private static void AdoptQuietly(string uid, CloudSaveEnvelope envelope)
        {
            var sidecar = CloudSaveLocalState.NewFor(uid, CloudSaveSidecar.NewDeviceId());

            if (sidecar != null
                && sidecar.MarkSynced(envelope.revision, envelope.payloadSha256,
                                      envelope.stateSha256, envelope.updatedAtUtcTicks)
                && CloudSaveSidecar.Save(sidecar))
            {
                CloudSaveCoordinator.NoteSyncResult(CloudSaveState.InSync);

                // 같은 기록이 dirty로 남아 있으면 다음 커밋이 내용 그대로인
                // revision 하나를 더 올린다 - 올릴 것이 없다고 여기서 정리한다
                CloudSaveSync.DropPending("복구 - 서버와 같은 기록");
                return;
            }

            // sidecar를 못 남겼다. "서버를 모르는 기기"로 남는다 - 다음 부팅이
            // 같은 지문을 다시 보고 InSync로 들어온다 (판정표의 정상 경로)
            Debug.LogWarning(Tag + " 복구: sidecar 채택 실패. 다음 부팅이 다시 판정합니다.");
            CloudSaveCoordinator.NoteSyncResult(CloudSaveState.LocalOnly);
        }

        // ---------------------------------------------------------------- 내부

        private static Task<CloudSaveFetchResult> Fetch(string uid)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fetchOverride != null) return fetchOverride(uid);
#endif
#if UNITY_EDITOR
            // 에디터의 Firebase는 운영 프로젝트에 붙는다. 60단계 행 사고의
            // 방어선(EditorServerCheckAllowed)을 복구 읽기도 그대로 지난다
            if (!CloudSaveCoordinator.EditorServerCheckAllowed)
            {
                Debug.Log(Tag + " 복구: 에디터 실서버 게이트가 꺼져 있어 서버를 보지 않습니다.");
                return Task.FromResult(new CloudSaveFetchResult
                {
                    status = CloudSaveStoreStatus.Offline
                });
            }
#endif
            return CloudSaveStore.FetchAsync(uid);
        }

        private static string Short(string sha)
        {
            if (string.IsNullOrEmpty(sha)) return "-";
            return sha.Length <= 8 ? sha : sha.Substring(0, 8);
        }
    }
}
