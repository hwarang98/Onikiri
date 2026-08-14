namespace Onikiri.UI
{
    /**
     * @brief 부팅 화면의 단계. 순서대로만 흐르고 되돌아가지 않는다.
     *
     * Entered가 마지막이다 - 인트로는 한 실행에 한 번이고, 게임으로 들어간
     * 뒤에 다시 나타나는 경로는 없다(테스트 패널의 "다시 보기"만 예외).
     */
    public enum IntroPhase
    {
        /** 202 STUDIO. 브랜드의 첫 화면 */
        Studio = 0,

        /** ONIKIRI 로고만. CTA는 아직 없다 */
        Brand,

        /** 타이틀. CTA(계정 선택 또는 터치하여 시작)가 함께 선다 */
        Title,

        /** 오버레이가 내려가고 게임이 보인다 */
        Entered,
    }

    /**
     * @brief 부팅 화면의 규칙. **순수 로직이라 EditMode에서 전부 검사된다.**
     *
     * IntroFlow(MonoBehaviour)가 매 프레임 묻고 이 클래스가 답한다. 갈라 둔
     * 이유는 AccountLinkPolicy와 같다 - 분기가 컴포넌트 안에 살면 씬 없이는
     * 검사할 수 없고, 이 분기들은 전부 "첫 실행의 사람"에게만 드러나는
     * 것이라 개발 중에는 아무도 안 밟는다.
     *
     * ## 게임에 대한 계약: 영향 0
     *
     * 밸런스·세이브·전투 루프를 건드리지 않는다. 진입 흐름과 화면만이다.
     */
    public static class IntroPolicy
    {
        /**
         * @brief 스플래시 한 장의 길이 (초).
         *
         * 짧다. 재실행마다 보는 화면이라 길면 브랜드가 아니라 장애물이 된다.
         * 그리고 어느 장이든 탭 한 번으로 건너뛴다 - 스킵을 막아서 얻는 것은
         * 노출 몇 초이고 잃는 것은 매 실행의 짜증이다.
         */
        public const float StudioSeconds = 1.4f;
        public const float BrandSeconds = 1.1f;

        /**
         * @brief 스플래시 단계를 한 칸 민다. 탭이나 시간, 어느 쪽이든 먼저 온 것.
         *
         * Title에서 멈춘다 - 타이틀을 시간으로 지나가게 하지 않는다. 진입은
         * 언제나 사람의 탭이다(자동 진입이 필요한 재실행 유저도 "터치하여
         * 시작"을 거친다. 로드 게이트가 열리기 전에 게임이 드러나는 경로를
         * 시간 축에 만들지 않기 위해서다).
         */
        public static IntroPhase Next(IntroPhase phase, bool tapped, float elapsedInPhase)
        {
            switch (phase)
            {
                case IntroPhase.Studio:
                    return tapped || elapsedInPhase >= StudioSeconds ? IntroPhase.Brand : phase;
                case IntroPhase.Brand:
                    return tapped || elapsedInPhase >= BrandSeconds ? IntroPhase.Title : phase;
                default:
                    return phase;
            }
        }

        /**
         * @brief 타이틀에 계정 선택([구글 로그인]/[게스트로 시작])을 세우는가.
         *
         * 아니면 "터치하여 시작" 한 줄이다.
         *
         * 선택은 **한 번만 묻는다.** 첫 설치가 가입 의향의 최고점이라 그때
         * 구글을 앞세우지만, 이미 고른 사람(게스트 포함)에게 매 실행 다시
         * 물으면 그것은 선택이 아니라 잔소리다 - 게스트로 남는 사람의 연동
         * 경로는 설정의 [구글 연동]이 상시로 연다.
         *
         * @param chosenBefore 이 기기에서 이미 한 번 골랐는가 (로컬 기록)
         * @param linked       Firebase가 "이미 구글이 붙어 있다"고 답했는가.
         *                     로컬 기록이 지워져도(앱 데이터 삭제) 세션이
         *                     살아 있으면 이쪽이 참이 된다
         */
        public static bool ShowsAccountChoice(bool chosenBefore, bool linked)
        {
            return !chosenBefore && !linked;
        }

        /**
         * @brief 지금 게임으로 들어가도 되는가. **로딩 게이트의 전부다.**
         *
         * 세이브가 적용되기 전에 오버레이를 내리면 빈 초기 상태가 한 프레임
         * 보인다 - 진행이 날아간 것처럼 읽히는 최악의 첫인상이다.
         *
         * Firebase는 여기 없다. 초기화가 실패해도(오프라인·낡은 Play 서비스)
         * 게임은 로컬로 계속이고, 그 실패를 게이트에 넣으면 비행기 모드가
         * 게임을 막는다.
         *
         * @param saveLoaded GameSession이 세이브 적용을 끝냈는가
         * @param linkBusy   구글 연동 창이 떠 있는 중인가 - 그 동안의 탭은
         *                   진입이 아니라 오조작이다
         */
        public static bool CanEnter(bool saveLoaded, bool linkBusy)
        {
            return saveLoaded && !linkBusy;
        }

        /**
         * @brief 세이브 로드 결과를 타이틀에 적을 한 줄. 정상이면 빈 문자열.
         *
         * 손상을 조용히 새 게임으로 덮지 않는다는 약속(I-6)의 화면 절반이다.
         * 나머지 절반은 SaveSystem이 원본을 백업해 두는 것.
         */
        public static string WarningFor(Onikiri.Progression.SaveLoadOutcome outcome)
        {
            switch (outcome)
            {
                case Onikiri.Progression.SaveLoadOutcome.Corrupt:
                    return "세이브를 읽지 못했습니다 - 원본은 백업해 두었습니다";
                case Onikiri.Progression.SaveLoadOutcome.FutureVersion:
                    return "세이브가 더 새 버전입니다 - 앱을 업데이트하세요";
                default:
                    return string.Empty;
            }
        }
    }
}
