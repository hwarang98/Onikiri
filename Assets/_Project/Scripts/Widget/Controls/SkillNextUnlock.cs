using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 머리글의 한 줄 예고. **다음에 무엇이 언제 열리는가.**
     *
     * ## 왜 이 줄이 필요해졌는가
     *
     * 49b가 신규 오의 셋을 코리더 안으로 앞당기면서(st12/18/27) 해금이 여섯
     * 번 일어나게 됐다 - 기본 셋(st8/15/21)과 번갈아 3~6스테이지마다 하나다.
     *
     * 그 여섯 번이 **사건이 되려면 예고가 있어야 한다.** 없으면 화면은 매번
     * "갑자기 목록이 하나 늘었다"만 말하고, 플레이어는 늘어난 것을 알아채지도
     * 못한 채 지나간다. 41단계가 잠긴 탭을 감추지 않고 조건을 적기로 한 것과
     * 같은 판단이고, 이유도 같다: **열릴 것이 있다는 사실 자체가 진행의 이유다.**
     *
     * 가장 가까운 것 하나만 적는다(SkillSystem.TryNextUnlock). 목록을 다 적으면
     * 그것은 예고가 아니라 로드맵이고, 로드맵은 이 화면이 할 일이 아니다.
     *
     * 다 열리면 줄을 **감춘다.** "더 없음"을 적어 두면 그 자리가 영원히
     * 아무것도 안 말하면서 자리만 차지한다.
     */
    public sealed class SkillNextUnlock : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;
        [SerializeField] private TMP_Text label;

        private CharacterLevel character;

        private void Start()
        {
            character = CharacterLevel.Instance;

            if (system != null) system.Changed += Refresh;

            // 해금은 레벨업으로도 일어난다. SkillSystem의 Changed가 그것을
            // 이미 중계하지만, 이 줄은 시스템이 없는 씬에서도 조용히 꺼져야 한다
            if (character != null) character.Changed += Refresh;

            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (system != null) system.Changed -= Refresh;
            if (character != null) character.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (label == null) return;

            string name, gate;
            if (system == null || !system.TryNextUnlock(out name, out gate))
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.gameObject.SetActive(true);
            label.text = "다음  " + name + "  " + gate;
        }
    }
}
