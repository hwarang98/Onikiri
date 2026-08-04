using System;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 강화 목록을 들고 있고, 구매 결과를 전투 스탯에 반영한다.
     *
     * 스탯을 적용하는 지점을 한 곳으로 모은 이유는, 강화가 실제로 게임에 반영되는
     * 경로가 하나뿐이어야 하기 때문이다. UI가 직접 PlayerCombat을 만지면 "버튼은
     * 눌리는데 아무것도 안 세지는" 상태를 알아채기 어렵다.
     */
    public sealed class UpgradeSystem : MonoBehaviour
    {
        /** 트랙 식별자. 스탯 적용이 이 문자열로 갈린다 */
        public const string AttackPowerId = "attack_power";
        public const string AttackSpeedId = "attack_speed";

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private UpgradeTrack[] tracks;

        /** 레벨이나 잔액이 바뀌어 버튼 표시를 갱신해야 할 때 발생 */
        public event Action Changed;

        public int TrackCount { get { return tracks != null ? tracks.Length : 0; } }

        public UpgradeTrack GetTrack(int index)
        {
            if (tracks == null || index < 0 || index >= tracks.Length) return null;
            return tracks[index];
        }

        private void Start()
        {
            // 레벨 1의 값도 반영해야 한다. 그러지 않으면 시작 스탯은 프리팹에 적힌 값,
            // 강화 후 스탯은 곡선 값이 되어 첫 구매에서 수치가 튄다
            ApplyAll();
            Raise();
        }

        public bool TryPurchase(int index)
        {
            var track = GetTrack(index);
            if (track == null) return false;

            if (!track.TryPurchase(PlayerWallet.Instance)) return false;

            Apply(track);
            Raise();
            return true;
        }

        public void ApplyAll()
        {
            if (tracks == null) return;
            foreach (var track in tracks) Apply(track);
        }

        private void Apply(UpgradeTrack track)
        {
            if (track == null || combat == null) return;

            switch (track.Id)
            {
                case AttackPowerId:
                    combat.Damage = track.Value;
                    break;

                case AttackSpeedId:
                    // 공격속도는 BigDouble이 필요 없는 축이다. 상한이 있는 선형 곡선이라
                    // double 범위를 벗어날 일이 없고, PlayerCombat도 float로 받는다
                    combat.AttacksPerSecond = (float)track.Value.ToDouble();
                    break;

                default:
                    Debug.LogWarning("[Onikiri] Upgrade track '" + track.Id + "' has no stat wired.");
                    break;
            }
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
