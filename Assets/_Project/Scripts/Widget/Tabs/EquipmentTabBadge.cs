using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 장비 탭의 빨간 배지. **지금 누를 수 있는 버튼 수**를 적는다.
     *
     * QuestTabBadge와 같은 규칙(탭 버튼의 자식, 숫자)이지만 세는 것이 다르다.
     * 퀘스트는 "받을 것"을 세고 여기는 "살 수 있는 것"을 센다.
     *
     * ## 재화가 있는 것만으로는 안 된다
     *
     * 성장 탭 배지가 남은 스탯 포인트를 세는 것과 같은 기준이다. 배지는 "가서
     * 할 일이 있다"는 뜻이고, 눌러도 아무것도 안 바뀌는 버튼(상한에 닿았거나
     * 단련이 남았는데 등급업을 보고 있거나)은 할 일이 아니다.
     * EquipmentSystem.AffordableCount 가 그 판정을 한 곳에서 한다.
     *
     * ## 골드와 보석 둘 다 듣는다
     *
     * 이 화면만 두 재화를 쓴다. 하나만 들으면 "보석이 들어왔는데 배지가 안
     * 뜬다"가 되고, 그 증상은 다음 골드 획득 때 저절로 고쳐져서 원인을 찾기
     * 어렵다.
     */
    public sealed class EquipmentTabBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text label;

        private EquipmentSystem equipment;
        private PlayerWallet wallet;
        private GemWallet gems;
        private StageProgress stage;

        private void Start()
        {
            equipment = EquipmentSystem.Instance;
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;
            stage = Object.FindFirstObjectByType<StageProgress>();

            if (equipment != null) equipment.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;
            if (stage != null) stage.Changed += Refresh;

            Refresh();
        }

        private void OnEnable()
        {
            if (equipment == null) equipment = EquipmentSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (equipment != null) equipment.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        private void Refresh()
        {
            if (badge == null) return;

            int count = equipment != null ? equipment.AffordableCount : 0;

            bool show = count > 0;
            if (badge.activeSelf != show) badge.SetActive(show);

            if (show && label != null) label.text = count.ToString();
        }
    }
}
