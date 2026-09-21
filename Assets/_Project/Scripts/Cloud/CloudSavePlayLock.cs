using System;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 이 기기의 **플레이를 통째로 멈추는** 한 개의 깃발 (63단계).
     *
     * 다른 기기가 세션을 인수했다. 회수하는 것은 로그인이 아니라 **권한**이다:
     * 전투 진행 · 재화 변경 · 뽑기와 구매 · 스테이지 진행 · 로컬 진행 저장 ·
     * 클라우드 작성권. Firebase 계정은 그대로 붙어 있다.
     *
     * ## 왜 깃발이 팝업보다 **먼저**인가
     *
     * 감지와 팝업 사이에는 언제나 한두 프레임이 있다. 그 사이에 10연 버튼이
     * 눌리면 그 뽑기는 **이미 남의 기기가 정본을 쥔 계정**에서 일어난다. 올라가지
     * 못하는 지불이고, 다음 부팅이 그것을 버린다. 그래서 순서가 계약이다:
     *
     *     깃발 → (이벤트) → 팝업
     *
     * `Engage`가 `Locked = true`를 세운 **뒤에** 이벤트를 쏜다. 구독자가 몇
     * 프레임 늦게 그려도 그 사이는 이미 잠겨 있다.
     *
     * ## 어디에 물려 있는가
     *
     * 문 하나마다 한 줄이다. 시스템마다 흩는 대신 **재화가 지나는 유일한 문**과
     * **진행이 지나는 유일한 문**에 건다:
     *
     *   `PlayerWallet.Add`/`TrySpend`   골드 획득·소비 (전투 보상이 여기로 온다)
     *   `GemWallet.Add`/`TrySpend`      보석 획득·소비 (뽑기·구매의 지불)
     *   `GachaSystem.TryFreePullAt`     무료 뽑기는 지불이 없어 지갑을 안 지난다
     *   `SkillGachaSystem` 무료 뽑기    같은 이유
     *   `StageProgress.RegisterKill`    처치 누적 - 스테이지 진행의 입구
     *   `GameSession.Save`              로컬 진행 저장
     *   `CloudSaveSync.Tick`/`NoteSaved` 클라우드 작성권
     *
     * `Time.timeScale`도 0으로 내린다. 위 문들이 이미 값의 변화를 막지만,
     * 화면이 계속 싸우는 것은 **사람에게 거짓을 보여 주는 것**이다 - 잡아도
     * 아무 일이 없는 전투를 몇 초 보여 주느니 멈추는 쪽이 정직하다.
     *
     * ## 잠금은 이 실행에서 풀리지 않는다
     *
     * `Release`는 검사와 씬 재로드용이다. 실제 사용자 흐름에서 이 깃발이 서면
     * 그 뒤는 타이틀뿐이다 - 같은 실행에서 다시 놀게 하려면 세션을 다시
     * 잡아야 하고, 그것은 새 실행이 할 일이다(세션 id는 실행마다 하나다).
     */
    public static class CloudSavePlayLock
    {
        private const string Tag = "[CloudSave]";

        /** 지금 이 기기의 플레이가 회수됐는가 */
        public static bool Locked { get; private set; }

        /** 왜 멈췄는가. 로그와 진단이 읽는다 */
        public static string Reason { get; private set; }

        /**
         * @brief **인계 중이다.** 새 진행은 이미 막혔고, 마지막 정리만 남았다.
         *
         * ## 왜 상태가 둘이어야 하는가
         *
         * 온라인 A는 인수 요청을 보면 스스로 물러난다(설계 §4.1): 새 변경을
         * 즉시 막고 → 현재 상태를 로컬에 저장하고 → 마지막 커밋을 시도하고 →
         * 세션을 놓는다. 그런데 그 **저장과 커밋이 이미 잠긴 기기에서 일어나야
         * 한다** - 잠금 하나로 전부 막으면 A가 넘기는 것은 몇 초 전의 기록이 되고,
         * 그 몇 초가 정확히 사람이 마지막으로 한 일이다.
         *
         * 그래서 `Locked`(새 진행 금지)와 `Sealing`(마지막 정리 허용)을 나눈다.
         * 지갑·처치·스테이지는 `Locked`만 보고 즉시 멈추고, 저장·커밋·release는
         * `Sealing` 동안만 지나간다.
         */
        public static bool Sealing { get; private set; }

        /** 새 진행이 막혔는가. 지갑·전투·스테이지가 보는 값 */
        public static bool Blocked
        {
            get { return Locked; }
        }

        /** 마지막 정리(저장·커밋·release)가 허용되는가 */
        public static bool AllowsFinalWrite
        {
            get { return !Locked || Sealing; }
        }

        /**
         * @brief 깃발이 **선 뒤에** 울린다. 종료 팝업이 구독한다.
         *
         * 여러 번 `Engage`해도 한 번만 울린다 - 폴링이 매 주기 같은 상실을
         * 다시 보기 때문이다. "종료 팝업이 한 번만 뜬다"가 그 계약이고,
         * 그것을 여기서 지키면 구독자마다 지킬 필요가 없다.
         */
        public static event Action Engaged;

        private static float releasedTimeScale = 1f;

        /**
         * @brief 플레이를 회수한다. **두 번째 호출은 아무 일도 하지 않는다.**
         *
         * @param reason 로그 한 줄. 사람이 읽는 문장이지 상태가 아니다
         */
        public static void Engage(string reason)
        {
            if (!BeginSeal(reason)) return;
            CompleteSeal();
        }

        /**
         * @brief **새 진행을 즉시 막고** 마지막 정리를 시작한다 (63단계 §4.1).
         *
         * 종료 팝업은 아직 뜨지 않는다 - 정리가 끝난 뒤에 뜬다. 사람에게
         * "종료합니다"를 보여 준 뒤에도 몇 초 더 서버와 주고받는 것은
         * 화면과 사실이 어긋나는 상태다.
         *
         * @return 이 호출이 실제로 봉인을 시작했는가. 이미 잠겼으면 false
         */
        public static bool BeginSeal(string reason)
        {
            if (Locked) return false;

            // ★ 순서가 계약이다. 깃발이 먼저, 이벤트가 나중
            Locked = true;
            Sealing = true;
            Reason = string.IsNullOrEmpty(reason) ? "다른 기기가 인수했습니다" : reason;

            releasedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            Debug.LogWarning(Tag + " 이 기기의 플레이를 회수합니다 - " + Reason
                             + " (Firebase 로그인은 유지됩니다).");
            return true;
        }

        /** 정리가 끝났다. **여기서 종료 팝업이 뜬다** - 한 번만 */
        public static void CompleteSeal()
        {
            if (!Sealing) return;

            Sealing = false;

            var handler = Engaged;
            if (handler != null) handler();
        }

        /**
         * @brief 깃발을 내린다. **씬 재로드와 검사 전용이다.**
         *
         * 사용자 흐름에서 이것을 부르는 곳은 타이틀로 나가는 자리 하나뿐이다 -
         * 그 뒤의 새 실행이 세션을 처음부터 다시 잡는다.
         */
        public static void Release()
        {
            if (!Locked) return;

            Locked = false;
            Sealing = false;
            Reason = string.Empty;
            Time.timeScale = releasedTimeScale <= 0f ? 1f : releasedTimeScale;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /**
         * @brief 정적 상태를 되돌린다. **검사 전용이다.**
         *
         * 구독자까지 지운다 - 파괴된 팝업의 델리게이트가 남아 다음 검사에서
         * 울리면, 그 검사는 자기가 만들지 않은 팝업을 보게 된다.
         */
        public static void ResetForTests()
        {
            Locked = false;
            Sealing = false;
            Reason = string.Empty;
            Engaged = null;

            Time.timeScale = 1f;
            releasedTimeScale = 1f;
        }
#endif
    }
}
