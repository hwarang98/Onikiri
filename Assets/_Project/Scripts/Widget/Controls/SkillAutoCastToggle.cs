using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 스킬 패널 머리글의 자동 시전 표시 겸 토글.
     *
     * ## 왜 토글이 있는가 - 끄고 싶어서가 아니다
     *
     * 자동 시전이 기본이고 대부분은 끄지 않는다. 그런데도 스위치를 두는 이유는
     * **자동이라는 사실을 화면에 적을 자리**가 필요하기 때문이다. 오의가 알아서
     * 나가는 게임에서 스킬 목록만 보여주면, 처음 여는 사람은 "어떻게 쓰지"를
     * 먼저 묻는다. "자동 시전 ON"이라는 한 줄이 그 질문을 없앤다.
     *
     * 눌러서 끌 수 있게 한 것은 그 한 줄이 **주장이 아니라 상태**여야 하기
     * 때문이다. 끌 수 없는 라벨은 장식이고, 장식은 언제부턴가 거짓말이 돼도
     * 아무도 모른다 - 눌리면 그 자리에서 검증된다.
     *
     * 수동 탭 보너스가 붙는 단계에서 이 자리는 "자동 / 수동" 선택이 된다.
     */
    public sealed class SkillAutoCastToggle : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [SerializeField] private string onText = "자동 시전  ON";
        [SerializeField] private string offText = "자동 시전  OFF";

        [SerializeField] private Color onColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color offColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        private void Start()
        {
            if (button != null) button.onClick.AddListener(Toggle);
            if (system != null) system.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
            if (system != null) system.Changed -= Refresh;
        }

        private void Toggle()
        {
            if (system == null) return;
            system.AutoCast = !system.AutoCast;
        }

        private void Refresh()
        {
            if (label == null) return;

            bool on = system != null && system.AutoCast;
            label.text = on ? onText : offText;
            label.color = on ? onColor : offColor;
        }
    }
}
