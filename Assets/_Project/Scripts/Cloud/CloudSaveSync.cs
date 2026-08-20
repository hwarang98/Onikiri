using System;
using System.Threading.Tasks;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 자동 동기화의 오케스트레이터 (60단계). **처음으로 서버에 쓴다.**
     *
     * MonoBehaviour가 아니다. 시계(`Tick`)와 수명 신호(pause/resume)는
     * `GameSession`이 이미 들고 있고 - 자동 저장 30초·Pause 저장이 그 증거다 -
     * 같은 신호를 받는 두 번째 컴포넌트를 세우면 순서가 계약이 없는 채로 갈린다.
     * 그래서 GameSession이 자기 훅에서 이쪽을 **부르기만** 한다.
     *
     * ## 쓰기는 언제나 58단계 트랜잭션이다
     *
     * `CloudSaveStore.CommitAsync` 하나뿐이다 - 세션 확인·AlreadyApplied·
     * revision 잠금·백업 복사가 전부 그 안에 있다. 단독 SetAsync나 Firestore
     * 오프라인 큐로 정본을 쓰는 경로는 이 파일에 없고, 있어서도 안 된다
     * (last-write-wins가 최신 진행을 조용히 덮는다).
     *
     * ## urgent 트리거는 이벤트 구독이다
     *
     * 귀문 승리(EvolutionSystem.Evolved) · 뽑기(GachaSystem/SkillGachaSystem.
     * Pulled - 유료·무료 10연 포함) · 보석 소비(GemWallet.GemsChanged 감소 =
     * 동료/장비 구매까지 전부 지나는 길목). **그 시스템들은 한 줄도 안 바뀐다** -
     * 이미 있는 이벤트를 듣기만 하므로 밸런스·귀문 무수정 계약이 지켜진다.
     */
    public static class CloudSaveSync
    {
        private const string Tag = "[CloudSave]";

        /** 마지막 로컬 저장의 스냅샷. 커밋은 이것을 올린다 - 디스크 재읽기가 없다 */
        private static SaveData pendingData;

        private static float dirtySince = -1f;
        private static float urgentSince = -1f;
        private static float lastFailureAt = float.NegativeInfinity;

        private static bool committing;
        private static long lastGems = -1L;

#if UNITY_EDITOR
        /**
         * @brief 에디터에서 sync 실서버 왕복을 허용할 것인가. **기본 꺼짐이다.**
         *
         * 60단계에서 실제로 물린 사고의 방벽이다: PlayMode 테스트가 Main 씬을
         * 열면 인트로가 실제 Firebase 로그인을 하고(56단계 워밍), uid가 생긴
         * 상태에서 시계가 차면 Tick이 **운영 서버로 진짜 커밋**을 발사했다 -
         * 그 왕복이 도메인 리로드와 교착해 에디터가 통째로 행이 걸렸다.
         *
         * 부팅 쪽 게이트(CloudSaveCoordinator.EditorServerCheckAllowed)와 짝이다.
         * 에디터에서 sync를 보고 싶으면 테스트 패널이 이것을 켠다. 실기에는
         * 이 게이트가 없다.
         */
        public static bool EditorNetworkAllowed;
#endif

        /** 다른 기기가 세션을 들고 있는 것을 마지막 왕복에서 봤는가 */
        public static bool OtherDeviceActive { get; private set; }

        /** 충돌을 발견했을 때 서버가 들고 있던 봉투. 충돌 화면이 그린다 */
        public static CloudSaveEnvelope ConflictServerEnvelope { get; private set; }

        public static string StatusLine
        {
            get { return CloudSaveSyncPolicy.StatusLine(CloudSaveCoordinator.State, OtherDeviceActive); }
        }

        // ---------------------------------------------------------------- 신호

        /**
         * @brief 부팅 직후 한 번. urgent 이벤트를 구독한다.
         *
         * 구독이 여기(정적) 있는 이유는 씬 재로드를 넘어 살아남기 때문이다 -
         * 충돌 해결이 씬을 다시 여는데, 그때마다 구독이 사라지면 재로드 뒤의
         * 첫 뽑기가 urgent를 놓친다. 대신 중복 구독을 스스로 막는다.
         */
        public static void Wire(EvolutionSystem evolution, GachaSystem gacha,
                                SkillGachaSystem skillGacha, GemWallet gems)
        {
            if (evolution != null)
            {
                evolution.Evolved -= OnEvolved;
                evolution.Evolved += OnEvolved;
            }

            if (gacha != null)
            {
                gacha.Pulled -= OnYodoPulled;
                gacha.Pulled += OnYodoPulled;
            }

            if (skillGacha != null)
            {
                skillGacha.Pulled -= OnSkillPulled;
                skillGacha.Pulled += OnSkillPulled;
            }

            if (gems != null)
            {
                gems.GemsChanged -= OnGemsChanged;
                gems.GemsChanged += OnGemsChanged;
                lastGems = -1L;
            }
        }

        /**
         * @brief 로컬 저장이 끝날 때마다. **저장된 그 스냅샷**이 올라갈 후보다.
         *
         * 디스크를 다시 읽지 않는 이유는 저장과 커밋 사이에 또 저장이 끼면
         * "무엇이 올라갔는가"를 아무도 모르게 되기 때문이다 - 스냅샷을 쥐고
         * 있으면 서버의 payload가 정확히 이 한 벌이다.
         */
        public static void NoteSaved(SaveData snapshot)
        {
            if (snapshot == null) return;

            pendingData = snapshot;
            if (dirtySince < 0f) dirtySince = Time.realtimeSinceStartup;
        }

        /**
         * @brief 충돌 화면의 재료를 밖에서 채운다 (61단계).
         *
         * 60단계에서는 커밋 거부·복귀 확인만 이 재료를 채웠다 - **부팅에서
         * 발견된 충돌**과 **계정 복구의 충돌**은 상태만 Conflict로 남고 설정의
         * 선택 버튼이 영영 안 살았다(버튼은 봉투가 있어야 뜬다). 이 창구가
         * 그 두 경로를 같은 화면으로 잇는다.
         */
        public static void NoteConflict(CloudSaveEnvelope server)
        {
            if (server == null) return;
            ConflictServerEnvelope = server;
        }

        /**
         * @brief 올릴 것이 없어졌다 (61단계 복구 ④ - 서버와 같은 기록).
         *
         * dirty를 그대로 두면 다음 디바운스가 내용 그대로인 revision 하나를 더
         * 올린다 - 쓰기 하나가 아깝다기보다, "안 바뀌었는데 revision이 올랐다"가
         * 진단을 흐린다.
         */
        public static void DropPending(string why)
        {
            pendingData = null;
            dirtySince = -1f;
            urgentSince = -1f;
            Debug.Log(Tag + " 올릴 것 정리: " + why);
        }

        /** 잃으면 지불이 사라지는 순간들. 다음 Tick에서 짧은 유예 뒤 올린다 */
        public static void RequestUrgent(string why)
        {
            if (urgentSince < 0f) urgentSince = Time.realtimeSinceStartup;
            Debug.Log(Tag + " urgent 동기화 예약: " + why);
        }

        /** 매 프레임 (GameSession.Update). 조건이 차면 커밋 하나를 띄운다 */
        public static void Tick()
        {
#if UNITY_EDITOR
            if (!EditorNetworkAllowed) return;
#endif
            if (committing) return;
            if (!CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State)) return;
            if (pendingData == null) return;

            float now = Time.realtimeSinceStartup;

            if (!CloudSaveSyncPolicy.ShouldCommit(
                    dirtySince < 0f ? -1f : now - dirtySince,
                    urgentSince < 0f ? -1f : now - urgentSince,
                    now - lastFailureAt))
                return;

            FirebaseRuntime.Observe(CommitAsync("주기 동기화"));
        }

        /**
         * @brief 앱이 뒤로 간다. 로컬은 이미 저장됐고(GameSession), 클라우드와
         *        세션 release를 **시도**한다.
         *
         * 완료를 보장으로 세지 않는다 - 모바일은 이 뒤에 프로세스를 예고 없이
         * 회수한다. 그래서 주기 동기화(120초)가 평소에 서 있어야 하고, 여기는
         * 마지막 기회일 뿐이다.
         */
        public static void OnAppPaused()
        {
#if UNITY_EDITOR
            if (!EditorNetworkAllowed) return;
#endif
            if (CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State) && pendingData != null
                && dirtySince >= 0f && !committing)
                FirebaseRuntime.Observe(CommitThenReleaseAsync());
            else
                FirebaseRuntime.Observe(ReleaseAsync());
        }

        /** 앱이 앞으로 돌아온다. 세션을 다시 잡고 서버가 움직였는지 본다 */
        public static void OnAppResumed()
        {
#if UNITY_EDITOR
            if (!EditorNetworkAllowed) return;
#endif
            FirebaseRuntime.Observe(ResumeAsync());
        }

        // ---------------------------------------------------------------- 내부

        private static async Task CommitAsync(string why)
        {
            committing = true;
            try
            {
                string uid = CloudScores.Uid;
                if (string.IsNullOrEmpty(uid)) { MarkFailure(); return; }

                CloudSaveLocalState sidecar = CloudSaveSidecar.Load();
                if (!CloudSavePolicy.SidecarAppliesTo(sidecar, uid))
                {
                    // 첫 커밋. 이 uid의 sidecar를 여기서 만든다 (revision 0 = 신규)
                    sidecar = CloudSaveLocalState.NewFor(uid, CloudSaveSidecar.NewDeviceId());
                    if (sidecar == null) { MarkFailure(); return; }
                }

                // 세션 없이는 규칙이 어차피 거부한다. 먼저 잡는다
                CloudSaveSessionStatus session =
                    await CloudSaveSession.AcquireAsync(uid, sidecar.deviceId);

                OtherDeviceActive = session == CloudSaveSessionStatus.Busy;

                if (!CloudSaveSession.HoldsWrite)
                {
                    if (session != CloudSaveSessionStatus.Offline) MarkFailure();
                    return;
                }

                SaveData data = pendingData;
                CloudSaveCommitResult result = await CloudSaveStore.CommitAsync(uid, data, sidecar);

                CloudSaveCoordinator.NoteSyncResult(CloudSaveSyncPolicy.StateAfterCommit(result.status));

                if (result.IsSynced)
                {
                    // 이 스냅샷은 올라갔다. 그 사이 새 저장이 있었으면 그쪽이 다음 dirty다
                    if (ReferenceEquals(pendingData, data))
                    {
                        pendingData = null;
                        dirtySince = -1f;
                    }
                    else
                    {
                        dirtySince = Time.realtimeSinceStartup;
                    }

                    urgentSince = -1f;
                    Debug.Log(Tag + " " + why + " 완료 (rev " + result.revision + ")");
                    return;
                }

                if (result.status == CloudSaveStoreStatus.Conflict)
                {
                    // **자동으로 아무것도 하지 않는다.** 서버 쪽을 읽어 화면에
                    // 보일 재료만 챙기고, 선택은 사람이 한다 (전투 중이면 그
                    // 화면은 안전한 시점에 열린다 - hot swap 금지)
                    CloudSaveFetchResult fetch = await CloudSaveStore.FetchAsync(uid);
                    ConflictServerEnvelope = fetch.status == CloudSaveStoreStatus.Found
                        ? fetch.envelope : null;

                    Debug.LogWarning(Tag + " 서버와 갈라졌습니다 (서버 rev " + result.revision
                                     + "). 기록 선택이 필요합니다.");
                    return;
                }

                MarkFailure();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Tag + " 동기화 예외: " + exception.Message);
                MarkFailure();
            }
            finally
            {
                committing = false;
            }
        }

        private static async Task CommitThenReleaseAsync()
        {
            await CommitAsync("백그라운드 동기화");
            await ReleaseAsync();
        }

        private static async Task ReleaseAsync()
        {
            CloudSaveLocalState sidecar = CloudSaveSidecar.Load();
            string uid = CloudScores.Uid;

            if (string.IsNullOrEmpty(uid) || sidecar == null) return;
            if (!CloudSaveSession.HoldsWrite) return;

            await CloudSaveSession.ReleaseAsync(uid, sidecar.deviceId);
        }

        private static async Task ResumeAsync()
        {
            string uid = CloudScores.Uid;
            CloudSaveLocalState sidecar = CloudSaveSidecar.Load();

            if (string.IsNullOrEmpty(uid) || !CloudSavePolicy.SidecarAppliesTo(sidecar, uid)) return;

            CloudSaveSessionStatus session = await CloudSaveSession.AcquireAsync(uid, sidecar.deviceId);
            OtherDeviceActive = session == CloudSaveSessionStatus.Busy;

            // 서버가 그 사이 움직였는가. **전투 중 핫스왑은 없다** - 갈라졌으면
            // 상태만 Conflict로 남기고, 화면은 안전한 시점(설정·다음 부팅)에 연다
            CloudSaveFetchResult fetch = await CloudSaveStore.FetchAsync(uid);
            if (fetch.status != CloudSaveStoreStatus.Found) return;

            if (fetch.envelope.revision > sidecar.baseRevision)
            {
                ConflictServerEnvelope = fetch.envelope;
                CloudSaveCoordinator.NoteSyncResult(CloudSaveState.Conflict);
                Debug.LogWarning(Tag + " 자리를 비운 사이 서버가 앞서 갔습니다 (rev "
                                 + fetch.envelope.revision + " > base " + sidecar.baseRevision
                                 + "). 기록 선택이 필요합니다.");
            }
        }

        private static void MarkFailure()
        {
            lastFailureAt = Time.realtimeSinceStartup;
        }

        // ---------------------------------------------------------------- urgent 귀

        private static void OnEvolved(int tier)
        {
            RequestUrgent("귀문 승리 (티어 " + tier + ")");
        }

        private static void OnYodoPulled(System.Collections.Generic.List<GachaSystem.PullResult> _)
        {
            RequestUrgent("요도 뽑기");
        }

        private static void OnSkillPulled(System.Collections.Generic.List<SkillGachaSystem.PullResult> _)
        {
            RequestUrgent("오의 뽑기");
        }

        /** 보석이 **줄었다** = 어딘가에서 지불했다. 동료·장비·단련이 전부 이 길목을 지난다 */
        private static void OnGemsChanged(long balance)
        {
            if (lastGems >= 0L && balance < lastGems) RequestUrgent("보석 소비 (" + lastGems + " -> " + balance + ")");
            lastGems = balance;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ResetForTests()
        {
#if UNITY_EDITOR
            EditorNetworkAllowed = false;
#endif
            pendingData = null;
            dirtySince = -1f;
            urgentSince = -1f;
            lastFailureAt = float.NegativeInfinity;
            committing = false;
            lastGems = -1L;
            OtherDeviceActive = false;
            ConflictServerEnvelope = null;
        }

        public static bool HasPendingForTests { get { return pendingData != null; } }
        public static bool IsUrgentForTests { get { return urgentSince >= 0f; } }
#endif
    }
}
