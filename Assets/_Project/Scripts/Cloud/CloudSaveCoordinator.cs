using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 크로스 저장의 상태. 설정 화면이 읽을 네 줄이다 (설계 §9).
     *
     * `Uploading`과 충돌 **해결**은 4단계다. 이 스텝에서 충돌이 나면 상태만
     * `Conflict`로 남기고 로컬로 들어간다 - 자동으로 어느 쪽도 쓰지 않는다.
     */
    public enum CloudSaveState
    {
        /** 아직 고르는 중 */
        Bootstrapping = 0,

        /** 서버를 못 봤다. 로컬로 논다 - **정상 경로다** */
        LocalOnly,

        /** 서버와 같은 기록이다 */
        InSync,

        /** 로컬이 앞서 있다. 올릴 것이 있다 (업로드는 4단계) */
        Dirty,

        /** 두 곳이 갈라졌다. **사람이 고른다** (선택 UI는 4단계) */
        Conflict,

        /** 어느 쪽도 건드리면 안 된다 */
        Blocked
    }

    /** 부팅에서 고른 한 벌과 그 이유 */
    public sealed class CloudSaveBootChoice
    {
        /** Apply할 세이브. **절대 null이 아니다** - 최소한 로컬이다 */
        public SaveData data;

        public CloudSaveDecision decision;
        public CloudSaveBlock block;
        public CloudSaveState state;

        /** 클라우드에서 받은 것을 적용하는가 */
        public bool fromCloud;

        /** logcat 한 줄. 실기에서 어느 갈래를 밟았는지가 이 문자열로 읽힌다 */
        public string reason = string.Empty;
    }

    /**
     * @brief 부팅에서 **로컬과 클라우드 중 하나를 고른다.** 그리고 그것으로 끝이다.
     *
     * ## 왜 고르는 일이 따로 있어야 하는가 - 방치 보상 때문이다
     *
     * 세이브를 적용하면 그 순간 방치 보상이 지급된다(GameSession). 클라우드를
     * **적용한 뒤에** 덮어씌우면 보상이 두 번 나간다 - 로컬 기준으로 한 번,
     * 클라우드 기준으로 또 한 번. 그래서 순서가 뒤집혀 있어야 한다:
     *
     *     읽기 -> 고르기 -> **고른 한 벌만** Apply 1회 -> 보상 1회 -> 즉시 저장
     *
     * 이 스텝의 존재 이유가 그 한 줄이다.
     *
     * ## 판정은 여기서 새로 짜지 않는다
     *
     * 57단계 `CloudSavePolicy`와 58단계 `CloudSaveStore`를 **부르기만** 한다.
     * 판정표(설계 §6.1)와 코드가 어긋나면 그것은 이 파일이 아니라 정책의 버그다.
     *
     * ## Firebase는 게이트가 아니다
     *
     * 서버 확인이 제한 시간 안에 안 끝나면 로컬로 들어간다. 오프라인·미로그인·
     * 게스트는 전부 **정상 경로**이고, 그 상태에서 게임은 한 프레임도 안 멈춘다
     * (56단계 인트로 계약: 부팅 게이트는 세이브 준비 완료이지 Firebase가 아니다).
     */
    public static class CloudSaveCoordinator
    {
        private const string Tag = "[CloudSave]";

        /**
         * @brief 부팅에서 서버를 실제로 확인할 것인가. **기본 켜짐이다** (60단계).
         *
         * 59단계까지는 꺼져 있었다 - 규칙이 운영에 배포되기 전이라, 켜면 부팅마다
         * 거부되는 왕복이 하나씩 나가고 그 로그가 진짜 실패를 덮었다. 규칙이
         * 배포됐으므로(2026-08-18, 사용자 승인) 이제 이 왕복은 실제 응답을 받는다.
         *
         * 켜져 있어도 **uid가 없으면 안 나간다**(ShouldCheckServer) - 에디터와
         * 로그인 전 첫 실행은 여전히 0프레임 동기 부팅이다. 그리고 코루틴이
         * 도중에 죽어도 세이브는 유실되지 않는다(GameSession.Save의 loaded 게이트 +
         * PlayMode `DestroyingTheSessionMidBootLosesNothing`이 그것을 잰다).
         */
        public static bool ServerCheckEnabled { get; set; } = true;

        public static CloudSaveState State { get; private set; }

        public static CloudSaveBootChoice LastChoice { get; private set; }

        /** 마지막 서버 확인 결과. 설정 화면·테스트 패널이 읽는다 */
        public static CloudSaveStoreStatus LastServerStatus { get; private set; }
            = CloudSaveStoreStatus.Missing;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ---- 테스트 seam. **릴리스 빌드에는 존재하지 않는다** (60단계 S4-0).
        //
        // 어느 세이브가 정본인지 바꿔치기할 수 있는 표면이라, 남겨 두면 개조
        // 클라가 가장 먼저 찾는 문이 된다. 56단계의 판단 그대로다 - "플래그는
        // 뒤집히지만 컴파일에서 지운 코드는 뒤집히지 않는다."
        private static Func<string, Task<CloudSaveFetchResult>> fetchOverride;
        private static string uidOverride;

        /** 서버 확인을 가짜로 바꾼다. 판정표의 모든 줄을 Firebase 없이 지나게 한다 */
        public static void UseFetchForTests(Func<string, Task<CloudSaveFetchResult>> fetch)
        {
            fetchOverride = fetch;
        }

        /** 부팅이 쓸 uid를 바꾼다. 에디터에는 로그인이 없어 uid가 비기 때문이다 */
        public static void UseIdentityForTests(string uid)
        {
            uidOverride = uid;
        }

        public static void ResetForTests()
        {
            fetchOverride = null;
            uidOverride = null;
            ServerCheckEnabled = true;
            State = CloudSaveState.Bootstrapping;
            LastChoice = null;
            LastServerStatus = CloudSaveStoreStatus.Missing;
        }
#endif

#if UNITY_EDITOR
        /**
         * @brief 에디터에서 부팅 실서버 확인을 허용할 것인가. **기본 꺼짐이다.**
         *
         * 에디터의 Firebase는 운영 프로젝트(onikiri-9cc18)에 붙는다. 에디터
         * 플레이·PlayMode 테스트가 운영 정본을 읽고 쓰는 것은 기능이 아니라
         * 사고다 - 60단계에서 이 왕복이 도메인 리로드와 교착해 에디터가 **두 번**
         * 행이 걸렸다(인트로 워밍이 실제 로그인을 만들고, uid가 생기는 순간
         * fetchOverride 없는 테스트의 부팅이 실서버로 나갔다).
         *
         * 에디터에서 실서버 경로를 보고 싶으면 테스트 패널의 스위치가 이것을 켠다.
         * 실기(개발/릴리스 빌드)에는 이 게이트가 없다 - ServerCheckEnabled뿐이다.
         */
        public static bool EditorServerCheckAllowed;
#endif

        /** 지금 부팅이 쓸 uid. 릴리스에서는 언제나 CloudScores의 것이다 */
        private static string BootUid
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (uidOverride != null) return uidOverride;
#endif
                return CloudScores.Uid;
            }
        }

        /**
         * @brief 부팅 선택. **여기서 아무것도 Apply하지 않는다** - 고르기만 한다.
         *
         * 서버 확인은 제한 시간 안에서만 기다린다. 넘으면 로컬로 들어가고,
         * 그 뒤에 서버가 돌아와도 전투 중에 핫스왑하지 않는다(4단계가 충돌
         * 화면으로 처리한다).
         */
        public static IEnumerator ChooseBootSave(SaveData local, Action<CloudSaveBootChoice> onDone)
        {
            State = CloudSaveState.Bootstrapping;

            CloudSaveLocalState sidecar = OwnSidecar();
            string uid = BootUid;

            CloudSaveFetchResult fetch = default(CloudSaveFetchResult);
            bool serverChecked = false;

            if (ShouldCheckServer(uid))
            {
                Task<CloudSaveFetchResult> task = Fetch(uid);
                float deadline = Time.realtimeSinceStartup + CloudSavePolicy.BootServerCheckSeconds;

                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;

                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    fetch = task.Result;
                    LastServerStatus = fetch.status;

                    // 오프라인·실패는 "서버를 못 봤다"이지 "서버에 없다"가 아니다.
                    // 그 둘을 섞으면 오프라인 부팅이 신규 업로드로 읽힌다
                    serverChecked = fetch.status != CloudSaveStoreStatus.Offline
                                    && fetch.status != CloudSaveStoreStatus.Failed;
                }
                else
                {
                    if (!task.IsCompleted) FirebaseRuntime.Observe(task);
                    LastServerStatus = CloudSaveStoreStatus.Offline;
                    Debug.Log(Tag + " 서버 확인이 제한 시간("
                              + CloudSavePolicy.BootServerCheckSeconds + "초)을 넘겼습니다. 로컬로 진입합니다.");
                }
            }

            CloudSaveBootChoice choice = Finish(ChooseFrom(local, sidecar, fetch, serverChecked),
                                                sidecar, fetch);

            if (onDone != null) onDone(choice);
        }

        /**
         * @brief 서버를 기다리지 않는 부팅. **같은 프레임에 끝난다.**
         *
         * 서버를 볼 이유가 없으면(꺼져 있거나 로그인 전) 한 프레임도 쓰지
         * 않는다. 56단계 인트로가 "세이브 로드는 같은 프레임에 돈다"를 가정하고
         * 있고, PlayMode 검사 몇은 첫 프레임에 `GameSession`을 파괴한다 -
         * 그 세계에서 코루틴으로 미루면 세이브가 통째로 안 얹힌다(실측으로 물렸다).
         */
        public static CloudSaveBootChoice ChooseLocally(SaveData local)
        {
            State = CloudSaveState.Bootstrapping;

            CloudSaveLocalState sidecar = OwnSidecar();

            return Finish(ChooseFrom(local, sidecar, default(CloudSaveFetchResult), false),
                          sidecar, default(CloudSaveFetchResult));
        }

        /**
         * @brief 동기화(CloudSaveSync)가 왕복 결과로 상태를 옮긴다 (60단계).
         *
         * 부팅 밖에서 상태가 움직이는 유일한 문이다. 전이 자체는
         * `CloudSaveSyncPolicy.StateAfterCommit`이 정하고, 여기는 받아 적는다 -
         * 그래야 상태의 출처가 언제나 정책 하나다.
         */
        public static void NoteSyncResult(CloudSaveState next)
        {
            State = next;
        }

        /** 부팅이 서버를 기다릴 것인가. 아니면 동기 경로로 끝난다 */
        public static bool WillCheckServer
        {
            get { return ShouldCheckServer(BootUid); }
        }

        /** 이 uid의 것이 아닌 sidecar는 **없는 것으로 친다** (5단계 Recover) */
        private static CloudSaveLocalState OwnSidecar()
        {
            CloudSaveLocalState sidecar = CloudSaveSidecar.Load();
            string uid = BootUid;

            return CloudSavePolicy.SidecarAppliesTo(sidecar, uid) ? sidecar : null;
        }

        /** 고른 뒤의 뒷정리. 두 경로(동기·비동기)가 **같은 것을 지나야** 한다 */
        private static CloudSaveBootChoice Finish(CloudSaveBootChoice choice,
                                                  CloudSaveLocalState sidecar,
                                                  CloudSaveFetchResult fetch)
        {
            // 클라우드를 채택할 때만 로컬 원본을 옆에 남긴다. 순환 보관(3벌)은
            // 4단계이고, 여기서는 **덮이기 직전의 한 벌**을 지키는 것이 목적이다
            if (choice.fromCloud) KeepPreCloudBackup();

            // 부팅에서 갈라짐을 봤다면 충돌 화면의 재료도 여기서 챙긴다(61단계).
            // 안 챙기면 설정의 "기록 선택" 버튼이 봉투 없음으로 영영 안 산다 -
            // 복구를 타이틀에서 밟은 사람이 정확히 이 부팅으로 들어온다
            if (choice.decision == CloudSaveDecision.Conflict && fetch.envelope != null)
                CloudSaveSync.NoteConflict(fetch.envelope);

            if (choice.decision == CloudSaveDecision.InSync && sidecar != null
                && fetch.envelope != null)
            {
                // 같은 기록이다. 서버 revision만 채택한다 - 쓰기는 없다
                if (sidecar.MarkSynced(fetch.envelope.revision, fetch.envelope.payloadSha256,
                                       fetch.envelope.stateSha256, fetch.envelope.updatedAtUtcTicks))
                    CloudSaveSidecar.Save(sidecar);
            }

            State = choice.state;
            LastChoice = choice;

            Debug.Log(Tag + " 부팅 선택: " + choice.reason);
            return choice;
        }

        /**
         * @brief 판정 -> 고른 한 벌. **순수 함수다** (파일도 네트워크도 없다).
         *
         * EditMode가 설계 §6.1의 각 줄을 이 함수로 지난다.
         */
        public static CloudSaveBootChoice ChooseFrom(SaveData local, CloudSaveLocalState sidecar,
                                                     CloudSaveFetchResult fetch, bool serverChecked)
        {
            if (local == null) local = SaveData.NewGame();

            // 서버 문서가 손상됐다. 봉투가 없으므로 판정표에 넣을 사실이 없다 -
            // 정책의 fault 사상을 그대로 쓴다(적용도 업로드도 금지)
            if (serverChecked && fetch.status == CloudSaveStoreStatus.Invalid)
                return Blocked(local, CloudSavePolicy.BlockFor(fetch.fault),
                               "서버 문서를 쓸 수 없음 (" + fetch.fault + ")");

            var facts = new CloudSaveFacts
            {
                serverChecked = serverChecked,
                hasLocal = true,
                localStateSha = CloudSaveFingerprint.StateHashOf(local),

                hasCloud = fetch.envelope != null,
                hasSidecar = sidecar != null,
                baseRevision = sidecar != null ? sidecar.baseRevision : 0L,
                lastSyncedStateSha = sidecar != null ? sidecar.lastSyncedStateSha256 : string.Empty
            };

            if (fetch.envelope != null)
            {
                facts.cloudFormatVersion = fetch.envelope.formatVersion;
                facts.cloudSaveVersion = fetch.envelope.saveVersion;
                facts.cloudRevision = fetch.envelope.revision;
                facts.cloudBaseRevision = fetch.envelope.baseRevision;
                facts.cloudStateSha = fetch.envelope.stateSha256;
                facts.cloudEnvelopeFault = CloudSaveEnvelopeFault.None;
            }

            CloudSaveVerdict verdict = CloudSavePolicy.Decide(facts);

            switch (verdict.decision)
            {
                case CloudSaveDecision.DownloadCloud:
                    return Download(local, fetch.envelope);

                case CloudSaveDecision.InSync:
                    return new CloudSaveBootChoice
                    {
                        data = local,
                        decision = verdict.decision,
                        state = CloudSaveState.InSync,
                        reason = "같은 기록 - 로컬 사용 (서버 rev "
                                 + (fetch.envelope != null ? fetch.envelope.revision : 0L) + ")"
                    };

                case CloudSaveDecision.UploadLocal:
                    return new CloudSaveBootChoice
                    {
                        data = local,
                        decision = verdict.decision,
                        state = CloudSaveState.Dirty,
                        reason = "로컬이 정본 - 업로드 대기 (업로드는 4단계)"
                    };

                case CloudSaveDecision.Conflict:
                    // **자동으로 어느 쪽도 쓰지 않는다.** 로컬로 들어가고 상태만 남긴다
                    return new CloudSaveBootChoice
                    {
                        data = local,
                        decision = verdict.decision,
                        state = CloudSaveState.Conflict,
                        reason = "두 곳이 갈라짐 - 로컬로 진입, 자동 쓰기 없음 (선택 UI는 4단계)"
                    };

                case CloudSaveDecision.Blocked:
                    return Blocked(local, verdict.block, "차단 (" + verdict.block + ")");

                default:
                    return new CloudSaveBootChoice
                    {
                        data = local,
                        decision = CloudSaveDecision.LocalOnly,
                        state = CloudSaveState.LocalOnly,
                        reason = serverChecked ? "서버에 기록 없음 - 로컬 사용" : "서버 확인 없음 - 로컬 사용"
                    };
            }
        }

        /** 서버 확인 없이 로컬로 들어간다. 부팅이 어떤 이유로든 어긋났을 때의 바닥 */
        public static CloudSaveBootChoice LocalFallback(SaveData local)
        {
            return new CloudSaveBootChoice
            {
                data = local ?? SaveData.NewGame(),
                decision = CloudSaveDecision.LocalOnly,
                state = CloudSaveState.LocalOnly,
                reason = "로컬 진입 (서버 확인 없음)"
            };
        }

        // ---------------------------------------------------------------- 내부

        private static CloudSaveBootChoice Download(SaveData local, CloudSaveEnvelope envelope)
        {
            SaveData cloud = CloudSaveFingerprint.Deserialize(envelope.payload);

            // 옛 버전의 클라우드 세이브는 **여기서 올린다.** 로컬 파일이 지나는
            // 것과 같은 사슬이라(SaveData.Migrate) 두 경로가 다른 답을 내지 않는다
            if (cloud == null || !SaveData.Migrate(cloud))
                return Blocked(local, CloudSaveBlock.CorruptPayload, "클라우드 payload를 적용할 수 없음");

            return new CloudSaveBootChoice
            {
                data = cloud,
                decision = CloudSaveDecision.DownloadCloud,
                state = CloudSaveState.InSync,
                fromCloud = true,
                reason = "클라우드 채택 (rev " + envelope.revision + ", "
                         + envelope.summary.maxStageReached + "층 · Lv."
                         + envelope.summary.characterLevel + ")"
            };
        }

        private static CloudSaveBootChoice Blocked(SaveData local, CloudSaveBlock block, string reason)
        {
            return new CloudSaveBootChoice
            {
                data = local,
                decision = CloudSaveDecision.Blocked,
                block = block,
                state = CloudSaveState.Blocked,
                reason = reason
            };
        }

        private static bool ShouldCheckServer(string uid)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fetchOverride != null) return true;
