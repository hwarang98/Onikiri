using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 퀘스트 판의 서브탭 (일일 / 반복 / 업적).
     *
     * GrowthPanelTabs와 같은 모양이지만 별개 컴포넌트다. 합칠 수도 있었지만
     * 그쪽은 **재화로 가른다**는 규칙이 주석의 절반이고, 이쪽은 **주기로 가른다** -
     * 매일 / 영원히 / 한 번. 같은 클래스에 두 규칙을 담으면 다음 사람이 어느
     * 쪽을 따라야 하는지 알 수 없다.
     *
     * ## 탭마다 배지가 붙는다
     *
     * 성장 탭은 배지가 하나였다(남은 포인트). 여기는 셋 다 붙는다 - 일일만
     * 받을 것이 있고 업적은 없는 상태가 흔하고, 그때 어느 탭을 열지가 곧
     * 필요한 정보다.
     */
    public sealed class QuestPanelTabs : MonoBehaviour
    {
        [Serializable]
        public sealed class Page
        {
            public QuestKind kind;
            public Button tab;
            public TMP_Text tabLabel;
            public Image tabBackground;
            public GameObject root;

            [Tooltip("이 탭에 받을 것이 있을 때 뜨는 배지")]
            public GameObject badge;
            public TMP_Text badgeLabel;
        }

        [SerializeField] private Page[] pages;

        [Header("색")]
        [SerializeField] private Color selectedText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unselectedText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color selectedTint = new Color(0.62f, 0.66f, 1.00f, 1f);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        private int selected;
        private QuestSystem quests;

        private void Start()
        {
            if (pages == null) return;

            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var page = pages[i];
                if (page.tab != null) page.tab.onClick.AddListener(() => Select(index));
            }

            quests = QuestSystem.Instance;
            if (quests != null) quests.Changed += RefreshBadges;

            Select(0);
            RefreshBadges();
        }

        /** 판이 꺼진 채로 저장되므로 켜질 때 다시 그린다 (QuestRow와 같은 처리) */
        private void OnEnable()
        {
            if (quests == null) quests = QuestSystem.Instance;
            RefreshBadges();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.Changed -= RefreshBadges;

            if (pages == null) return;
            foreach (var page in pages)
                if (page.tab != null) page.tab.onClick.RemoveAllListeners();
        }

        public void Select(int index)
        {
            if (pages == null || pages.Length == 0) return;

            selected = Mathf.Clamp(index, 0, pages.Length - 1);

            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                bool active = i == selected;

                if (page.root != null && page.root.activeSelf != active)
                    page.root.SetActive(active);

                if (page.tabLabel != null)
                    page.tabLabel.color = active ? selectedText : unselectedText;

                if (page.tabBackground != null)
                    page.tabBackground.color = active ? selectedTint : unselectedTint;
            }
        }

        private void RefreshBadges()
        {
            if (pages == null || quests == null) return;

            foreach (var page in pages)
            {
                if (page.badge == null) continue;

                int count = 0;
                var specs = QuestCatalog.Of(page.kind);
                for (int i = 0; i < specs.Length; i++)
                    count += quests.ClaimableCount(page.kind, i);

                bool show = count > 0;
                if (page.badge.activeSelf != show) page.badge.SetActive(show);

                // 점이 아니라 숫자다. 성장 탭 배지와 같은 규칙 - 개수가 곧
                // "몇 번 누를 수 있는가"이고 그것이 보상의 크기다
                if (show && page.badgeLabel != null) page.badgeLabel.text = count.ToString();
            }
        }
    }
}
