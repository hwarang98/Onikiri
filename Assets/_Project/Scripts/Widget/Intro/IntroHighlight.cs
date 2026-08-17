using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 온보딩이 "여기를 눌러라"라고 말하는 방식. **깜빡이는 것 하나뿐이다.**
     *
     * ## 왜 부품이 따로 있는가
     *
     * 강조가 붙는 곳이 둘이다 - 혈조 줄(SkillButton)과 교체 대상 자리
     * (SkillSlotChip). 둘은 서로 모르는 부품이고 배경 이미지도 각자
     * 들고 있는데, **깜빡임이 서로 다르면 두 단계가 다른 안내로 읽힌다.**
     * 주기와 색을 한 자리에 둔다.
     *
     * ## 코루틴을 안 쓴다. 시간 함수 하나다
     *
     * `Refresh`가 이벤트로만 불리므로 코루틴을 돌리면 강조가 꺼질 때
     * 멈추는 책임이 두 부품에 각각 생긴다. 대신 이 부품이 자기
     * `Update`를 갖되 **강조가 켜져 있을 때만 활성**이다 - 꺼지면
     * `enabled = false`가 되어 매 프레임 도는 코드가 화면에서 사라진다.
     *
     * 한 번에 한 곳만 켜지므로(SkillIntroGuide는 힌트를 하나만 돌려준다)
     * 실제로 도는 Update는 패널 전체에서 **최대 하나**다.
     *
     * ## 색은 금색이다
     *
     * 이 게임에서 금색은 완성·수령의 색이다(UiSkin.Gold, 가이드 카드의
     * readyColor). 온보딩도 "받을 것이 있다"는 말이므로 같은 색을 쓴다 -
     * 새 색을 하나 더 만들면 플레이어가 배워야 할 것이 늘어난다.
     */
    [DisallowMultipleComponent]
    public sealed class IntroHighlight : MonoBehaviour
    {
        [Tooltip("깜빡일 배경. 부품이 이미 들고 있는 것을 그대로 넘긴다")]
        [SerializeField] private Graphic target;

        [Tooltip("가장 밝을 때의 색. 금색 - 이 게임에서 '받을 것이 있다'의 색")]
        [SerializeField] private Color peak = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        [Tooltip("한 번 깜빡이는 데 걸리는 시간(초)")]
        [SerializeField] private float periodSeconds = 1.1f;

        [Tooltip("가장 어두울 때 원래 색이 남는 비율. 0이면 완전히 금색이 된다")]
        [Range(0f, 1f)]
        [SerializeField] private float floorBlend = 0.35f;

        /** 강조가 꺼질 때 되돌릴 색. 켜는 순간의 색을 기억한다 */
        private Color resting;

        private bool lit;

        /**
         * @brief 강조를 켜고 끈다. **부품의 Refresh가 매번 부른다.**
         *
         * 같은 상태로 다시 불러도 값이 없다 - 켜져 있는데 또 켜면 쉬는
         * 색을 지금의 깜빡임 값으로 덮어써, 껐을 때 엉뚱한 색이 남는다.
         */
        public void Set(bool on)
        {
            if (on == lit) return;

            lit = on;

            if (target == null) { enabled = false; return; }

            if (on)
            {
                resting = target.color;
                enabled = true;
                return;
            }

            target.color = resting;
            enabled = false;
        }

        private void OnDisable()
        {
            // 패널이 닫히는 것과 강조가 끝나는 것은 다르다. 여기서 색만
            // 되돌리고 lit은 그대로 둔다 - 다시 열리면 Refresh가 판정을
            // 새로 하고, 그때 Set이 같은 상태를 보면 아무 일도 안 한다
            if (lit && target != null) target.color = resting;
        }

        private void OnEnable()
        {
            if (lit && target != null) resting = target.color;
        }

        private void Update()
        {
            if (!lit || target == null) { enabled = false; return; }

            // 실시간이다. 타격 정지가 timeScale을 0으로 눕히는 순간에
            // 강조만 얼어붙으면 "멈췄다"로 읽힌다 - 가이드 카드의
            // WaitForSecondsRealtime과 같은 이유다
            float period = Mathf.Max(0.05f, periodSeconds);
            float phase = Mathf.Repeat(Time.unscaledTime, period) / period;

            // 0 -> 1 -> 0. 사인이라 양 끝에서 느려진다
            float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float blend = Mathf.Lerp(floorBlend, 1f, wave);

            target.color = Color.Lerp(resting, peak, blend);
        }

        /** 빌더가 쓴다. 배경과 색을 한 번에 적어 둔다 */
        public void Configure(Graphic graphic)
        {
            target = graphic;
            enabled = false;
        }
    }
}
