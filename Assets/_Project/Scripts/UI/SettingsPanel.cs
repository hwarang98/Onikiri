using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 설정 창 (37단계). 지금은 효과음 토글과 버전 표시뿐이다.
     *
     * 상단 바 재배치가 설정 진입점을 요구했고, 진입점만 있고 화면이 없으면
     * 죽은 버튼이다. 최소한으로 세운다 - 음소거는 방치형에서 실제로 첫날
     * 찾는 설정이고, 버전은 문의가 왔을 때 물어볼 첫 질문이다.
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

        private void Start()
        {
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (versionLabel != null)
                versionLabel.text = "버전 " + Application.version;
        }

        private void OnEnable()
        {
            RefreshMuteLabel();
        }

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