#endif
#if UNITY_EDITOR
            // 가짜 서버(fetchOverride)가 없으면 에디터는 실서버로 나가지 않는다
            if (!EditorServerCheckAllowed) return false;
#endif
            if (!ServerCheckEnabled) return false;
            return !string.IsNullOrEmpty(uid);
        }

        private static Task<CloudSaveFetchResult> Fetch(string uid)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fetchOverride != null) return fetchOverride(uid);
#endif
            return CloudSaveStore.FetchAsync(uid);
        }

        /**
         * @brief 클라우드를 적용하기 **전에** 로컬 원본을 옆에 둔다. **최근 3벌 순환.**
         *
         * 59단계는 한 벌만 남겼고, 그 한 벌은 두 번째 채택에서 덮였다 - 충돌을
         * 두 번 겪은 사람이 첫 번째 이전의 기록으로 돌아갈 길이 없었다는 뜻이다.
         * 설계 §7이 정한 대로 셋을 돌린다:
         *
         *     precloud.1  가장 최근에 덮인 것
         *     precloud.2  그 전
         *     precloud.3  그 전전 (다음 채택에서 버려진다)
         *
         * 실패는 삼킨다. 백업은 최선의 노력이지 게이트가 아니다(SaveSystem의
         * 손상 백업과 같은 규칙).
         */
        public static void KeepPreCloudBackup()
        {
            try
            {
                if (!File.Exists(SaveSystem.Path)) return;

                // 뒤에서부터 민다. 3은 버려지고 2 -> 3, 1 -> 2, 원본 -> 1
                if (File.Exists(BackupPathAt(2))) File.Copy(BackupPathAt(2), BackupPathAt(3), true);
                if (File.Exists(BackupPathAt(1))) File.Copy(BackupPathAt(1), BackupPathAt(2), true);
                File.Copy(SaveSystem.Path, BackupPathAt(1), true);

                Debug.Log(Tag + " 클라우드 적용 전 로컬 백업 (3벌 순환): " + BackupPathAt(1));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Tag + " 로컬 백업 실패: " + exception.Message);
            }
        }

        public static string BackupPathAt(int slot)
        {
            return SaveSystem.Path + ".precloud." + slot;
        }

        public static string PreCloudBackupPath
        {
            get { return SaveSystem.Path + ".precloud.1"; }
        }
    }
}
