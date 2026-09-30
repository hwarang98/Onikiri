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

        /** 다른 기기가 세션을 쥐고 있다는 로그를 **상태가 바뀔 때만** 적는다 */
        private static bool loggedBusy;
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

        /** App Check 때문에 막혔다는 로그를 한 번만 남긴다 - Tick은 매 프레임이다 */
        private static bool loggedAppCheckBlock;

        /**
         * @brief 지금 에디터에서 실서버로 나가면 안 되는가.
         *
         * 스위치(위)와 App Check 토큰(62단계) **둘 다** 있어야 나간다. 토큰이
         * 없으면 enforcement 뒤에 전부 거부되는 요청이 120초마다 하나씩 나가고,
         * 그 거부 로그가 규칙 문제로 읽힌다.
         */
        private static bool EditorBlocksNetwork()
        {
            if (!EditorNetworkAllowed) return true;

            if (FirebaseAppCheckBootstrap.EditorServerCallsAllowed)
            {
                loggedAppCheckBlock = false;
                return false;
            }

            if (!loggedAppCheckBlock)
            {
                loggedAppCheckBlock = true;
                Debug.Log(Tag + " sync가 나가지 않습니다 - App Check: "
                          + FirebaseAppCheckBootstrap.EditorBlockReason);
            }

            return true;
        }
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
            // 회수 뒤의 스냅샷은 업로드 후보가 아니다 (63단계). 봉인 중의
            // 마지막 한 벌은 예외다 - 그것이 다음 기기로 넘어간다
            if (!CloudSavePlayLock.AllowsFinalWrite) return;

            if (snapshot == null) return;

            pendingData = snapshot;
            if (dirtySince < 0f) dirtySince = Now;
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

        /**
         * @brief urgent 커밋 **직전에** "지금 상태를 저장하라"고 부르는 창구.
         *
         * `GameSession`이 자기 `Save()`를 여기 걸어 둔다. 방향이 거꾸로인 것처럼
         * 보이지만, `Wire`와 같은 모양이다 - 수명과 시계를 쥔 쪽이 GameSession이고
         * 이쪽은 신호만 보낸다.
         *
         * ## 62단계 실기 실측이 연 빚
         *
         * `GameSession.Save()`는 **자동 저장 30초 · pause · focus 상실 · 종료**
         * 에서만 돈다. 보석 소비·뽑기에는 저장이 없다. 그런데 urgent가 올리는 것은
         * `pendingData`(= 마지막 저장 스냅샷)라, 유예 2초 안에 자동 저장이 끼지
         * 않으면 **지불 전 스냅샷**이 올라간다.
         *
         * Note 20에서 2/2 재현했다: 보석 1022→992를 쓴 urgent가 rev 44에 1022를,
         * 992→962를 쓴 urgent가 rev 45에 992를 올렸다. 확률로 치면 2초/30초 =
         * 약 7%만 제대로 실렸다는 뜻이고, urgent가 하려던 일을 사실상 못 하고 있었다.
         *
         * 재화가 복제되지는 않는다 - 전체 스냅샷을 올리므로 지불과 상품이 **함께**
         * 빠진다(설계 §3의 성질이 여기서 방벽 노릇을 했다). 그래도 "잃으면 지불이
         * 사라지는 창"은 그대로 열려 있었다.
         *
         * ## 62.1.2: **성공 여부를 돌려준다**
         *
         * `Action`이던 시절에는 저장이 실패해도 그 사실이 여기로 돌아오지 않았다.
         * 그래서 urgent 커밋이 그대로 진행됐고, **디스크에 없는 메모리 스냅샷이나
         * 그보다 더 낡은 `pendingData`가 서버로 올라갈 수 있었다.**
         */
        public static Func<bool> SaveRequested;

        /**
         * @brief 지금 시각. 검사가 재시도 유예(30초)를 손에 쥘 수 있게 한 겹 둔다.
         *
         * 실기에서는 언제나 `Time.realtimeSinceStartup`이다.
         */
        private static float Now
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (clockOverride != null) return clockOverride();
#endif
                return Time.realtimeSinceStartup;
            }
        }

        /** 잃으면 지불이 사라지는 순간들. 다음 Tick에서 짧은 유예 뒤 올린다 */
        public static void RequestUrgent(string why)
        {
            if (CloudSavePlayLock.Locked) return;

            if (urgentSince < 0f) urgentSince = Now;
            Debug.Log(Tag + " urgent 동기화 예약: " + why);
        }

        /** 매 프레임 (GameSession.Update). 조건이 차면 커밋 하나를 띄운다 */
        public static void Tick()
        {
            // ★ 63단계: 회수된 기기는 서버로 아무것도 보내지 않는다. 규칙도
            // 막지만(writerHoldsSession), 거부될 요청을 120초마다 보내면 그
            // 거부 로그가 진짜 문제를 덮는다
            if (CloudSavePlayLock.Locked) return;

#if UNITY_EDITOR
            // 커밋을 실제로 띄우지 않는 검사 모드에서는 이 게이트를 지난다.
            // 게이트가 막으려는 것은 **에디터에서 나가는 실서버 왕복**인데,
            // 억제 중에는 나갈 수 있는 요청 자체가 없다 - 그리고 게이트가 여기서
            // 돌아가 버리면 62.1.2가 재려는 계약("저장 실패 뒤 커밋 0회")을
            // 성공 갈래와 구분할 수 없다. 둘 다 0이기 때문이다.
            if (!SuppressCommitForTests && EditorBlocksNetwork()) return;
#endif
            if (committing) return;
            if (!CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State)) return;

            float now = Now;

            // ★ urgent은 **자기 스냅샷을 스스로 세운다.** (62단계 실기가 연 빚)
            //
            // 이 블록이 `pendingData == null` 가드보다 **앞에** 있어야 하는 이유가
            // 두 가지다. 둘 다 실기에서 실제로 물렸다:
            //
            //   ① 커밋 직후의 지불은 **영영 안 올라간다.** 성공한 커밋은
            //      pendingData=null · dirtySince=-1로 끝나는데, `ShouldCommit`의
            //      첫 줄이 `dirty < 0 → false`다. 그래서 보석을 써도 자동 저장
            //      30초가 오기 전까지 urgent은 아무 일도 못 한다.
            //
            //   ② 자동 저장이 먼저 와 있으면 그 스냅샷은 **지불 전**이다.
            //      `GameSession.Save()`는 30초 주기·pause·종료에서만 돌고
            //      보석 소비·뽑기에는 저장이 없다. 유예 2초 안에 저장이 낄 확률은
            //      약 7%라, 나머지는 지불이 빠진 한 벌을 올린다.
            //
            // 재화가 복제되지는 않았다 - 전체 스냅샷이라 지불과 상품이 함께 빠진다
            // (설계 §3의 성질이 방벽 노릇을 했다). 그래도 "잃으면 지불이 사라지는
            // 창"은 열려 있었고, urgent이 막으려던 것이 정확히 그 창이다.
            //
            // 유예가 찬 뒤에만 부른다 - 매 프레임 저장하면 10연이 디스크를 열 번 친다.
            if (urgentSince >= 0f
                && now - urgentSince >= CloudSaveSyncPolicy.UrgentDelaySeconds
                && now - lastFailureAt >= CloudSaveSyncPolicy.RetryDelaySeconds)
            {
                var save = SaveRequested;

                // ★★ 62.1.2: **저장이 실패하면 이 커밋은 여기서 끝난다.**
                //
                // 예전에는 결과를 안 봤다. 그러면 두 가지가 서버로 갈 수 있었다:
                //   ① 디스크에 없는 메모리 스냅샷
                //   ② 그보다 더 낡은, 실패 전의 `pendingData`
                // 둘 다 "서버가 로컬보다 앞선 것을 들고 있다"는 거짓을 만든다.
                //
                // urgent 표시는 **지우지 않는다.** 지불은 여전히 서버에 없고,
                // 다음 저장이 성공하면 그때 최신 한 벌로 올라가야 한다.
                if (save != null && !save())
                {
                    MarkFailure();
                    Debug.LogWarning(Tag + " 로컬 저장이 실패해 urgent 커밋을 멈춥니다 "
                                     + "(urgent 예약은 유지 - 다음 저장 성공이 올립니다).");
                    return;
                }
            }

            if (pendingData == null) return;

            if (!CloudSaveSyncPolicy.ShouldCommit(
                    dirtySince < 0f ? -1f : now - dirtySince,
                    urgentSince < 0f ? -1f : now - urgentSince,
                    now - lastFailureAt))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 커밋을 띄운 횟수. "저장 실패 뒤에는 0"이 62.1.2의 계약이고,
            // Firebase 없이 그것을 재려면 세는 수가 여기 있어야 한다
            CommitAttemptsForTests++;
            if (SuppressCommitForTests)
            {
                // 실제 커밋이 하던 것과 같은 자리에 깃발을 세운다. 이것이 없으면
                // 다음 Tick이 같은 지불로 커밋을 한 번 더 띄우고, "정확히 한 번"이
                // 검사에서만 두 번으로 보인다 - 실기에서는 committing이 막는다
                committing = true;
                return;
            }
