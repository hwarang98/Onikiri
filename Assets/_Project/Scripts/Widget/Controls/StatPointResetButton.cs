using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 성장 탭 머리글 오른쪽의 "초기화" 버튼과 그 확인 팝업 (66단계).
     *
     * 누르면 바로 되돌리지 않고 팝업이 뜬다. 되돌린 뒤에는 다시 찍는 것
     * 말고 원래대로 돌아갈 길이 없고, 두 번째부터는 보석 150(사흘치)이 든다.
     * 그 비용이 **누르기 전에** 화면에 있어야 한다.
     *
     * 실제 일은 StatPointReset 한 문이 한다 - 보석 차감이 포인트 되돌림보다
     * 먼저이고, 보석은 GemWallet으로만 빠진다(60단계 urgent 동기화가 그것을
     * 듣는다). 여기는 무엇을 누를 수 있는지와 무엇이 일어날지를 보여줄 뿐이다.
     *
     * 문구는 빌더가 직렬화 필드로 넣는다(UIStrings.txt에 같은 줄이 있다).
     */
    public sealed class StatPointResetButton : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text openLabel;

        [Header("확인 팝업")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button dimButton;

        [Header("문구")]
        [SerializeField] private string openText = string.Empty;
        [SerializeField] private string openFreeText = string.Empty;
        [SerializeField] private string bodyFormat = string.Empty;
        [SerializeField] private string freeCostText = string.Empty;
        [SerializeField] private string gemCostFormat = string.Empty;
        [SerializeField] private string shortOfGemsFormat = string.Empty;

        [Header("색")]
        [SerializeField] private Color enabledColor = Color.white;
        [SerializeField] private Color disabledColor = Color.gray;

        private CharacterLevel character;
        private GemWallet gems;

        /** 마지막으로 누른 결과. 테스트 패널과 PlayMode 검사가 읽는다 */
        public StatPointReset.Result LastResult { get; private set; }

        public bool IsPopupOpen { get { return popupRoot != null && popupRoot.activeSelf; } }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void Start()
        {
            Bind();

            if (openButton != null) openButton.onClick.AddListener(Open);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Close);
            if (dimButton != null) dimButton.onClick.AddListener(Close);

            if (character != null) character.Changed += Refresh;
            if (gems != null) gems.GemsChanged += OnGems;

            if (popupRoot != null) popupRoot.SetActive(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Close);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);

            if (character != null) character.Changed -= Refresh;
            if (gems != null) gems.GemsChanged -= OnGems;
        }

        private void Bind()
        {
            if (character == null) character = CharacterLevel.Instance;
            if (gems == null) gems = GemWallet.Instance;
        }

        private void OnGems(long _)
        {
            Refresh();
        }

        /** 팝업을 연다. 찍은 것이 없으면 열지 않는다 - 버튼도 꺼져 있다 */
        public void Open()
        {
            Bind();
            if (character == null || character.SpentPoints <= 0) return;

            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
                popupRoot.transform.SetAsLastSibling();
            }
            Refresh();
        }

        public void Close()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        /** 팝업의 "초기화". 결과와 상관없이 팝업은 닫지 않는다 - 보석 부족은 그 자리에서 읽혀야 한다 */
        public void Confirm()
        {
            Bind();
            LastResult = StatPointReset.TryReset(character, gems);

            if (LastResult == StatPointReset.Result.ResetFree || LastResult == StatPointReset.Result.ResetPaid)
            {
                Close();
                Debug.Log("[Onikiri] 스탯 포인트 초기화: " + LastResult
                          + " (보석 " + (gems != null ? gems.Gems : 0L) + ")");
            }

            Refresh();
        }

        private void Refresh()
        {
            if (character == null) return;

            bool free = character.IsNextResetFree;
            int spent = character.SpentPoints;
            int cost = StatPointReset.NextCost(character);
            bool affordable = free || (gems != null && gems.CanAfford(cost));

            if (openLabel != null)
            {
                openLabel.text = free && !string.IsNullOrEmpty(openFreeText) ? openFreeText : openText;
                openLabel.color = spent > 0 ? enabledColor : disabledColor;
            }
            if (openButton != null) openButton.interactable = spent > 0;

            if (bodyLabel != null) bodyLabel.text = string.Format(bodyFormat, spent);

            if (costLabel != null)
            {
                if (free) costLabel.text = freeCostText;
                else if (affordable) costLabel.text = string.Format(gemCostFormat, cost);
                else costLabel.text = string.Format(shortOfGemsFormat, cost, gems != null ? gems.Gems : 0L);
                costLabel.color = affordable ? enabledColor : disabledColor;
            }

            if (confirmButton != null) confirmButton.interactable = spent > 0 && affordable;
        }
    }
}
