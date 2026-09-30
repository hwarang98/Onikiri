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

        // 62.1단계: 인증 준비·시간 경과·사이드카 유무를 손에 쥔다. 실제 Firebase도
        // 실제 시계도 없이 P0의 갈래(지연 로그인·타임아웃·uid 불일치)를 지나기 위한 것이다
        private static Func<string> lateUidOverride;
        private static Func<float> clockOverride;
        private static bool? sidecarPresenceOverride;

        /** uid가 **늦게** 도착하는 세계를 만든다. 부를 때마다 다른 값을 줄 수 있다 */
        public static void UseIdentityForTests(Func<string> provider)
        {
            lateUidOverride = provider;
        }

        /** 시계를 손에 쥔다. 6초 타임아웃을 6초 기다리지 않고 지나기 위한 것이다 */
        public static void UseClockForTests(Func<float> clock)
        {
            clockOverride = clock;
        }

        /** 디스크 사이드카의 **유무**만 바꾼다 (내용은 fetchOverride가 정한다) */
        public static void UseSidecarPresenceForTests(bool? present)
        {
            sidecarPresenceOverride = present;
        }

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
            lateUidOverride = null;
            clockOverride = null;
            sidecarPresenceOverride = null;
            AwaitedIdentity = false;
            chainArmed = false;
            chainUid = null;
            chainRevision = 0L;
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
                if (lateUidOverride != null) return lateUidOverride();
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

            // ★ 62.1단계 P0: **예산은 하나다.**
            //
            // 인증 대기와 서버 조회가 각자 6초를 쓰면 최악에 12초가 되고, 그것은
            // 오프라인에서 첫 화면이 12초 늦는다는 뜻이다. 그래서 마감을 여기서
            // 한 번 정하고 두 단계가 나눠 쓴다.
            float deadline = Now + CloudSavePolicy.BootServerCheckSeconds;

            // [0] 인증 준비 대기. **사이드카가 있는 기존 사용자만** 기다린다
            if (WillAwaitIdentity)
            {
                Debug.Log(Tag + " 사이드카가 있습니다. 인증 준비를 기다립니다 (최대 "
                          + CloudSavePolicy.BootServerCheckSeconds + "초).");

                while (string.IsNullOrEmpty(BootUid) && Now < deadline) yield return null;

                if (string.IsNullOrEmpty(BootUid))
                {
                    // 오프라인·로그인 지연. **로컬로 들어간다** - Firebase는 게이트가 아니다
                    LastServerStatus = CloudSaveStoreStatus.Offline;
                    AwaitedIdentity = true;
                    Debug.Log(Tag + " 인증이 제한 시간을 넘겼습니다. 로컬로 진입합니다.");

                    Finish(ChooseFrom(local, OwnSidecar(), default(CloudSaveFetchResult), false),
                           OwnSidecar(), default(CloudSaveFetchResult), onDone);
                    yield break;
                }

                AwaitedIdentity = true;
            }

            // uid가 정해진 **뒤에** 사이드카를 확정한다. 기다리는 동안 계정이
            // 갈렸다면(Recover) 그 사이드카는 이 uid의 것이 아니고,
            // `OwnSidecar`가 그것을 없는 것으로 친다 - 남의 기록은 자동 적용되지 않는다
            CloudSaveLocalState sidecar = OwnSidecar();
            string uid = BootUid;

            if (AwaitedIdentity && sidecar == null && DiskSidecar() != null)
                Debug.LogWarning(Tag + " 기다린 사이드카가 지금 uid("
                                 + Mask(uid) + ")의 것이 아닙니다. 자동 적용하지 않습니다.");

            CloudSaveFetchResult fetch = default(CloudSaveFetchResult);
            bool serverChecked = false;

            if (ShouldCheckServer(uid))
            {
                Task<CloudSaveFetchResult> task = Fetch(uid);

                while (!task.IsCompleted && Now < deadline) yield return null;

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

            Finish(ChooseFrom(local, sidecar, fetch, serverChecked), sidecar, fetch, onDone);
        }

        /**
         * @brief 고른 것을 마무리하고 **한 번만** 돌려준다.
         *
         * 갈래가 셋(인증 타임아웃·서버 타임아웃·정상)으로 늘면서 `onDone`을
         * 부르는 자리가 흩어질 위험이 생겼다. 두 번 부르면 `BootWith`가 두 번
         * 돌고, 그것은 곧 방치 보상 두 번이다 - 59단계가 없앤 사고 그대로다.
         * 그래서 나가는 문을 하나로 묶는다.
         */
        private static void Finish(CloudSaveBootChoice choice, CloudSaveLocalState sidecar,
                                   CloudSaveFetchResult fetch,
                                   Action<CloudSaveBootChoice> onDone)
        {
            CloudSaveBootChoice finished = Finish(choice, sidecar, fetch);
            if (onDone != null) onDone(finished);
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

        /**
         * @brief 부팅이 **인증 준비를 기다릴 것인가** (62.1단계 P0).
         *
         * `WillCheckServer`가 false여도 이것이 true면 코루틴 경로로 가야 한다 -
         * 그 둘을 한꺼번에 보지 않으면 "지금은 uid가 없으니 로컬"로 확정되고,
         * 1초 뒤 로그인이 끝나도 이미 늦다(§5.B).
         */
        public static bool WillAwaitIdentity
        {
            get
            {
                // 이미 기다린 부팅은 다시 기다리지 않는다. 예산은 하나다(62.1) -
                // 63단계에서 세션 게이트가 먼저 기다리게 되면서 생긴 조건이다
                if (AwaitedIdentity) return false;

                return AwaitIdentityAllowed && CloudSavePolicy.ShouldAwaitIdentity(
                    ServerCheckAllowed, !string.IsNullOrEmpty(BootUid), DiskSidecar() != null);
            }
        }

        /** 인증 준비를 실제로 기다린 부팅이었는가 (진단·검사가 읽는다) */
        public static bool AwaitedIdentity { get; private set; }

        /**
         * @brief 인증 준비를 기다린다. **부팅 판정보다 앞에서 쓸 수 있게 꺼냈다** (63단계).
         *
         * ## 왜 필요했는가 - 실기가 잡았다
         *
         * 63단계의 세션 게이트는 `ChooseBootSave`보다 **앞**에 선다(작성권이
         * 정본 재조회보다 먼저다). 그런데 그 자리에서는 아직 Firebase 로그인이
         * 안 끝나 `CloudScores.Uid`가 비어 있다 - 게이트는 uid 없이 들어와
         * 곧바로 `Offline`로 빠졌고, **다른 기기가 켜져 있어도 아무것도 묻지
         * 않았다.** 62.1단계 P0-1과 정확히 같은 모양이다(부팅 판정이 로그인보다
         * 1초 빨랐다).
         *
         * ## 예산은 여전히 하나다
         *
         * 여기서 기다리고 나면 `AwaitedIdentity`가 서고, `WillAwaitIdentity`가
         * 그 뒤로 false가 된다 - `ChooseBootSave`가 같은 기다림을 두 번째로
         * 하지 않는다. 오프라인에서 첫 화면이 12초 늦는 일이 없어야 한다.
         */
        public static IEnumerator AwaitIdentity()
        {
            if (!WillAwaitIdentity) yield break;

            Debug.Log(Tag + " 세션 게이트 전에 인증 준비를 기다립니다 (최대 "
                      + CloudSavePolicy.BootServerCheckSeconds + "초).");

            float deadline = Now + CloudSavePolicy.BootServerCheckSeconds;
            while (string.IsNullOrEmpty(BootUid) && Now < deadline) yield return null;

            AwaitedIdentity = true;

            if (string.IsNullOrEmpty(BootUid))
                Debug.Log(Tag + " 인증이 제한 시간을 넘겼습니다. 세션 없이 진행합니다.");
        }

        // ---------------------------------------------------------------- 사슬 확정

        /**
         * @brief **로컬 저장이 성공하기를 기다리는** 서버 사슬 (62.1.1 P1).
         *
         * 클라우드를 채택한 판정이 여기에 서버 revision/지문을 넣어 두고,
         * 디스크 저장이 성공했을 때 `NoteLocalSaveCommitted`가 꺼내 확정한다.
         * 저장이 실패하면 그대로 남아 **다음 자동 저장 성공**을 기다린다.
         */
        private static bool chainArmed;
        private static string chainUid;
        private static long chainRevision;
        private static string chainPayloadSha;
        private static string chainStateSha;
        private static long chainUpdatedAt;

        /** 확정을 기다리는 서버 revision. 없으면 0 (진단·검사가 읽는다) */
        public static long PendingChainRevision
        {
            get { return chainArmed ? chainRevision : 0L; }
        }

        private static void ArmChain(string uid, CloudSaveEnvelope envelope)
        {
            chainArmed = true;
            chainUid = uid;
            chainRevision = envelope.revision;
            chainPayloadSha = envelope.payloadSha256;
            chainStateSha = envelope.stateSha256;
            chainUpdatedAt = envelope.updatedAtUtcTicks;

            Debug.Log(Tag + " 서버 사슬 rev " + chainRevision
                      + " 확정 대기 - 로컬 저장이 성공해야 옮긴다.");
        }

        /**
         * @brief **로컬 저장이 디스크에 들어갔다.** 기다리던 사슬을 이제 확정한다.
         *
         * `GameSession.Save()`가 `SaveSystem.Save`의 성공을 확인한 뒤 부른다.
         * 저장이 실패한 호출에서는 부르지 않으므로, 예약은 그대로 남아 다음
         * 자동 저장 성공이 같은 일을 마저 한다.
         *
         * **정확히 한 번만 옮긴다** - 예약을 먼저 지우고 일한다. 두 번 옮기면
         * 그 자체는 같은 값이지만, "확정됐는가"를 묻는 자리가 두 답을 갖게 된다.
         */
        public static void NoteLocalSaveCommitted()
        {
            if (!chainArmed) return;

            CloudSaveLocalState chain = CloudSaveSidecar.Load();

            // 사슬이 없던 기기가 클라우드를 받은 경우다(재설치·복구 직후).
            // **저장이 성공한 지금** 세운다 - 여기서 안 만들면 첫 커밋이 base 0으로
            // 나가 방금 채택한 기록을 두고 충돌이 난다
            if (!CloudSavePolicy.SidecarAppliesTo(chain, chainUid))
                chain = CloudSaveLocalState.NewFor(chainUid, CloudSaveSidecar.NewDeviceId());

            // ★★ 62.1.2: **sidecar 파일이 디스크에 남은 뒤에만 예약을 푼다.**
            //
            // 예전에는 들어오자마자 `chainArmed = false`를 했다. 그러면 sidecar
            // 저장이 실패했을 때 예약이 사라져 **아무도 다시 시도하지 않는다** -
            // 로컬은 서버 기록인데 사슬은 옛 자리에 남아, 다음 커밋이 방금 채택한
            // 그 기록을 두고 충돌을 낸다(62.1이 실기에서 본 그 증상).
            if (chain != null
                && chain.MarkSynced(chainRevision, chainPayloadSha, chainStateSha, chainUpdatedAt)
                && CloudSaveSidecar.Save(chain))
            {
                chainArmed = false;
                Debug.Log(Tag + " 서버 사슬 확정: rev " + chainRevision + " (로컬 저장 완료 후).");
                return;
            }

            Debug.LogWarning(Tag + " 로컬 저장은 됐지만 사슬을 남기지 못했습니다 "
                             + "(rev " + chainRevision + "). 예약을 유지하고 "
                             + "다음 저장 성공에서 다시 시도합니다.");
        }

        /**
         * @brief 인증을 기다리는 것이 **이 실행에서 의미가 있는가.**
         *
         * ## 에디터에는 기다릴 로그인이 없다
         *
         * 실기에서는 부팅 직후 `CloudScores`가 로그인을 시작하고 1초 안팎에 uid가
         * 온다 - 기다림에 값이 있다. 에디터에는 그 로그인이 아예 없다(운영 프로젝트로
         * 나가지 않도록 게이트가 막는다). 그래서 에디터에서 기다리면 **오지 않을 것을
         * 6초 기다린 뒤 로컬로 들어가는 일**이 되고, 그 6초가 PlayMode를 망가뜨린다.
         *
         * ## 실제로 망가뜨렸다 (62.1 bisect)
         *
         * 이 게이트 없이 전량 PlayMode를 돌리면 `CloudConflictPlayTests`의 씬 재로드
         * 뒤에서 매달렸다. 검사들은 `fetchOverride`로 가짜 서버를 쓰면서 로그인은 하지
         * 않으므로 uid가 영영 오지 않고, 부팅이 코루틴으로 빠지면 **첫 프레임에
         * `GameSession`을 파괴하는 검사들**과 얽혀 59단계가 적어 둔 함정에 그대로 빠진다.
         *
         * 이 줄을 넣기 전 44개 중 매달림, 넣은 뒤 44/44. **원인은 추측이 아니라
         * bisect로 확정했다.**
         *
         * 검사가 uid를 직접 주는 경우에는 기다린다 - 그때는 실제로 도착할 uid가
         * 있기 때문이고, 62.1의 갈래들을 EditMode가 그 길로 지난다.
         */
        private static bool AwaitIdentityAllowed
        {
            get
            {
#if UNITY_EDITOR
                return lateUidOverride != null || uidOverride != null;
#else
                return true;
#endif
            }
        }

        /**
         * @brief 서버 확인이 **원리적으로** 허용되는가 (uid는 보지 않는다).
         *
         * `ShouldCheckServer`에서 uid 조건만 뺀 것이다. 기다릴지 정하는 시점에는
         * uid가 아직 없는 것이 정상이라, uid를 보는 판단으로는 답을 낼 수 없다.
         */
        private static bool ServerCheckAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (fetchOverride != null) return true;
#endif
#if UNITY_EDITOR
                if (!EditorServerCheckAllowed) return false;
                if (!FirebaseAppCheckBootstrap.EditorServerCallsAllowed) return false;
#endif
                return ServerCheckEnabled;
            }
        }

        /**
         * @brief 디스크의 사이드카. **uid로 거르지 않는다.**
         *
         * `OwnSidecar`는 uid와 대조하므로 로그인 전에는 언제나 null이다 - 그것으로는
         * "기다릴 값이 있는 기기인가"를 물을 수 없다. 여기서 묻는 것은 소유권이
         * 아니라 **존재**다: 이 기기가 서버와 사슬을 맺은 적이 있는가.
         */
        private static CloudSaveLocalState DiskSidecar()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (sidecarPresenceOverride.HasValue)
                return sidecarPresenceOverride.Value ? CloudSaveLocalState.NewFor(
                    "sidecar-presence-probe", CloudSaveIds.New()) : null;
