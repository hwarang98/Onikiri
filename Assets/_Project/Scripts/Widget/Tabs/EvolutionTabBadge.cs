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

        /**
         * @brief 배지는 **"가서 할 일이 있다"**는 뜻이다. 그 뜻이 3단계에 바뀌었다.
         *
         * 전까지는 "재화가 모여서 살 수 있다"(`AnyAffordable`)였다. 승급이 귀문
         * 돌파로만 오르게 되면서 경지 탭에서 **누를 수 있는 것이 없어졌고**,
         * 살 수 없는 것을 사라고 가리키는 배지는 거짓말이 된다.
         *
         * 그래서 배지를 끈다. 진행이 막혔다는 신호는 귀문 안내가 전투 화면에서
         * 직접 말하고, 그 자리가 실제로 플레이어가 가야 할 곳이다.
         */
        private void Refresh()
        {
            if (badge == null) return;

            if (badge.activeSelf) badge.SetActive(false);
        }
    }
}
