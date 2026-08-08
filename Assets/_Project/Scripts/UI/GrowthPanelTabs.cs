using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 캐릭터 성장 패널의 최상위 탭. 가르는 기준은 **재화**다.
     *
     * ## 왜 재화로 가르는가
     *
     * 17단계에서 강화 목록을 공격/치명타/생존으로 나눴는데, 스탯 포인트로 사는
     * 증폭 축이 그 탭들 **아래에** 그대로 남아 있었다. 그래서 어느 계열을 골라도
     * 화면 아래쪽에는 포인트 축이 붙어 있었고, 한 화면에 골드와 포인트가 섞였다.
     *
     * 섞이면 "이 버튼을 누르면 무엇이 줄어드는가"를 화면에서 읽을 수 없다. 골드는
     * 가만히 있어도 다시 차지만 포인트는 레벨을 올려야 나오고 되돌릴 수도 없다
     * (StatPointButton 참고). 값싼 재화와 비싼 재화가 같은 모양의 행으로 나란히
     * 서 있으면, 그 차이는 눌러보고 나서야 알게 된다.
     *
     * 그래서 최상위는 재화로 가른다.
     *
     *   강화  = 골드 6축      (계열은 목록 안의 얇은 머리글로만 남는다)
     *   성장  = 포인트 2축 + 남은 포인트
     *   전직  = 잠금 (Lv.30)
     *
     * 탭은 이 한 줄뿐이다. 18단계에는 그 아래 계열 서브탭 줄이 하나 더 있었는데
     * 19단계에 걷어냈다 - 계열 탭은 여섯 축 중 넷을 늘 감췄고, 골드를 어디에
     * 쓸지 고르는 화면에서는 무엇이 있는지가 곧 필요한 정보라서 세 번 눌러
     * 비교하는 값이 한 번 훑어 내리는 값보다 컸다.
     *
     * 남은 기준이 재화 하나라 층위 문제도 없다.
     *
     * ## 배지
     *
     * 성장 탭에는 남은 포인트가 있을 때 숫자 배지가 뜬다. 17단계 플레이에서
     * 24점이 쌓인 채로 방치돼 있었는데, 화면 어디에도 "쓸 것이 있다"는 신호가
     * 없었기 때문이다 - 포인트 축은 다른 탭 뒤에 숨어 있고, 상단 경험치 줄은
     * 레벨업 버튼만 보여준다.
     *
     * 배지는 **탭 줄에** 붙는다. 탭을 열지 않아도 보이는 유일한 자리다.
     */
    public sealed class GrowthPanelTabs : MonoBehaviour
    {
        /** 최상위 탭 하나. 탭 버튼과 그 탭이 켜는 것들을 함께 들고 있다 */
        [Serializable]
        public sealed class Page
        {
            public string displayName;
            public Button tab;
            public TMP_Text tabLabel;
            public Image tabBackground;

            [Tooltip("이 탭이 선택될 때만 켜지는 것들. 지금은 페이지 루트 하나씩이지만, " +
                     "탭에 딸린 고정 줄이 생기면 여기 함께 넣는다")]
            public GameObject[] roots;

            [Tooltip("남은 포인트 배지. 성장 탭에만 있고 나머지는 비워둔다")]
            public GameObject badge;
            public TMP_Text badgeLabel;
        }

        [SerializeField] private Page[] pages;

        [Header("색")]
        [SerializeField] private Color selectedText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unselectedText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color selectedTint = new Color(0.62f, 0.66f, 1.00f, 1f);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        /**
         * @brief 지금 선택된 탭.
         *
         * 세이브에 넣지 않는다. 앱을 껐다 켜면 강화로 돌아가는데, 그것이 잘못된
         * 상태가 아니라 **가장 자주 보는 화면**이기 때문이다 - 골드는 방치 중에도
         * 계속 쌓이므로 켤 때마다 살 것이 있다.
         *
         * 스크롤 위치도 마찬가지로 저장하지 않는다. 강화 목록은 위쪽이 공격이고
         * 그것이 어느 시점에나 살 만하다.
         */
        private int selected;

        private CharacterLevel character;

        private void Start()
        {
            if (pages == null) return;

            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                var page = pages[i];
                if (page.tab != null) page.tab.onClick.AddListener(() => Select(index));
            }

            character = CharacterLevel.Instance;
            if (character != null) character.Changed += RefreshBadges;

            Select(0);
            RefreshBadges();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= RefreshBadges;

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

                if (page.roots != null)
                    foreach (var root in page.roots)
                        if (root != null && root.activeSelf != active) root.SetActive(active);

                if (page.tabLabel != null)
                    page.tabLabel.color = active ? selectedText : unselectedText;

                if (page.tabBackground != null)
                    page.tabBackground.color = active ? selectedTint : unselectedTint;
            }
        }

        /**
         * @brief 남은 포인트 배지를 갱신한다.
         *
         * 배지는 탭 버튼의 자식이라 **페이지가 꺼져 있어도 보인다.** 그것이
         * 배지의 존재 이유다 - 성장 탭을 열지 않은 사람에게 열 이유를 준다.
         */
        private void RefreshBadges()
        {
            if (pages == null) return;

            int points = character != null ? character.UnspentPoints : 0;

            foreach (var page in pages)
            {
                if (page.badge == null) continue;

                bool show = points > 0;
                if (page.badge.activeSelf != show) page.badge.SetActive(show);

                // 점이 아니라 숫자를 적는다. 남은 개수가 곧 "몇 번 누를 수 있는가"라
                // 그 자체가 보상의 크기다. 레벨업 버튼이 대기 레벨 수를 적는 것과
                // 같은 규칙이다(LevelHud)
                if (show && page.badgeLabel != null) page.badgeLabel.text = points.ToString();
            }
        }
    }
}
