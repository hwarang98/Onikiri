using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 레벨업이 밀려 있을 때 켜지는 빨간 점 (개선안 v2).
     *
     * 레벨업 버튼이 전투 화면을 떠나 캐릭터 패널 헤더로 들어가면서, 버튼이
     * 나타나는 것 자체가 신호이던 규칙(LevelHud - "없다가 나타나는 것이
     * 눈에 걸린다")이 패널을 열어야만 보이는 규칙이 됐다. 그 공백을 점이
     * 메운다:
     *
     *   상단 초상화     초상을 누르면 캐릭터 패널이 열린다(홈) - 신호와
     *                   입구가 같은 자리다(퀘스트 아이콘 배지와 같은 규칙)
     *   하단 캐릭터 탭  다른 화면을 보고 있을 때의 두 번째 입구
     *
     * 숫자를 안 적는 이유: 대기 레벨 수는 헤더의 레벨업 버튼이 이미 적는다
     * ("레벨업 5" - LevelHud). 점은 "있다"만 말하고 개수는 누른 다음
     * 자리가 말한다.
     */
    public sealed class LevelUpNoticeBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;

        private CharacterLevel character;

        private void Start()
        {
            character = CharacterLevel.Instance;
            if (character != null) character.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (character == null) character = CharacterLevel.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (badge == null) return;

            bool show = character != null && character.PendingLevelUps > 0;
            if (badge.activeSelf != show) badge.SetActive(show);
        }
    }
}
