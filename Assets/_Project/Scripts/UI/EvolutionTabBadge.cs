using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 전직 탭의 빨간 배지. **지금 진화할 수 있으면** 뜬다 (33단계).
     *
     * EquipmentTabBadge와 같은 규칙 - 재화가 있는 것만으로는 안 되고, 눌러서
     * 실제로 무언가 바뀌는 상태(해금 + 다음 티어 존재 + 두 재화 충족)만 센다.
     * 판정은 EvolutionSystem.AnyAffordable 한 곳이 한다.
     *
     * 숫자는 언제나 1이다 - 사다리는 한 번에 한 칸만 오른다. 그래도 숫자를
     * 적는 이유는 다른 배지들과 모양이 같아야 하기 때문이다. 배지마다 다른
     * 문법을 쓰면 배지가 아니라 장식이 된다.
     *
     * 골드·보석·레벨·티어 넷을 다 듣는다. 하나라도 빠지면 "조건이 찼는데
     * 배지가 안 뜬다"가 되고, 그 증상은 다음 이벤트에 저절로 고쳐져서 원인을
     * 찾기 어렵다.
     */
    public sealed class EvolutionTabBadge : MonoBehaviour
    {
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text label;

        private EvolutionSystem evolution;
        private CharacterLevel character;
        private PlayerWallet wallet;
        private GemWallet gems;

        private void Start()
        {
            evolution = EvolutionSystem.Instance;
            character = CharacterLevel.Instance;
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;

            if (evolution != null) evolution.Changed += Refresh;
            if (character != null) character.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            Refresh();
        }

        private void OnEnable()
        {
            if (evolution == null) evolution = EvolutionSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (evolution != null) evolution.Changed -= Refresh;
            if (character != null) character.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        private void Refresh()
        {
            if (badge == null) return;

            bool show = evolution != null && evolution.AnyAffordable;
            if (badge.activeSelf != show) badge.SetActive(show);

            if (show && label != null) label.text = "1";
        }
    }
}
