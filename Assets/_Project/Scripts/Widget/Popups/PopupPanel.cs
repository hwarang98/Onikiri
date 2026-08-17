using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 팝업 하나의 여닫기 (#1). 딤 배경 + 닫기 버튼.
     *
     * ## 화면과 팝업의 차이
     *
     * 그전까지 설정·랭킹은 성장 패널 띠를 통째로 차지하는 **화면**이었다.
     * 하단 탭 화면들(스킬·퀘스트·장비)과 같은 형태다. 그런데 그 둘은 성격이
     * 다르다 - 스킬과 장비는 **거기서 무언가를 한다**(사고, 끼우고, 올린다).
     * 설정과 랭킹은 **보고 나온다.**
     *
     * 보고 나오는 것을 화면으로 만들면 두 가지가 어긋난다. 첫째로 게임이
     * 뒤에서 계속 도는데 그것이 안 보인다 - 팝업은 화면의 일부만 덮으므로
     * 전투와 골드가 계속 보인다. 둘째로 닫는 방법이 "그 탭을 다시 누르기"
     * 하나뿐인데, 그건 열 때 누른 것이 무엇이었는지 기억해야 하는 일이다.
     *
     * ## 닫는 길이 셋이다
     *
     *   X 버튼      팝업의 표준. 늘 같은 자리(오른쪽 위)에 있다
     *   딤 탭       바깥을 누르면 닫힌다. 손가락으로 쓰는 화면의 표준이다
     *   여는 버튼   상단 바의 그 칩을 다시 누르면 닫힌다(HudScreenButton)
     *
     * 셋 다 같은 일을 한다 - 루트를 끈다. 상태가 하나라 "닫았는데 반쯤
     * 열려 있다"가 성립하지 않는다.
     */
    public sealed class PopupPanel : MonoBehaviour
    {
        [Tooltip("닫기 버튼. 창의 오른쪽 위")]
        [SerializeField] private Button closeButton;

        [Tooltip("딤 배경 버튼. 창 바깥을 누르면 닫힌다")]
        [SerializeField] private Button dimButton;

        /**
         * @brief 딤을 눌러도 안 닫히게 (뽑기 연출처럼 끝까지 봐야 하는 팝업).
         *
         * 실수로 바깥을 스치는 것과 닫으려는 의도를 구분할 수 없는 팝업이
         * 있다. 결과가 하나씩 열리는 동안의 뽑기 창(#12)이 그렇다 - 거기서
         * 오폭으로 닫히면 무엇을 뽑았는지 못 본 채로 사라진다.
         */
        [Tooltip("딤 탭으로 닫지 않는다. X 버튼만 남는다")]
        [SerializeField] private bool dimIsInert;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (dimButton != null && !dimIsInert) dimButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);
        }

        /** 판을 끈다. 여는 쪽(HudScreenButton)이 다시 켠다 */
        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
