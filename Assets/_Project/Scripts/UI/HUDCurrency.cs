using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /// <summary>
    /// Gold readout in the top bar.
    ///
    /// Event-driven rather than polled in Update: the balance only changes on a kill, and
    /// re-formatting a BigDouble every frame for a number that rarely moves is wasted work
    /// on a phone.
    /// </summary>
    public sealed class HUDCurrency : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "G ";

        private PlayerWallet wallet;

        private void Start()
        {
            wallet = PlayerWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[Onikiri] HUDCurrency found no PlayerWallet.");
                return;
            }

            wallet.GoldChanged += OnGoldChanged;
            OnGoldChanged(wallet.Gold);
        }

        private void OnDestroy()
        {
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
        }

        private void OnGoldChanged(BigDouble gold)
        {
            if (label != null) label.text = prefix + NumberFormatter.Format(gold);
        }
    }
}
