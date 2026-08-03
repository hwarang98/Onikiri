using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /// <summary>
    /// The player's currency.
    ///
    /// Gold is a <see cref="BigDouble"/> from the very first coin, not a long that gets
    /// promoted later. In an idle game the total passes long's range within hours of play,
    /// and retrofitting the number type afterwards means touching every system that ever
    /// reads a balance.
    /// </summary>
    public sealed class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [SerializeField] private BigDouble gold;

        /// <summary>Raised on every balance change, with the new total.</summary>
        public event Action<BigDouble> GoldChanged;

        /// <summary>Running total of everything ever earned. Useful for later prestige maths.</summary>
        public BigDouble LifetimeGold { get; private set; }

        public BigDouble Gold { get { return gold; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Raise();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(BigDouble amount)
        {
            if (amount.IsZero || amount.IsNegative) return;

            gold += amount;
            LifetimeGold += amount;
            Raise();
        }

        public bool CanAfford(BigDouble cost)
        {
            return gold >= cost;
        }

        /// <summary>Spends only if affordable; returns whether the purchase happened.</summary>
        public bool TrySpend(BigDouble cost)
        {
            if (cost.IsNegative || !CanAfford(cost)) return false;

            gold -= cost;
            Raise();
            return true;
        }

        /// <summary>Used by save/load, which restores a balance rather than earning it.</summary>
        public void SetBalance(BigDouble value, BigDouble lifetime)
        {
            gold = value;
            LifetimeGold = lifetime;
            Raise();
        }

        private void Raise()
        {
            var handler = GoldChanged;
            if (handler != null) handler(gold);
        }
    }
}