#endif

            FirebaseRuntime.Observe(CommitAsync(
                urgentSince >= 0f ? "urgent 동기화" : "주기 동기화"));
        }

        /**
         * @brief 앱이 뒤로 간다. 클라우드와 세션 release를 **시도**한다.
         *
         * 완료를 보장으로 세지 않는다 - 모바일은 이 뒤에 프로세스를 예고 없이
         * 회수한다. 그래서 주기 동기화(120초)가 평소에 서 있어야 하고, 여기는
         * 마지막 기회일 뿐이다.
         *
         * ## ★★ 62.1.2: 바로 앞 저장이 성공했는가를 받는다
         *
         * `GameSession.OnApplicationPause`가 `Save()` 바로 뒤에 이것을 부른다.
         * 그 저장이 실패했으면 `pendingData`에 남은 것은 **실패 전의 낡은
         * 스냅샷**이고, 여기서 올리면 urgent 갈래에서 막은 것을 pause 갈래로
         * 그대로 통과시키는 것이 된다(지시서: "자동 저장·pause·focus·quit 경로도
         * 같은 성공 계약").
         *
         * **release는 그래도 한다.** 저장이 실패했다고 세션을 쥐고 있으면
         * 다른 기기가 180초 만료까지 묶인다 - 올리지 않는 것과 잡고 있는 것은
         * 다른 문제다.
         *
         * @param localSaveOk 직전 `GameSession.Save()`가 디스크에 넣는 데 성공했는가
         */
        public static void OnAppPaused(bool localSaveOk = true)
        {
#if UNITY_EDITOR
            if (EditorBlocksNetwork()) return;
#endif
            if (!localSaveOk)
            {
                Debug.LogWarning(Tag + " 로컬 저장이 실패해 pause 커밋을 건너뜁니다 "
                                 + "(세션은 놓아준다 - 다른 기기를 만료까지 묶지 않는다).");
                FirebaseRuntime.Observe(ReleaseAsync());
                return;
            }

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
            if (EditorBlocksNetwork()) return;
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

                    // 62단계 실측이 연 빚: 이 갈래는 **아무것도 안 적고 돌아갔다.**
                    // 그래서 두 기기 실측에서 "커밋이 왜 안 나가는가"를 알아내는 데
                    // 진단을 한 번 더 돌려야 했다(상태 문장에는 적히지만, 로그만
                    // 읽는 사람에게는 침묵과 구분되지 않는다).
                    //
                    // 상태가 바뀔 때만 적는다 - 재시도는 30초마다 오므로 매번 적으면
                    // 다른 기기가 켜져 있는 동안 로그가 그 줄로 덮인다.
                    if (session == CloudSaveSessionStatus.Busy && !loggedBusy)
                    {
                        loggedBusy = true;
                        Debug.Log(Tag + " " + why + " 보류 - 다른 기기가 세션을 쥐고 있습니다 "
                                  + "(놓거나 " + CloudSavePolicy.SessionExpirySeconds
                                  + "초 만료 뒤 인수).");
                    }
                    return;
                }

                loggedBusy = false;

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
                        dirtySince = Now;
                    }

                    urgentSince = -1f;

                    // AlreadyApplied를 "완료"와 같은 말로 적으면 **응답 유실 복구가
                    // 로그에서 사라진다.** 62단계 실측에서 revision을 앞뒤로 대조해야만
                    // 그 갈래를 판정할 수 있었다 - 새로 쓴 것과 이미 있던 것을 확인한
                    // 것은 다른 사건이고, 다른 사건은 다르게 적혀야 한다.
                    Debug.Log(Tag + " " + why
                              + (result.status == CloudSaveStoreStatus.AlreadyApplied
                                  ? " 확인 (rev " + result.revision
                                    + " - 이미 적용됨, 응답 유실 복구)"
                                  : " 완료 (rev " + result.revision + ")"));
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

        /**
         * @brief 자리를 넘긴다: **마지막 커밋 -> release** (63단계 §4.1).
         *
         * `OnAppPaused`와 닮았지만 다른 것이 하나 있다 - 여기는 이미 잠긴
         * 기기에서 돈다(`CloudSavePlayLock.Sealing`). `ReleaseAsync`가
         * `HoldsWrite`를 보므로 봉인이 그 값을 살려 두는 것이 전제다.
         */
        public static async Task YieldSeatAsync()
        {
            if (CloudSaveSyncPolicy.MayWrite(CloudSaveCoordinator.State)
                && pendingData != null && dirtySince >= 0f && !committing)
                await CommitAsync("인수 인계");

            await ReleaseAsync();
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
            lastFailureAt = Now;
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
        /**
         * @brief urgent 시계가 언제 걸렸는가. 안 걸렸으면 음수. **진단·테스트 전용.**
         *
         * 62단계 6절이 재는 것이 이 값의 **불변성**이다: 10연 한 번에 뽑기 이벤트
         * 열 개와 보석 감소 하나가 같은 프레임에 오는데, 그때마다 시계가 새로
         * 걸리면 유예가 계속 밀리거나(늦은 업로드) 이벤트마다 커밋이 하나씩
         * 나간다(revision 폭증). 처음 걸린 값이 그대로 남아야 한 커밋으로 모인다.
         */
        public static float UrgentSinceForTests
        {
            get { return urgentSince; }
        }

        /** Tick이 커밋을 띄운 횟수. **저장 실패 뒤에는 늘면 안 된다** (62.1.2) */
        public static int CommitAttemptsForTests { get; private set; }

        /** 커밋을 실제로 띄우지 않고 세기만 한다 - Firebase 없이 Tick 계약을 잰다 */
        public static bool SuppressCommitForTests;

        /** 올릴 후보가 등록돼 있는가. `NoteSaved`가 불렸는지를 잰다 */
        public static bool HasPendingSnapshotForTests
        {
            get { return pendingData != null; }
        }

        /** 시계를 손에 쥔다. 재시도 유예(30초)를 30초 기다리지 않기 위한 것이다 */
        public static void UseClockForTests(Func<float> clock)
        {
            clockOverride = clock;
        }

        private static Func<float> clockOverride;

        public static void ResetForTests()
        {
#if UNITY_EDITOR
            EditorNetworkAllowed = false;
            loggedAppCheckBlock = false;
#endif
            // 파괴된 GameSession의 Save를 다음 검사가 부르면 안 된다
            SaveRequested = null;

            pendingData = null;
            dirtySince = -1f;
            urgentSince = -1f;
            lastFailureAt = float.NegativeInfinity;
            committing = false;
            loggedBusy = false;
            lastGems = -1L;
            clockOverride = null;
            SuppressCommitForTests = false;
            CommitAttemptsForTests = 0;
            OtherDeviceActive = false;
            ConflictServerEnvelope = null;
        }

        public static bool HasPendingForTests { get { return pendingData != null; } }
        public static bool IsUrgentForTests { get { return urgentSince >= 0f; } }
#endif
    }
}
