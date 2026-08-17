using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 장착 슬롯 한 칸. 지금 그 자리에 든 오의를 보여주고, 누르면 뺀다.
     *
     * ## 왜 칩이 "빼는 곳"이고 목록이 "넣는 곳"인가
     *
     * 한쪽 조작에 두 뜻을 주지 않기 위해서다. 칩을 눌러 목록이 열리고 거기서
     * 고르는 방식도 있지만, 그러면 화면 하나에 팝업이 하나 더 생기고 "지금
     * 무엇을 고르는 중인가"라는 상태가 생긴다. 이 게임의 다른 화면에는 그런
     * 상태가 하나도 없다 - 전부 누르면 그 자리에서 일어난다.
     *
     * 칩 = 빼기 / 줄 = 넣기로 나누면 조작이 둘 다 즉발이고, 자리가 다 찼을 때
     * "무엇을 뺄지"를 플레이어가 먼저 정하게 된다. 그것이 슬롯이 만드는 질문
     * 자체이므로 화면이 그 질문을 숨기지 않는 편이 낫다.
     *
     * ## 잠긴 칸
     *
     * 자리는 **기본 오의를 배울 때마다 하나씩** 열리고(Lv.10/15/20) 넷째는
     * 최전선 st51이다(SkillCurve.SlotsFor). 그 전에는 자물쇠와 조건만 적는다 -
     * 41단계의 잠긴 미리보기 규칙과 같다. 감추지 않는 이유도 같다: 열릴 것이
     * 있다는 사실 자체가 진행의 이유다.
     */
    public sealed class SkillSlotChip : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;
        [SerializeField] private int slot;

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        [Header("색")]
        [SerializeField] private Color filledTint = Color.white;
        [SerializeField] private Color emptyTint = new Color(0.34f, 0.36f, 0.55f, 1f);
        [SerializeField] private Color lockedTint = new Color(0.28f, 0.29f, 0.44f, 1f);
        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color iconTint = new Color(0.82f, 0.80f, 0.86f, 1f);

        /** 잠긴 칸에 뜨는 자물쇠. 잠금 표시는 이 게임 전체가 같은 글리프를 쓴다 */
        [SerializeField] private Sprite lockGlyph;

        /**
         * @brief 빈 칸의 그림. **비어 있어도 된다** - 그러면 아이콘이 꺼진다.
         *
         * 자물쇠를 재활용하지 않는다. 잠긴 칸은 못 쓰는 것이고 빈 칸은 지금
         * 쓸 수 있는 자리라 뜻이 정반대인데, 같은 그림을 쓰면 화면이 그 둘을
         * 하나로 말한다.
         */
        [SerializeField] private Sprite emptyGlyph;

        private Sprite[] icons;

        /** 오의별 아이콘. 빌더가 카탈로그 순서로 적어 준다 */
        [SerializeField] private Sprite[] skillIcons;

        /** 온보딩 교체 안내가 이 자리를 가리키는지 물어볼 곳 */
        private Onikiri.Progression.SkillGachaSystem gacha;

        /** 깜빡임. 가리켜질 때 처음 붙는다 */
        private IntroHighlight highlight;

        private void Start()
        {
            icons = skillIcons;
            gacha = Onikiri.Progression.SkillGachaSystem.Instance;
            if (button != null) button.onClick.AddListener(OnClick);
            if (system != null) system.Changed += Refresh;
            if (gacha != null) gacha.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (gacha == null) gacha = Onikiri.Progression.SkillGachaSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (system != null) system.Changed -= Refresh;
            if (gacha != null) gacha.Changed -= Refresh;
        }

        /**
         * @brief 온보딩이 이 자리를 비우라고 가리키는가.
         *
         * 여기가 켜지는 것은 혈조가 **이 자리의 것보다 세진 다음**이다
         * (SkillIntroGuide). 눌러서 비우면 혈조 줄의 장착 버튼이 서고,
         * 끼우는 순간 강조 전체가 영영 꺼진다(introEquipDone).
         *
         * 잠긴 자리·빈 자리에서는 안 부른다. 그쪽은 Refresh가 먼저
         * 돌아가는데, 비어 있으면 애초에 교체할 것이 없으므로 조언도
         * Swap이 아니다.
         */
        private void LightIntro()
        {
            bool on = false;

            if (gacha != null)
            {
                var advice = Onikiri.Progression.SkillIntroGuide.Resolve(
                    system, gacha.IntroClaimed, gacha.IntroEquipDone);
                on = advice.Hint == Onikiri.Progression.SkillIntroHint.Swap
                     && advice.SlotIndex == slot;
            }

            if (highlight == null)
            {
                if (!on || background == null) return;
                highlight = gameObject.AddComponent<IntroHighlight>();
                highlight.Configure(background);
            }

            highlight.Set(on);
        }

        private void OnClick()
        {
            if (system == null) return;
            system.Equip(slot, -1);
        }

        private void Refresh()
        {
            if (system == null) return;

            if (system.IsSlotLocked(slot))
            {
                if (background != null) background.color = lockedTint;
                if (icon != null) { icon.sprite = lockGlyph; icon.color = dimColor; icon.enabled = true; }
                // 자리마다 조건이 다르다(49b) - 앞의 셋은 기본 오의의 레벨,
                // 넷째는 스테이지. 상수를 여기 적으면 곡선과 갈린다
                if (label != null) { label.text = SkillCurve.GateTextFor(slot); label.color = dimColor; }
                if (button != null) button.interactable = false;
                LightIntro();
                return;
            }

            int index = system.EquippedAt(slot);

            if (index < 0)
            {
                if (background != null) background.color = emptyTint;
                if (icon != null)
                {
                    icon.sprite = emptyGlyph;
                    icon.color = dimColor;
                    icon.enabled = emptyGlyph != null;
                }
                if (label != null) { label.text = "비어 있음"; label.color = dimColor; }
                if (button != null) button.interactable = false;

                // 방금 비운 자리가 여기일 수 있다. 그때 조언은 Swap이 아니라
                // Equip으로 넘어가 있으므로 이 호출이 강조를 **끈다**
                LightIntro();
                return;
            }

            var equipped = system.GetSlot(index);

            if (background != null) background.color = filledTint;
            if (icon != null)
            {
                icon.sprite = icons != null && index < icons.Length ? icons[index] : null;
                icon.color = iconTint;
                icon.enabled = icon.sprite != null;
            }
            if (label != null)
            {
                label.text = equipped != null ? equipped.displayName : string.Empty;
                label.color = textColor;
            }
            if (button != null) button.interactable = true;

            // 자리 색을 정한 다음이다 - 강조는 쉬는 색 위에 얹힌다
            LightIntro();
        }
    }
}
