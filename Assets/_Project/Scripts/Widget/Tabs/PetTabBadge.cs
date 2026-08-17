using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 펫 탭의 빨간 배지. **지금 누를 수 있는 버튼 수**를 적는다.
     *
     * EquipmentTabBadge와 같은 규칙이다 - 배지는 "가서 할 일이 있다"는
     * 뜻이므로 재화까지 본다(PetSystem.AffordableCount). 해금은 보석을,
     * 레벨은 골드를 듣는다. 스테이지도 듣는다 - st31에 게이트가 열리는
     * 순간 보석이 이미 있으면 그 자리에서 배지가 켜져야 한다.
     */
    public sealed class PetTabBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text label;

        private PetSystem pets;
        private PlayerWallet wallet;
        private GemWallet gems;
        private StageProgress stage;

        private void Start()
        {
            pets = PetSystem.Instance;
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;
            stage = Object.FindFirstObjectByType<StageProgress>();

            if (pets != null) pets.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;
            if (stage != null) stage.Changed += Refresh;

            Refresh();
        }

        private void OnEnable()
        {
            if (pets == null) pets = PetSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (pets != null) pets.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        private void Refresh()
        {
            if (badge == null) return;

            int count = pets != null ? pets.AffordableCount : 0;

            bool show = count > 0;
            if (badge.activeSelf != show) badge.SetActive(show);

            if (show && label != null) label.text = count.ToString();
        }
    }
}
