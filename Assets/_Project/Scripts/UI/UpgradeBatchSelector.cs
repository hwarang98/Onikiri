using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Onikiri.UI
{
    /**
     * @brief 강화 배수 선택 줄 - [×1] [×10] [×100] [최대] (#9).
     *
     * ## 이것이 무엇이 아닌가
     *
     * **할인이 아니다.** 고른 배수는 "한 칸씩 사기를 그만큼 반복한다"는 뜻이고,
     * 총액도 효과도 기존 곡선 그대로다(UpgradeTrack.AffordableLevels 주석).
     * 새로 생기는 힘은 0이고, 줄어드는 것은 화면을 두드리는 횟수뿐이다.
     *
     * ## 왜 상태를 여기 두는가
     *
     * 배수는 목록 전체의 **모드**다. 행마다 따로 고르면 "지금 ×100인 줄이
     * 어느 것인가"를 행 아홉 개에서 각각 읽어야 하고, 그건 배수를 고르는 값보다
     * 비싸다. 그래서 줄 하나가 상태를 들고 모든 UpgradeButton이 그것을 읽는다.
     *
     * ## 세이브가 아니라 취향이다
     *
     * PlayerPrefs에 남긴다. 진행 값이 아니라 이 사람이 어떻게 누르기를 좋아하는가
     * 이고, 그것을 세이브에 넣으면 세이브 버전이 UI 취향 때문에 올라간다 -
     * 클라우드 세이브(다음 배치)가 옮겨야 할 것도 그만큼 늘어난다.
     */
    public sealed class UpgradeBatchSelector : MonoBehaviour
    {
        /** 배수 하나. 칩 버튼과 그 버튼이 뜻하는 수 */
        [Serializable]
        public sealed class Choice
        {
            public Button button;
            public TMP_Text label;
            public Image background;

            [Tooltip("살 칸 수. 0이면 '최대' - 잔액이 감당하는 데까지")]
            public int count;
        }

        [SerializeField] private Choice[] choices;

        [Header("색")]
        [SerializeField] private Color selectedText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unselectedText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color selectedTint = new Color(0.62f, 0.66f, 1.00f, 1f);
        [SerializeField] private Color unselectedTint = new Color(0.40f, 0.42f, 0.70f, 1f);

        private const string PrefsKey = "onikiri.upgrade.batch";

        /**
         * @brief 지금 고른 배수. **언제나 1 이상이다.**
         *
         * static인 이유는 읽는 쪽(UpgradeButton)이 줄을 찾아 헤매지 않게 하기
         * 위해서다. 강화 목록과 배수 줄은 같은 패널 안에 있지만 부모가 다르고,
         * 행마다 참조를 배선하면 빌더가 아홉 번 같은 일을 한다.
         *
         * 줄이 아직 없는 씬(전투 전용 테스트)에서는 기본값 1이라 예전과 똑같이
         * 한 칸씩 산다 - 배수 줄이 없다고 강화가 멈추지는 않는다.
         *
         * **0("최대")이 값이던 시절이 있었다.** 그 값을 받은 쪽이 잔액이
         * 감당하는 데까지 칸을 하나씩 셌고, 강화 행 아홉이 그것을 골드가
         * 변할 때마다 다시 했다 - 요괴 한 마리에 13.68ms다. 이 속성이 양수만
         * 돌려주는 것이 그 경로를 막는 첫 관문이고, 두 번째 관문은
         * `UpgradeTrack.AffordableLevels`가 0을 0칸으로 거절하는 것이다.
         */
        public static int Current { get; private set; }

        /** 배수가 바뀌었다. 행들이 비용·증가폭 표시를 다시 그린다 */
        public static event Action Changed;

        static UpgradeBatchSelector()
        {
            Current = 1;
        }

        private void Awake()
        {
            // 저장된 취향을 되살린다. 유효하지 않은 값(옛 버전의 칩 구성)은
            // 1로 떨어뜨린다 - 없는 배수가 고정되면 어떤 칩도 선택으로 안 보인다
            int saved = PlayerPrefs.GetInt(PrefsKey, 1);
            Current = IsValid(saved) ? saved : 1;
        }

        private void Start()
        {
            if (choices == null) return;

            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice.button == null) continue;

                int count = choice.count;
                choice.button.onClick.AddListener(() => Select(count));
            }

            Refresh();
        }

        private void OnEnable()
        {
            // 탭을 다시 열 때도 칩 색이 맞아야 한다. 배수는 static이라 이 줄이
            // 꺼져 있는 동안에도 살아 있고, 켜질 때 화면만 따라오면 된다
            Refresh();
        }

        private void OnDestroy()
        {
            if (choices == null) return;
            foreach (var choice in choices)
                if (choice.button != null) choice.button.onClick.RemoveAllListeners();
        }

        private bool IsValid(int count)
        {
            // 0 이하는 어떤 칩에도 없어야 하지만, 씬이 옛 배선을 들고 있을
            // 수 있으므로 여기서도 막는다. 저장된 옛 취향(0 = 최대)이 이
            // 판정에 걸려 1로 떨어지고, 그것이 마이그레이션 전부다
            if (count <= 0) return false;

            if (choices == null) return count == 1;
            foreach (var choice in choices)
                if (choice.count == count) return true;
            return false;
        }

        public void Select(int count)
        {
            if (count <= 0) return;

            Current = count;
            PlayerPrefs.SetInt(PrefsKey, count);

            Refresh();

            var handler = Changed;
            if (handler != null) handler();
        }

        private void Refresh()
        {
            if (choices == null) return;

            foreach (var choice in choices)
            {
                bool active = choice.count == Current;

                if (choice.label != null)
                    choice.label.color = active ? selectedText : unselectedText;

                if (choice.background != null)
                    choice.background.color = active ? selectedTint : unselectedTint;
            }
        }
    }
}
