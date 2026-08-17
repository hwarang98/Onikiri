using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 대장간의 서브탭 (장비 / 요도 / 도감).
     *
     * QuestPanelTabs와 같은 모양이지만 별개 컴포넌트다. 저쪽은 **주기로**
     * 가르고(매일/영원히/한 번), 여기는 **무엇을 벼리는가**로 가른다 -
     * 사람이 만든 것 / 요괴에게서 뺏은 것 / 모은 것. 같은 클래스에 두 규칙을
     * 담으면 다음 사람이 어느 쪽을 따라야 하는지 알 수 없다(QuestPanelTabs가
     * GrowthPanelTabs와 갈라진 것과 같은 판단이다).
     *
     * ## 배지의 출처가 탭마다 다르다
     *
     * 장비는 EquipmentSystem이 "지금 살 수 있는 버튼 수"를 세고, 요도는
     * YodoSystem이 "지금 벼릴 수 있는 자루 수"를 센다. 도감은 배지가 없다 -
     * 도감에는 누를 것이 없고, 배지는 "가서 할 일이 있다"는 뜻이기 때문이다
     * (EquipmentSystem.AnyAffordable 주석의 기준 그대로).
     */
    public sealed class ForgePanelTabs : MonoBehaviour
    {
        /** 배지가 무엇을 세는가. 페이지가 늘면 여기 한 줄이 는다 */
        public enum Source
        {
            /** 배지 없음 (도감) */
            None,

            /** 살 수 있는 장비 버튼 수 */
            Equipment,

            /** 벼릴 수 있는 요도 수 */
            Yodo
        }

        [Serializable]
        public sealed class Page
        {
            public Source badgeSource;
            public Button tab;
            public TMP_Text tabLabel;
            public Image tabBackground;
            public GameObject root;

            public GameObject badge;
            public TMP_Text badgeLabel;
        }

        [SerializeField] private Page[] pages;

        /**
         * @brief 탭을 바꿀 때 스크롤 길이를 그 페이지에 맞춘다.
         *
         * ## 왜 퀘스트 판과 다르게 하는가
         *
         * 퀘스트 판은 Content 높이를 **가장 긴 페이지**에 고정한다. 세 페이지의
         * 길이가 1764 / 1000 언저리로 비슷해서 남는 슬랙이 눈에 안 띄기 때문이다.
         *
         * 대장간은 다르다 - 요도 페이지가 870px인데 도감은 710px이라, 도감을
         * 끝까지 내리면 **마지막 줄 아래로 160px의 빈 판**이 스크롤된다(실기
         * 캡처에서 보였다). 목록의 끝이 화면의 끝이 아니면 "더 있나?" 하고
         * 끌어보게 되고, 그것이 32단계에 스크롤을 피했던 이유 그대로다.
         *
         * 겸사겸사 탭을 바꿀 때 맨 위로 돌아간다. 요도 탭을 바닥까지 내려둔
         * 채 도감으로 넘어가면 도감이 중간부터 보이는데, 그것은 "다른 화면"이
         * 아니라 "같은 화면이 잘린 것"으로 읽힌다.
         */
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private float[] pageHeights;

        [Header("색")]
        [SerializeField] private Color selectedText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unselectedText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color selectedTint = new Color(0.62f, 0.66f, 1.00f, 1f);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        private int selected;
        private EquipmentSystem equipment;
        private YodoSystem yodo;

        private void Start()
        {
            if (pages == null) return;

            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var page = pages[i];
                if (page.tab != null) page.tab.onClick.AddListener(() => Select(index));
            }

            Bind();
            Select(0);
            RefreshBadges();
        }

        /** 판이 꺼진 채로 저장되므로 켜질 때 다시 그린다 (QuestPanelTabs와 같은 처리) */
        private void OnEnable()
        {
            Bind();
            RefreshBadges();
        }

        private void Bind()
        {
            if (equipment == null)
            {
                equipment = EquipmentSystem.Instance;
                if (equipment != null) equipment.Changed += RefreshBadges;
            }
            if (yodo == null)
            {
                yodo = YodoSystem.Instance;
                if (yodo != null) yodo.Changed += RefreshBadges;
            }
        }

        private void OnDestroy()
        {
            if (equipment != null) equipment.Changed -= RefreshBadges;
            if (yodo != null) yodo.Changed -= RefreshBadges;

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

            FitScroll();
        }

        private void FitScroll()
        {
            if (scroll == null || scroll.content == null) return;

            if (pageHeights != null && selected < pageHeights.Length && pageHeights[selected] > 0f)
            {
                var size = scroll.content.sizeDelta;
                size.y = pageHeights[selected];
                scroll.content.sizeDelta = size;
            }

            // 1이 맨 위다. 페이지가 뷰포트보다 짧으면 이 값은 무시된다
            scroll.verticalNormalizedPosition = 1f;
        }

        /** 지금 열려 있는 페이지. 캡처 리그가 탭을 바꿔 가며 찍을 때 쓴다 */
        public int Selected { get { return selected; } }

        private void RefreshBadges()
        {
            if (pages == null) return;

            foreach (var page in pages)
            {
                if (page.badge == null) continue;

                int count = 0;
                switch (page.badgeSource)
                {
                    case Source.Equipment:
                        count = equipment != null ? equipment.AffordableCount : 0;
                        break;
                    case Source.Yodo:
                        count = yodo != null ? yodo.ForgeableCount : 0;
                        break;
                }

                bool show = count > 0;
                if (page.badge.activeSelf != show) page.badge.SetActive(show);

                // 점이 아니라 숫자다. 성장·퀘스트 탭 배지와 같은 규칙
                if (show && page.badgeLabel != null) page.badgeLabel.text = count.ToString();
            }
        }
    }
}
