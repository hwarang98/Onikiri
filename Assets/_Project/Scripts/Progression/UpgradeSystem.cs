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

        public UpgradeTrack GetTrack(string id)
        {
            if (tracks == null) return null;
            foreach (var track in tracks)
                if (track != null && track.Id == id) return track;
            return null;
        }

        /**
         * @brief 세이브 복원. 레벨을 넣은 뒤 스탯까지 다시 적용한다.
         *
         * 레벨만 되돌리고 적용을 잊으면 표시된 레벨과 실제 전투 스탯이 어긋난 채로
         * 플레이가 시작된다.
         */
        public void RestoreLevels(string[] ids, int[] levels)
        {
            if (ids == null || levels == null) return;

            int count = Mathf.Min(ids.Length, levels.Length);
            for (int i = 0; i < count; i++)
            {
                var track = GetTrack(ids[i]);
                // 세이브에 있지만 지금은 없는 트랙은 조용히 건너뛴다. 강화 목록이
                // 바뀌어도 예전 세이브를 계속 읽을 수 있어야 한다
                if (track != null) track.SetLevel(levels[i]);
            }

            ApplyAll();
            Raise();
        }

        public string[] CollectIds()
        {
            if (tracks == null) return new string[0];

            var ids = new string[tracks.Length];
            for (int i = 0; i < tracks.Length; i++) ids[i] = tracks[i] != null ? tracks[i].Id : string.Empty;
            return ids;
        }

        public int[] CollectLevels()
        {
            if (tracks == null) return new int[0];

            var levels = new int[tracks.Length];
            for (int i = 0; i < tracks.Length; i++) levels[i] = tracks[i] != null ? tracks[i].Level : 1;
            return levels;
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
                    // 공격속도는 BigDouble이 필요 없는 축이다. 아트가 정한 상한이 있어서
                    // double 범위를 벗어날 일이 없고, PlayerCombat도 float로 받는다.
                    //
                    // 여기서 상한을 다시 확인하지 않는다. PlayerCombat의 세터가 자른다.
                    // 트랙의 maxLevel과 전투의 상한은 같은 곳(AttackSpeedCurve)에서
                    // 나오지만, 둘 중 하나를 고치고 다른 하나를 잊었을 때 스탯이
                    // 조용히 어긋나는 것보다 잘리는 편이 낫다
                    combat.AttacksPerSecond = (float)track.Value.ToDouble();
                    break;

                default:
                    Debug.LogWarning("[Onikiri] Upgrade track '" + track.Id + "' has no stat wired.");
                    break;
            }
        }

        /** 강화 버튼이 "5 -> 5.6" 을 표시할 때 쓴다 */
        public BigDouble NextValue(UpgradeTrack track)
        {
            return track != null ? track.ValueAtLevel(track.Level + 1) : BigDouble.Zero;
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
