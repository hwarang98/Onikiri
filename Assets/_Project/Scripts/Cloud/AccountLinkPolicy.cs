namespace Onikiri.Cloud
{
    /**
     * @brief 지금 이 기기의 정체성이 어떤 상태인가. 계정 UI가 그리는 세 문장.
     *
     * "로그인했는가"가 아니라 **"정체성이 기기 밖에 있는가"**를 가른다.
     * 익명 로그인도 로그인이지만 그 uid는 앱을 지우면 사라지므로, 사람에게
     * 는 로그인이 아니라 **게스트**다.
     */
    public enum AccountState
    {
        /** Firebase가 아직 안 떴다. 상태를 모른다 - "없음"과 다른 말이다 */
        Unknown = 0,

        /** 익명. 앱을 지우면 기록이 사라진다 */
        Guest,

        /** 구글(또는 애플)이 붙었다. 재설치해도 돌아온다 */
        Linked,
    }

    /**
     * @brief 자격 증명을 받은 뒤 무엇을 할 것인가.
     *
     * 이 세 갈래가 55단계의 전부다. 화면도 로그도 이 이름을 그대로 쓴다 -
     * 실기에서 어느 갈래를 밟았는지가 logcat 한 줄로 읽혀야 한다.
     */
    public enum LinkPlan
    {
        /** 지금 익명 uid에 자격 증명을 붙인다. **uid가 유지된다** = 기록이 그대로 이어진다 */
        Link,

        /** 그 계정은 이미 있다. 연동이 아니라 **로그인**해서 그 uid로 돌아간다 */
        Recover,

        /** 이미 붙어 있다 / 사람이 취소했다 / 이 플랫폼에 구현이 없다 */
        Nothing,

        /** 네트워크 문제. 다시 눌러보면 되는 종류라 문구가 달라야 한다 */
        Retry,
    }

    /**
     * @brief 연동·복구·병합의 **규칙**. Firebase도 유니티도 한 줄 없다.
     *
     * LeaderboardPolicy와 같은 이유로 갈라져 있다 - 여기 적힌 것은 네트워크의
     * 성질이 아니라 이 게임의 성질이고, 그래서 EditMode 테스트가 Firebase 없이
     * 검사할 수 있다. AccountLink(실제 호출)는 실기 logcat으로만 증명되므로,
     * **증명 가능한 부분을 최대한 이쪽으로 끌어내는 것**이 이 파일의 존재 이유다.
     *
     * ## 이 스텝의 알맹이는 "연동"이 아니라 복구다
     *
     * 연동(Link)은 쉽다 - 익명 유저에 자격 증명을 붙이면 uid가 그대로고 문서도
     * 그대로다. 어려운 것은 **재설치·다기기**다. 그때 기기는 새 익명 uid를
     * 들고 있고, 예전 구글 계정으로 붙이려 하면 Firebase가 거절한다
     * (`credential-already-in-use`). 그 거절이 이 흐름의 **정상 경로**이고,
     * 거기서 연동을 포기하고 **로그인으로 갈아타는 것**이 복구다.
     */
    public static class AccountLinkPolicy
    {
        /**
         * @brief 자격 증명을 받기 **전에** 정하는 첫 계획.
         *
         * @param signedIn    Firebase에 로그인된 사용자가 있는가 (보통 익명)
         * @param anonymous   그 사용자가 익명인가
         * @param alreadyHas  이 공급자가 이미 붙어 있는가
         *
         * 익명이 아닌데 로그인돼 있다 = 이미 구글이 붙은 계정이다. 그때 또
         * 붙이려 하면 Firebase가 `provider-already-linked`를 던지는데, 그
         * 예외를 받아 문구를 만드는 것보다 **애초에 안 부르는 것**이 맞다.
         *
         * 로그인 자체가 안 돼 있으면(부팅 실패·오프라인 첫 실행) 연동할 대상이
         * 없다. 여기서 Link를 돌려주면 익명 로그인이 없는 상태로 Link를 불러
         * NullReference가 난다 - 그 순간은 네트워크가 이미 나쁜 순간이라
         * 가장 안 좋은 자리에서 터진다.
         */
        public static LinkPlan PlanFor(bool signedIn, bool anonymous, bool alreadyHas)
        {
            if (alreadyHas) return LinkPlan.Nothing;
            if (!signedIn) return LinkPlan.Retry;
            if (!anonymous) return LinkPlan.Nothing;
            return LinkPlan.Link;
        }

        /**
         * @brief 네이티브 로그인이 실패했다. 그 다음은?
         *
         * 취소를 Retry로 두지 않는 것이 중요하다 - 사람이 계정 선택창을 닫은
         * 것은 "안 하겠다"이지 "실패"가 아니다. 여기서 재시도 문구를 띄우면
         * 닫은 창이 다시 뜨는 것처럼 읽힌다.
         */
        public static LinkPlan PlanAfterAcquireFailure(AuthFailure failure)
        {
            if (failure == AuthFailure.Network) return LinkPlan.Retry;
            return LinkPlan.Nothing;
        }

        /**
         * @brief 연동(Link)이 실패했다. 그 다음은? **여기가 이 스텝의 분기점이다.**
         *
         * AlreadyInUse만이 Recover로 간다. 나머지는 전부 멈춘다 - 특히
         * "아무 실패나 일단 로그인으로 갈아탄다"로 만들면 안 된다. 로그인은
         * **지금 기기의 익명 uid를 버리는** 동작이라, 애매한 실패에서 그것을
         * 하면 멀쩡한 사람의 진행이 버려질 수 있다.
         */
        public static LinkPlan PlanAfterLinkFailure(AuthFailure failure)
        {
            if (failure == AuthFailure.AlreadyInUse) return LinkPlan.Recover;
            if (failure == AuthFailure.Network) return LinkPlan.Retry;
            return LinkPlan.Nothing;
        }

        /**
         * @brief 복구할 때 살아남을 도달층. **높은 쪽을 남긴다.**
         *
         * 버려지는 익명 uid의 문서와 돌아온 계정의 문서 중 큰 값이다.
         * 후퇴 방어(LeaderboardPolicy)와 **같은 원칙**이고, 같은 원칙인 것이
         * 중요하다 - 정체성이 바뀌는 순간만 예외적으로 기록이 내려갈 수 있다면
         * 그것이 곧 기록을 지우는 방법이 된다(낮은 기기에서 로그인하기).
         *
         * 로컬 진행도 함께 본다. 셋 중 가장 큰 값이 답이다:
         *
         *   local      이 기기의 세이브가 실제로 도달한 층
         *   abandoned  버려질 익명 uid의 서버 문서 (보통 local과 같거나 낮다)
         *   recovered  돌아온 계정의 서버 문서 (다른 기기·재설치 전의 기록)
         *
         * abandoned를 굳이 세는 이유는 local과 늘 같지는 않아서다 - 세이브를
         * 백업에서 되돌린 기기는 로컬이 서버보다 낮다. 그 경우에도 자기가
         * 올렸던 기록은 자기 것이다.
         */
        public static int MergedStage(int local, int abandoned, int recovered)
        {
            int best = local;
            if (abandoned > best) best = abandoned;
            if (recovered > best) best = recovered;
            return best;
        }

        /**
         * @brief 복구 뒤에 서버로 쓸 일이 있는가.
         *
         * 돌아온 계정의 기록이 이미 가장 높으면 아무것도 안 쓴다. 이것이
         * 흔한 경우다(재설치 = 로컬 1층, 서버 171층) - 그때 쓰기를 한 번
         * 보내면 규칙이 거부할 뿐이고 요금만 쓴다.
         */
        public static bool ShouldMergeAfterRecovery(int merged, int recovered)
        {
            return merged > recovered;
        }

        /**
         * @brief 복구된 문서의 이름을 되살릴 것인가.
         *
         * **이름도 기록의 일부다.** 데이터를 지운 기기에는 이름이 없는데
         * 돌아온 문서에는 그 사람이 정한 이름이 적혀 있다 - 되살리지 않으면
         * 랭킹표에서는 "랑무사"인데 자기 화면에서는 "이름없는 무사"가 되어
         * 복구가 절반만 된 것으로 보인다(실기에서 그렇게 떴다).
         *
         * 스스로 정한 이름이 이미 있으면 **건드리지 않는다**. 그 값은 이
         * 기기의 사람이 방금 고른 것이고, 서버의 옛 이름으로 덮는 것은
         * 복구가 아니라 되돌리기다.
         */
        public static bool ShouldRestoreName(bool hasChosenName, string recoveredName)
        {
            if (hasChosenName) return false;
            return !string.IsNullOrEmpty(recoveredName);
        }

        /**
         * @brief 버려지는 익명 문서를 지울 것인가. **지금은 늘 false다.**
         *
         * 규칙 4-B가 클라이언트의 delete를 막아 두었기 때문이다
         * (`allow delete: if false`). 그것을 이 스텝 때문에 여는 것은
         * 거래가 맞지 않는다 - 삭제 권한이 열리면 가장 먼저 생기는 피해자는
         * "남의 uid를 알아낸 사람"이 아니라 **자기 기록을 실수로 날린 사람**
         * 이라고 그 규칙에 이미 적어 두었고, 그 판단은 여기서도 그대로다.
         *
         * 남는 문서가 랭킹을 더럽히지 않는가? 더럽히지 않는다 - 그 문서의
         * 도달층은 **병합으로 이미 복구된 계정에 옮겨졌고**, 원래 값 이상으로
         * 자라지 않는다(그 uid로 다시 로그인할 방법이 없다). 즉 죽은 줄
         * 하나가 순위표 어딘가에 남는데, 그것은 이 사람의 예전 기록이므로
         * 잘못된 값도 아니다.
         *
         * ⚠️ **출시 전 교체 대상**: 계정 삭제 요청(GDPR·앱스토어 계정 삭제
         * 의무)이 서면 서버 쪽(Cloud Functions)이 이 정리를 맡아야 한다.
         * 클라이언트에 권한을 주는 방식으로는 풀지 않는다.
         */
        public static bool ShouldDeleteAbandonedDocument()
        {
            return false;
        }
    }
}
