using System;
using Onikiri.Battle;
using Onikiri.Cloud;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 부팅 화면: 스플래시(202 STUDIO -> ONIKIRI) -> 타이틀 -> 게임.
     *
     * 이 오버레이는 씬에 **켜진 채 저장되는 유일한 전면 UI다**. 첫 프레임부터
     * 게임을 가리고 있어야 하기 때문이다 - 뒤에서는 GameSession이 세이브를
     * 적용하고 Firebase가 조용히 깨어난다(둘 다 이 컴포넌트가 시작하지 않고,
     * 세이브는 GameSession.Start가 원래 하던 그대로다. 이중 초기화 없음).
     *
     * ## 로딩 게이트
     *
     * 게임 진입(오버레이 내리기)은 세이브 적용이 끝난 뒤에만 된다
     * (IntroPolicy.CanEnter). Firebase는 게이트에 **없다** - 실패해도 게임은
     * 익명/로컬로 계속이고, 그것이 오프라인 안전이다.
     *
     * ## 타이틀의 두 얼굴
     *
     *   처음 온 사람      [구글 로그인] / [게스트로 시작] - 계정 선택
     *                     (슬레이어 키우기 등 한국 방치형의 표준 진입)
     *   이미 고른 사람    "터치하여 시작" 한 줄. 매 실행 로그인을 묻지 않는다
     *
     * 갈래는 IntroPolicy.ShowsAccountChoice가 정하고, "골랐다"는 사실은
     * 세이브가 아니라 PlayerPrefs다 - 기기 취향이지 진행이 아니다
     * (SettingsPanel의 음소거와 같은 결).
     */
    public sealed class IntroFlow : MonoBehaviour
    {
        /** "계정 선택을 이미 했다"의 로컬 기록. 값은 1 하나뿐이다 */
        public const string AccountChosenKey = "onikiri_account_chosen";

        [SerializeField] private GameSession session;

        [Header("단계별 묶음")]
        [SerializeField] private GameObject studioGroup;
        [SerializeField] private GameObject brandGroup;
        [SerializeField] private GameObject titleGroup;

        [Header("타이틀 CTA")]
        [SerializeField] private GameObject accountGroup;
        [SerializeField] private GameObject touchGroup;
        [SerializeField] private TMP_Text touchLabel;
        [SerializeField] private GoogleLinkButton googleLink;
        [SerializeField] private Button guestButton;

        [Header("상태줄")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text warnLabel;
        [SerializeField] private TMP_Text versionLabel;

        [Header("탭 받이 (오버레이 전체)")]
        [SerializeField] private Button screenButton;

        private IntroPhase phase = IntroPhase.Studio;
        private float elapsedInPhase;

        /** 지금 타이틀이 계정 선택 모습인가 (아니면 "터치하여 시작") */
        private bool accountChoiceShown;

        /** 인트로가 게임을 세워 둔 상태인가. 정지/재개가 짝을 이루게 하는 걸쇠 */
        private bool gamePaused;

        /** 재개할 때 돌아갈 배속. 정지 직전 값을 기억한다 (HitStop과 같은 규칙) */
        private float pausedTimeScale = 1f;

        /** 구글 로그인이 성공했는데 세이브가 아직이라면, 게이트가 열리는 즉시 들어간다 */
        private bool enterWhenLoaded;

        private void Awake()
        {
            // 무엇보다 먼저다. 세이브 로드(GameSession.Start)가 같은 프레임에
            // 돌고, 그 안의 방치 보상이 이 값을 보고 뜰지 기다릴지 정한다
            MarkNotEntered();

            if (session == null) session = FindFirstObjectByType<GameSession>();

            // 형제 순서가 곧 그리기 순서다. 다른 빌더가 SafeArea 뒤에 무엇을
            // 더 세우든 부팅 화면은 맨 위여야 한다
            transform.SetAsLastSibling();

            if (screenButton != null) screenButton.onClick.AddListener(OnScreenTapped);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestPressed);
            if (googleLink != null) googleLink.Finished += OnGoogleFinished;

            if (versionLabel != null)
                versionLabel.text = "버전 " + Application.version + "  ·  202 STUDIO";

            // **인트로 화면은 플레이가 아니다.** 오버레이가 화면만 가리면 뒤에서
            // 전투가 그대로 돌고, 보이지 않는 타격음이 스플래시를 뚫고 나온다
            // (실기에서 그렇게 들렸다). 여기(Awake)인 이유는 첫 Update가 돌기
            // 전이어야 전투가 한 프레임도 안 밟기 때문이고, HitStop(-100)의
            // 시작 리셋이 이미 지난 뒤라 그 경고와도 안 겹친다.
            PauseGame();
        }

        private void Start()
        {
            // Firebase를 스플래시 뒤에서 조용히 깨운다. 결과는 기다리지 않고
            // 실패는 CloudScores 안에서 죽는다 - 여기 걸리는 게이트가 아니다.
            // 초기화·익명 로그인 모두 1회 캐시라(initTask / CurrentUser 재사용)
            // 나중에 랭킹·연동이 다시 불러도 이중 초기화가 없다
            var _ = WarmUpAsync();

            ApplyPhase();
        }

        private void OnEnable()
        {
            AccountLink.Changed += OnAccountChanged;
        }

        private void OnDisable()
        {
            AccountLink.Changed -= OnAccountChanged;
        }

        private static async System.Threading.Tasks.Task WarmUpAsync()
        {
            if (!await CloudScores.InitializeAsync()) return;
            await CloudScores.SignInAnonymouslyAsync();
        }

        // ---------------------------------------------------------------- 단계
        private void Update()
        {
            elapsedInPhase += Time.unscaledDeltaTime;

            var next = IntroPolicy.Next(phase, false, elapsedInPhase);
            if (next != phase) SetPhase(next);

            if (phase == IntroPhase.Title) RefreshTitle();
        }

        private void OnScreenTapped()
        {
            if (phase == IntroPhase.Studio || phase == IntroPhase.Brand)
            {
                SetPhase(IntroPolicy.Next(phase, true, elapsedInPhase));
                return;
            }

            // 계정 선택이 떠 있으면 화면 탭은 진입이 아니다 - 선택은 버튼으로만
            if (phase == IntroPhase.Title && !accountChoiceShown) TryEnter();
        }

        private void SetPhase(IntroPhase next)
        {
            phase = next;
            elapsedInPhase = 0f;
            ApplyPhase();
        }

        private void ApplyPhase()
        {
            if (studioGroup != null) studioGroup.SetActive(phase == IntroPhase.Studio);
            if (brandGroup != null)
                brandGroup.SetActive(phase == IntroPhase.Brand || phase == IntroPhase.Title);
            if (titleGroup != null) titleGroup.SetActive(phase == IntroPhase.Title);

            if (phase != IntroPhase.Title) return;

            bool chosenBefore = PlayerPrefs.GetInt(AccountChosenKey, 0) == 1;
            accountChoiceShown = IntroPolicy.ShowsAccountChoice(
                chosenBefore, AccountLink.State == AccountState.Linked);

            if (accountGroup != null) accountGroup.SetActive(accountChoiceShown);
            if (touchGroup != null) touchGroup.SetActive(!accountChoiceShown);
            if (googleLink != null) googleLink.Refresh();

            if (warnLabel != null)
            {
                string warning = IntroPolicy.WarningFor(SaveSystem.LastOutcome);
                warnLabel.text = warning;
                warnLabel.gameObject.SetActive(warning.Length > 0);
            }

            RefreshTitle();
        }

        /** 게이트·상태줄·깜빡임. 타이틀이 떠 있는 동안 매 프레임 */
        private void RefreshTitle()
        {
            bool loaded = session == null || session.IsLoaded;
            bool canEnter = IntroPolicy.CanEnter(loaded, AccountLink.IsBusy);

            if (enterWhenLoaded && canEnter)
            {
                Enter();
                return;
            }

            if (guestButton != null) guestButton.interactable = canEnter;

            if (touchLabel != null)
            {
                // 게이트가 닫혀 있으면 깜빡임을 멈추고 반투명으로 - "아직"이 읽힌다
                var color = touchLabel.color;
                color.a = canEnter
                    ? 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f)
                    : 0.25f;
                touchLabel.color = color;
            }

            if (statusLabel == null) return;

            if (!loaded) SetStatus("불러오는 중...");
            else if (AccountLink.IsBusy || !string.IsNullOrEmpty(AccountLink.Status))
                SetStatus(AccountLink.Status);
            else SetStatus(string.Empty);
        }

        private void SetStatus(string text)
        {
            if (statusLabel.text == text) return;
            statusLabel.text = text;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // ---------------------------------------------------------------- 진입
        private void OnGuestPressed()
        {
            MarkChosen();
            TryEnter();
        }

        private void OnGoogleFinished(bool changed)
        {
            if (!changed) return;

            // 연동이든 복구든 정체성이 정해졌다. 게이트가 열려 있으면 바로,
            // 아니면 열리는 순간 들어간다 - 로그인까지 한 사람에게 탭을 한 번
            // 더 시키는 것은 마찰이다
            MarkChosen();
            enterWhenLoaded = true;
        }

        /** Firebase가 늦게 "이미 연동됨"을 알려온 경우 - 선택을 물을 이유가 사라졌다 */
        private void OnAccountChanged()
        {
            if (this == null || phase != IntroPhase.Title) return;
            if (!accountChoiceShown) return;
            if (AccountLink.State != AccountState.Linked) return;

            MarkChosen();
            ApplyPhase();
        }

        private static void MarkChosen()
        {
            PlayerPrefs.SetInt(AccountChosenKey, 1);
            PlayerPrefs.Save();
        }

        private void TryEnter()
        {
            bool loaded = session == null || session.IsLoaded;
            if (!IntroPolicy.CanEnter(loaded, AccountLink.IsBusy)) return;
            Enter();
        }

        /**
         * @brief **게임에 들어왔는가.** 부팅 오버레이 뒤에서 기다리는 것들이 읽는다.
         *
         * 방치 보상 팝업이 첫 손님이다(OfflineRewardPopup.Show). 세이브를 읽는
         * 순간 지급과 함께 뜨는데, 그 순간은 아직 **타이틀 화면**이다 - 게임을
         * 시작하기도 전에 "2시간 방치 +147K 골드"가 로고 위에 떴다.
         *
         * static인 이유는 기다리는 쪽이 오버레이를 찾아 헤매지 않게 하기
         * 위해서다. 인트로는 씬에 하나뿐이고 꺼진 뒤에는 사라지므로
         * (gameObject.SetActive(false)) 참조를 들고 있어도 못 쓴다.
         *
         * 인트로가 없는 씬(전투 전용 테스트)에서는 **처음부터 참**이다 -
         * 오버레이가 없다는 것은 이미 게임 안이라는 뜻이고, 거짓으로 두면
         * 그런 씬에서 팝업이 영영 안 뜬다.
         */
        public static bool HasEntered { get; private set; }

        /** 방금 들어왔다. 기다리던 것들이 이때 자기 일을 한다 */
        public static event Action Entered;

        static IntroFlow()
        {
            HasEntered = true;
        }

        /**
         * @brief 오버레이가 살아 있는 동안은 "아직 안 들어옴"이다.
         *
         * Awake에서 내린다. 이 컴포넌트가 존재한다는 것 자체가 부팅 게이트가
         * 있다는 뜻이고, 그 판단은 다른 어떤 것보다 먼저여야 한다 - 세이브
         * 로드(GameSession)가 같은 프레임에 돌기 때문이다.
         */
        private void MarkNotEntered()
        {
            HasEntered = false;
        }

        private void Enter()
        {
            if (phase == IntroPhase.Entered) return;
            phase = IntroPhase.Entered;
            enterWhenLoaded = false;
            ResumeGame();
            gameObject.SetActive(false);

            HasEntered = true;

            var handler = Entered;
            if (handler != null) handler();
        }

        // ---------------------------------------------------------------- 정지/재개

        /**
         * @brief 인트로 동안 게임을 통째로 세운다. 소리까지.
         *
         * timeScale은 직접 쓰지 않고 HitStop을 지난다 - 직접 쓰면 히트스톱이
         * 끝날 때 그 값이 지워진다(HitStop.SetBaseTimeScale 주석). 소리는
         * 따로 멈춘다(AudioListener.pause) - 정지 직전 프레임에 이미 발사된
         * 소리는 timeScale과 무관하게 끝까지 울리기 때문이다.
         *
         * 정지해도 계속 도는 것들은 전부 unscaled라 의도대로다: 인트로 자신,
         * 자동 저장(GameSession), 제출 디바운스(LeaderboardSubmitter).
         */
        private void PauseGame()
        {
            if (gamePaused) return;
            gamePaused = true;

            pausedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            HitStop.RequestBaseTimeScale(0f);
            AudioListener.pause = true;
        }

        private void ResumeGame()
        {
            if (!gamePaused) return;
            gamePaused = false;

            AudioListener.pause = false;
            HitStop.RequestBaseTimeScale(pausedTimeScale);
        }

        /**
         * @brief 안전망. 인트로가 선 채로 파괴되면(씬 교체·에디터 정지) 게임이
         * 멈춘 채 남으면 안 된다 - HitStop.OnDestroy와 같은 결이다.
         */
        private void OnDestroy()
        {
            ResumeGame();
        }

        // ---------------------------------------------------------------- 도구
        /** 테스트 패널의 "다시 보기". 실제 부팅과 같은 경로를 처음부터 태운다 */
        public void Replay()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            enterWhenLoaded = false;
            PauseGame();
            SetPhase(IntroPhase.Studio);
        }

        /** 캡처 리그·자동화용. 게이트만 지키고 곧장 게임으로 */
        public void SkipToGame()
        {
            if (phase == IntroPhase.Entered) return;
            bool loaded = session == null || session.IsLoaded;
            if (loaded) Enter();
            else { SetPhase(IntroPhase.Title); enterWhenLoaded = true; }
        }
    }
}