#endif
            CloudSaveLocalState state = CloudSaveSidecar.Load();
            return state != null && state.IsWellFormed() ? state : null;
        }

        /** 지금 시각. 검사가 시간을 손에 쥘 수 있게 한 겹 둔다 */
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

        /** uid를 로그에 적을 때 쓰는 마스킹. 원문을 남기지 않는다 */
        private static string Mask(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(없음)";
            return id.Length <= 6 ? id : id.Substring(0, 6) + "…";
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

            // ★ 62.1: **사슬을 서버 자리로 옮기는 것이 두 갈래다.**
            //
            //   InSync        같은 기록이다. revision만 채택한다
            //   DownloadCloud 서버 것을 로컬로 삼았다. 지금 로컬은 **서버 rev N에서 왔다**
            //
            // 예전에는 InSync만 옮겼다. 62.1 이전에는 실기 부팅이 클라우드를 채택하는
            // 일이 아예 없었으므로(그것이 P0-1이었다) 이 빠짐이 드러나지 않았다.
            //
            // 실기에서 그대로 났다: 부팅이 rev 124를 채택했는데 사이드카는 91에 남았고,
            // 2분 뒤 커밋이 base 91 vs 서버 124를 보고 **방금 채택한 그 기록을 두고**
            // 충돌을 냈다("서버와 갈라졌습니다 (서버 rev 124)"). 최신을 받아 놓고
            // 곧바로 다시 고르라고 묻는 것은 P0를 고친 의미를 없앤다.
            bool adoptedServerChain = choice.decision == CloudSaveDecision.InSync
                                      || choice.decision == CloudSaveDecision.DownloadCloud;

            // ★★ 62.1.1 P1: **여기서 사슬을 확정하지 않는다. 예약만 한다.**
            //
            // 고른 클라우드 한 벌이 디스크에 들어가는 것은 이 뒤의
            // `GameSession.BootWith` → `Save()`다. 여기서 사이드카를 먼저 옮기면
            // 그 사이에 앱이 죽거나 저장이 실패했을 때 이런 상태가 남는다:
            //
            //     로컬 세이브 파일 = 옛 기록
            //     sidecar base     = 최신 서버 revision
            //
            // 다음 동기화는 그 옛 기록을 **서버에서 파생된 변경분**으로 읽고
            // 올린다 - 다른 기기의 최신 진행을 옛 기록으로 덮는 길이 열린다.
            // 사슬은 디스크가 사실이 된 뒤에만 사실이어야 한다.
            if (adoptedServerChain && fetch.envelope != null)
                ArmChain(BootUid, fetch.envelope);

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

            // 62단계: 스위치를 켰더라도 App Check debug token이 없으면 안 나간다.
            // 토큰 없이 나간 요청은 enforcement 뒤에 전부 거부되고, 그 거부는
            // 규칙이 틀린 것과 로그에서 구분되지 않는다
            if (!FirebaseAppCheckBootstrap.EditorServerCallsAllowed)
            {
                Debug.Log(Tag + " 부팅 서버 확인 안 함 - App Check: "
                          + FirebaseAppCheckBootstrap.EditorBlockReason);
                return false;
            }
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
