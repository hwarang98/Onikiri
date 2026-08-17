using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 하단 진입 탭에 붙는 빨간 배지. **판을 열지 않아도 보인다.**
     *
     * 리텐션 신호의 전부가 이것이다. 퀘스트는 판을 열어야 보이는데, 판을 열
     * 이유가 판 안에 있으면 아무도 열지 않는다 - 17단계에 스탯 포인트 24점이
     * 쌓인 채 방치됐던 것과 같은 구조다(GrowthPanelTabs 주석).
     *
     * 성장 탭 배지와 같은 규칙을 쓴다: **탭 버튼의 자식**이고, 개수를 숫자로 적는다.
     */
    public sealed class QuestTabBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text label;

        private QuestSystem quests;

        private void Start()
        {
            quests = QuestSystem.Instance;
            if (quests != null) quests.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (quests == null) quests = QuestSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (badge == null) return;

            int count = quests != null ? quests.TotalClaimable : 0;

            bool show = count > 0;
            if (badge.activeSelf != show) badge.SetActive(show);

            if (show && label != null) label.text = count.ToString();
        }
    }
}
