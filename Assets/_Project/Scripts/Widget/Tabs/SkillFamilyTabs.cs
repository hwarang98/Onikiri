using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 스킬 목록의 계열 탭 (검식 / 혈식 / 귀오의).
     *
     * ## 왜 ForgePanelTabs를 그대로 못 쓰는가
     *
     * 저쪽은 탭마다 **자기 페이지 루트**가 있어서 루트 하나를 켜고 끄면 끝난다.
     * 이쪽은 그런 루트가 없다 - 열다섯 줄이 하나의 Content 안에 나란히 서고,
     * 계열은 각 줄이 **자기 데이터로** 들고 있다(SkillCatalog.Skills[i].Family).
     * 줄을 계열별 부모로 묶는 안도 있었지만 그러면 SkillButton이 참조를 찾는
     * 경로가 한 겹 깊어지고, 빌더가 세 부모의 자리 계산을 따로 해야 한다.
     *
     * 그래서 이 컴포넌트는 **줄 배열 하나**만 들고 계열은 카탈로그에 묻는다.
     * 표가 바뀌어도 여기는 안 바뀐다 - 신규 오의 일곱이 들어와도 빌더가 배열을
     * 다시 채우면 그만이고, 이 파일은 한 줄도 안 고친다.
     *
     * ## 줄의 자리는 빌더가 정한다
     *
     * 각 줄의 anchoredPosition은 **계열 안에서 몇 번째인가**로 이미 굽혀 있다
     * (SkillPanelBuilder.BuildRow). 런타임에 자리를 다시 잡지 않는 이유는 그
     * 계산이 두 곳에 살면 반드시 갈리기 때문이고, 무엇보다 **자리는 상수**다 -
     * 계열도 표 순서도 런타임에 안 바뀐다.
     *
     * 그래서 여기가 하는 일은 정확히 셋이다: 켜고 끄기, 탭 색, 스크롤 길이.
     */
    public sealed class SkillFamilyTabs : MonoBehaviour
    {
        [Serializable]
        public sealed class Tab
        {
            public Button button;

            /** 주 표기 - "검식". SkillCatalog.FamilyNames에서 온다 */
            public TMP_Text nameLabel;

            /** 작은 부제 - "일반". 뽑기 희귀도의 낱말과 겹치는 것은 이 하나뿐이다 */
            public TMP_Text subtitleLabel;

            public Image background;
        }

        [SerializeField] private Tab[] tabs;

        /**
         * @brief 목록의 줄들. **카탈로그 인덱스 순서 그대로다.**
         *
         * 계열을 여기 같이 담지 않는 것이 요점이다 - 담으면 카탈로그와 씬에
         * 같은 사실이 두 벌 살고, 표를 고치는 날 씬을 다시 굽지 않으면 두 벌이
         * 갈린다. 그 갈림은 "탭을 눌렀는데 엉뚱한 오의가 뜬다"로만 나타난다.
         */
        [SerializeField] private GameObject[] rows;

        [SerializeField] private ScrollRect scroll;

        /** 한 줄이 차지하는 세로 (행 높이 + 간격). 스크롤 길이 계산에 쓴다 */
        [SerializeField] private float rowPitch = 158f;

        [Header("계열 색")]
        /**
         * @brief 선택된 탭의 바탕 틴트. **계열 색이고 SlashRgba에서 왔다.**
         *
         * 검식 #A8D8FF · 혈식 #C8304C · 귀오의 #FF9500 - 각각 연참·혈륜·귀참의
         * 참격 색이다. 새 색을 정의하지 않은 이유는 39단계의 팔레트 단일 출처
         * 규칙이고, 여기서는 한 겹 더 있다: **탭 색과 그 계열 오의가 화면에서
         * 뿌리는 참격 색이 같아야** 탭이 "무엇의 묶음인지"를 말한다.
         */
        [SerializeField] private Color[] familyTints;

        [SerializeField] private Color selectedText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unselectedText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        private int selected;

        /** 지금 열려 있는 계열. 캡처 리그가 탭을 바꿔 가며 찍을 때 쓴다 */
        public int Selected { get { return selected; } }

        public int TabCount { get { return tabs != null ? tabs.Length : 0; } }

        private void Start()
        {
            if (tabs == null) return;

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Select(index));
            }

            Select(0);
        }

        /**
         * @brief 판이 꺼진 채로 저장되므로 켜질 때 다시 그린다.
         *
         * ForgePanelTabs와 같은 처리이고 같은 이유다. 다만 여기서는 한 가지가
         * 더 걸려 있다 - 줄의 활성 상태는 **씬에 저장되는 값**이라, 다시 안
         * 그리면 마지막으로 구운 탭의 줄만 남은 채로 판이 열린다.
         */
        private void OnEnable()
        {
            Apply();
        }

        private void OnDestroy()
        {
            if (tabs == null) return;
            foreach (var tab in tabs)
                if (tab.button != null) tab.button.onClick.RemoveAllListeners();
        }

        public void Select(int familyIndex)
        {
            if (tabs == null || tabs.Length == 0) return;

            selected = Mathf.Clamp(familyIndex, 0, tabs.Length - 1);
            Apply();

            // 1이 맨 위다. 탭을 바꿨는데 목록이 중간부터 보이면 그것은 "다른
            // 화면"이 아니라 "같은 화면이 잘린 것"으로 읽힌다 (ForgePanelTabs와 같은 판단)
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void Apply()
        {
            if (tabs == null || tabs.Length == 0) return;

            ApplyRows();
            ApplyTabs();
            ApplyScrollLength();
        }

        /** 선택된 계열의 줄만 켠다. 계열은 카탈로그에 묻는다 - 위 주석 참고 */
        private void ApplyRows()
        {
            if (rows == null) return;

            int count = Mathf.Min(rows.Length, SkillCatalog.Count);
            for (int i = 0; i < count; i++)
            {
                if (rows[i] == null) continue;

                bool active = (int)SkillCatalog.Skills[i].Family == selected;
                if (rows[i].activeSelf != active) rows[i].SetActive(active);
            }

            // 배열이 카탈로그보다 길면 남는 줄은 끈다. 표에서 오의를 뺀 뒤
            // 씬을 안 구운 상태가 그 자리이고, 켜 두면 참조가 끊긴 줄이 뜬다
            for (int i = count; i < rows.Length; i++)
                if (rows[i] != null && rows[i].activeSelf) rows[i].SetActive(false);
        }

        private void ApplyTabs()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                bool active = i == selected;

                if (tab.nameLabel != null)
                    tab.nameLabel.color = active ? selectedText : unselectedText;

                if (tab.subtitleLabel != null)
                    tab.subtitleLabel.color = active ? selectedText : unselectedText;

                if (tab.background != null)
                    tab.background.color = active ? TintOf(i) : unselectedTint;
            }
        }

        private Color TintOf(int familyIndex)
        {
            if (familyTints == null || familyIndex < 0 || familyIndex >= familyTints.Length)
                return unselectedTint;
            return familyTints[familyIndex];
        }

        /**
         * @brief 스크롤 길이를 **지금 계열의 줄 수**에 맞춘다.
         *
         * 가장 긴 계열에 고정하지 않는 이유는 대장간이 44단계에 겪은 자리
         * 그대로다 - 계열마다 다섯이라 지금은 셋이 같은 길이지만, 진행 해금
         * 오의가 늘거나 줄면 갈린다. 그때 짧은 탭의 마지막 줄 아래로 빈 판이
         * 스크롤되고, 목록의 끝이 화면의 끝이 아니면 "더 있나?" 하고 끌어보게 된다.
         */
        private void ApplyScrollLength()
        {
            if (scroll == null || scroll.content == null) return;

            int inFamily = 0;
            int count = rows != null ? Mathf.Min(rows.Length, SkillCatalog.Count) : SkillCatalog.Count;
            for (int i = 0; i < count; i++)
                if ((int)SkillCatalog.Skills[i].Family == selected) inFamily++;

            var size = scroll.content.sizeDelta;
            size.y = inFamily * rowPitch;
            scroll.content.sizeDelta = size;
        }
    }
}
