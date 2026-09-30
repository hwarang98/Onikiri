using Onikiri.Cloud;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 설정 창 (37단계). 효과음 토글 · **계정 섹션(인트로 스텝)** · 버전.
     *
     * 상단 바 재배치가 설정 진입점을 요구했고, 진입점만 있고 화면이 없으면
     * 죽은 버튼이다. 최소한으로 세운다 - 음소거는 방치형에서 실제로 첫날
     * 찾는 설정이고, 버전은 문의가 왔을 때 물어볼 첫 질문이다.
     *
     * ## 계정의 상시 거처가 여기다 (인트로 스텝)
     *
     * 타이틀은 **진입 순간의 선택**만 맡는다(연동 유저는 그 화면을 다시 안
     * 본다). 게임 도중 "내가 게스트였나", "지금이라도 붙이고 싶다", "이름을
     * 고치고 싶다"가 생기면 그 답은 상시 화면에 있어야 하고, 그 자리가
     * 톱니바퀴다. 랭킹 화면의 계정 줄(55단계)은 그대로다 - 복구의 결과가
     * 순위로 보이는 자리라는 근거가 여전히 유효하다. 연동 흐름 자체는
     * GoogleLinkButton 한 벌이라 두 입구가 갈릴 수 없다.
     *
     * ## 세이브가 아니라 PlayerPrefs다
     *
     * 음소거는 진행이 아니라 기기 취향이다. 세이브에 넣으면 버전 사슬(v12)을
     * 하나 더 태우고, 기기를 옮기면 취향까지 따라간다 - 따라가면 안 되는
     * 값이다. 적용은 AudioListener.volume 하나로 끝난다(효과음뿐인 게임이다).
     */
    public sealed class SettingsPanel : MonoBehaviour
    {
        public const string MutedKey = "onikiri_sfx_muted";

        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text muteLabel;
        [SerializeField] private TMP_Text versionLabel;

        [Header("계정 (인트로 스텝)")]
        [SerializeField] private TMP_Text accountLabel;
        [SerializeField] private GoogleLinkButton googleLink;

        [Header("클라우드 저장 (60단계)")]
        [SerializeField] private TMP_Text cloudStatusLabel;
        [SerializeField] private Button conflictButton;
        [SerializeField] private CloudConflictPanel conflictPanel;

        [Header("이름")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button nameEditButton;
        [SerializeField] private GameObject nameEditGroup;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button nameConfirmButton;
        [SerializeField] private TMP_Text statusLabel;

        private void Start()
        {
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (versionLabel != null)
                versionLabel.text = "버전 " + Application.version;

            if (nameEditButton != null) nameEditButton.onClick.AddListener(BeginNameEdit);
            if (conflictButton != null) conflictButton.onClick.AddListener(OpenConflictPanel);
            if (nameConfirmButton != null) nameConfirmButton.onClick.AddListener(ConfirmName);

            // 키보드의 "완료"로도 확정된다 - 소프트 키보드가 확인 버튼을 덮는
            // 실기 함정(LeaderboardPanel과 같은 사정)
            if (nameInput != null) nameInput.onSubmit.AddListener(OnNameSubmitted);

            if (googleLink != null) googleLink.Finished += OnLinkFinished;
        }

        private void OnEnable()
        {
            AccountLink.Changed += RefreshAccount;
            RefreshAccount();
            RefreshName();
            RefreshMuteLabel();
            RefreshCloudStatus();

            if (nameEditGroup != null) nameEditGroup.SetActive(false);
            SetStatus(string.Empty);
        }

        /**
         * @brief 클라우드 저장 상태 한 줄 (설계 §9의 네 문장).
         *
         * 열려 있는 동안 상태가 변할 수 있어(백그라운드 동기화 완료) 갱신
         * 버튼 대신 매 초 다시 읽는다 - 설정 창은 짧게 열리는 화면이라
         * 폴링 비용이 뜻이 없다.
         *
         * "기록 선택 필요"일 때만 선택 버튼이 산다. 그 화면(CloudConflictPanel)을
         * **안전한 시점에 사람이 여는** 유일한 입구가 이 버튼이다 - 전투 도중
         * 충돌이 발견돼도 화면이 저절로 덮치지 않는다(hot swap 금지).
         */
        private void RefreshCloudStatus()
        {
            if (cloudStatusLabel != null) cloudStatusLabel.text = CloudSaveSync.StatusLine;

            if (conflictButton != null)
                conflictButton.gameObject.SetActive(
                    CloudSaveCoordinator.State == CloudSaveState.Conflict
                    && CloudSaveSync.ConflictServerEnvelope != null);
        }

        private void Update()
        {
            if (Time.frameCount % 60 != 0) return;
            RefreshCloudStatus();
        }

        private void OpenConflictPanel()
        {
            if (conflictPanel == null) return;

            if (!conflictPanel.Show(CloudSaveSync.ConflictServerEnvelope))
                SetStatus("클라우드 기록을 아직 읽지 못했습니다");
        }

        private void OnDisable()
        {
            AccountLink.Changed -= RefreshAccount;
        }

        // ---------------------------------------------------------------- 계정

        /**
         * @brief 계정 상태 한 줄. "지금 내 기록이 어디에 있는가"의 답이다.
         *
         * 게스트에게는 상태만이 아니라 **결과**("앱을 지우면 사라집니다")를
         * 적는다 - 연동 버튼을 누를 이유가 이 줄에서 읽혀야 한다(랭킹 화면과
         * 같은 문장, 같은 이유).
         */
        private void RefreshAccount()
        {
            if (this == null) return;

            if (accountLabel != null)
            {
                switch (AccountLink.State)
                {
                    case AccountState.Linked:
                        string who = AccountLink.LinkedLabel;
                        accountLabel.text = string.IsNullOrEmpty(who)
                            ? "구글 연동됨"
                            : "구글 · " + who;
                        break;

                    case AccountState.Guest:
                        accountLabel.text = "게스트 · 앱을 지우면 사라집니다";
                        break;

                    default:
                        // 확인 중과 게스트는 다른 문장이다(AccountLink.State 주석)
                        accountLabel.text = "계정 확인 중...";
                        break;
                }
            }

            if (!string.IsNullOrEmpty(AccountLink.Status)) SetStatus(AccountLink.Status);
        }

        private void OnLinkFinished(bool changed)
        {
            if (this == null || !isActiveAndEnabled) return;

            RefreshAccount();
            if (!changed) return;

            // 복구가 이름까지 되살렸을 수 있다. Restore는 신호를 안 내므로
            // (복원은 변경이 아니다) 직접 다시 그린다
            RefreshName();
            RefreshCloudStatus();

            // 복구가 세이브 갈라짐을 발견했다(61단계 ⑤). 설정 창은 안전한
            // 시점이고 사람이 방금 복구를 눌렀다 - 버튼을 찾게 두지 않고 충돌
            // 화면을 바로 연다. 전투 화면에서는 여전히 저절로 안 뜬다(hot swap
            // 금지 - 그쪽은 상태 줄과 버튼만 남는다)
            if (CloudSaveCoordinator.State == CloudSaveState.Conflict
                && CloudSaveSync.ConflictServerEnvelope != null)
                OpenConflictPanel();
        }

        // ---------------------------------------------------------------- 이름

        private void RefreshName()
        {
            if (nameLabel != null) nameLabel.text = PlayerProfile.Name;
        }

        private void BeginNameEdit()
        {
            if (nameEditGroup == null) return;

            bool opening = !nameEditGroup.activeSelf;
            nameEditGroup.SetActive(opening);

            if (opening && nameInput != null)
            {
                // 기본 이름은 채우지 않는다 - 지우는 것이 첫 동작이 되면 안 된다
                nameInput.text = PlayerProfile.HasChosenName ? PlayerProfile.Name : string.Empty;
                nameInput.characterLimit = PlayerProfile.MaxLength;
                nameInput.ActivateInputField();
            }
        }

        private void OnNameSubmitted(string _)
        {
            ConfirmName();
        }

        /** 확정만 한다. 서버 제출은 LeaderboardSubmitter가 디바운스로 묶는다 */
        private void ConfirmName()
        {
            if (nameInput == null) return;

            if (!PlayerProfile.SetName(nameInput.text))
            {
                SetStatus("이름을 한 글자 이상 입력하세요");
                return;
            }

            RefreshName();
            if (nameEditGroup != null) nameEditGroup.SetActive(false);
            SetStatus("이름이 바뀌었습니다 - 잠시 뒤 랭킹에 반영됩니다");
        }

        private void SetStatus(string text)
        {
            if (statusLabel == null) return;
            statusLabel.text = text;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // ---------------------------------------------------------------- 소리

        /** 저장된 음소거 상태를 리스너에 건다. 씬 시작 시 SoundPrefsApplier가 부른다 */
        public static void ApplySavedVolume()
        {
            AudioListener.volume = PlayerPrefs.GetInt(MutedKey, 0) == 1 ? 0f : 1f;
        }

        private void ToggleMute()
        {
            bool muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            PlayerPrefs.SetInt(MutedKey, muted ? 0 : 1);
            PlayerPrefs.Save();

            ApplySavedVolume();
            RefreshMuteLabel();
        }

        private void RefreshMuteLabel()
        {
            if (muteLabel == null) return;
            bool muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            muteLabel.text = muted ? "효과음  꺼짐" : "효과음  켜짐";
        }
    }
}
