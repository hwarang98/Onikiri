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
        public const string CritRateId = "crit_rate";
        public const string CritDamageId = "crit_damage";

        /** 생존 축. DPS에 기여하지 않으므로 효율을 재는 자가 다르다 */
        public const string HealthId = "health";
        public const string HealthRegenId = "health_regen";

        /**
         * @brief 획득 축. 골드를 골드로 바꾸는 유일한 축이다.
         *
         * 전투 스탯이 아니라 **보상 배수**라, 다른 여섯처럼 combat/health로 흘러
         * 가지 않는다. 대신 GoldGainMultiplier로 노출하고 골드를 지급하는 쪽이
         * 읽어 간다. 효율 자도 다르다(GoldGainEfficiency).
         */
        public const string GoldGainId = "gold_gain";

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private UpgradeTrack[] tracks;

        /** 레벨이나 잔액이 바뀌어 버튼 표시를 갱신해야 할 때 발생 */
        public event Action Changed;

        public int TrackCount { get { return tracks != null ? tracks.Length : 0; } }

        /**
         * @brief 지금 골드 보상에 곱해지는 배수. 축이 없으면 1.
         *
         * ## 왜 여기서 노출하고 지급 쪽이 읽는가
         *
         * PlayerWallet.Add에서 곱하는 방법이 더 짧지만 그러면 **방치 보상이 두 번
         * 곱해진다.** 방치 보상은 나갈 때 적어둔 초당 골드에서 나오는데(SaveData),
         * 그 값 자체가 이미 이 배수를 포함하고 있고, 지급 경로는 wallet.Add다.
         *
         * 그래서 규칙을 반대로 세운다 - **지갑은 받은 것을 그대로 넣고, 골드를
         * 만드는 쪽이 곱한다.** 만드는 곳은 셋뿐이다:
         *
         *   처치/보스 골드   EnemySpawner
         *   클리어 보너스    BossFight
         *   방치 보상        GameSession.EstimateGoldPerSecond
         *
         * 정적 접근자(Current)를 두는 이유는 그 셋 중 둘이 UpgradeSystem 참조를
         * 들고 있지 않기 때문이다. CharacterLevel.Instance가 스탯 증폭에 쓰는
         * 방식과 같다.
         */
        public double GoldGainMultiplier
        {
            get
            {
                var track = GetTrack(GoldGainId);
                return track != null ? track.Value.ToDouble() : 1d;
            }
        }

        /**
         * @brief 씬에 있는 UpgradeSystem의 골드 배수. 없으면 1.
         *
         * 1로 떨어지는 것이 중요하다. 전투 전용 테스트 씬은 UpgradeSystem 없이
         * 스포너만 세우는데, 거기서 0이 되면 골드가 통째로 사라진다.
         */
        public static double CurrentGoldGain
        {
            get { return Instance != null ? Instance.GoldGainMultiplier : 1d; }
        }

        /** 씬에 하나뿐이다. 골드를 만드는 쪽이 참조 없이 배수를 읽어 간다 */
        public static UpgradeSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second UpgradeSystem appeared; keeping the first.");
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
            if (track == null) return;

            // 생존 축은 PlayerHealth로 간다. combat이 없어도 적용돼야 하므로
            // 아래 전투 스탯보다 먼저 처리한다
            if (track.Id == HealthId || track.Id == HealthRegenId)
            {
                if (health == null) return;

                if (track.Id == HealthId)
                    health.MaxHealthStat = track.Value.ToDouble() * StatAmp(CharacterLevel.HealthAmpId);
                else
                    // 회복은 증폭하지 않는다. 최대 체력의 비율이라(HealthRegenCurve)
                    // 체력 증폭이 오르면 초당 회복량도 같이 오른다. 여기서 또 곱하면
                    // 체력 포인트 하나가 유효체력을 두 번 밀어올린다
                    health.RegenStat = track.Value.ToDouble();
                return;
            }

            // 획득 축은 전투 스탯이 아니다. 값을 어디로도 밀어 넣지 않고,
            // 골드를 지급하는 쪽이 GoldGainMultiplier로 읽어 간다.
            //
            // 여기서 조용히 빠져나가는 것이 중요하다. 아래 switch의 default가
            // "스탯이 배선되지 않았다"고 경고하는데, 이 축은 배선되지 않은 것이
            // 아니라 배선할 스탯이 없는 것이다
            if (track.Id == GoldGainId) return;

            if (combat == null) return;

            switch (track.Id)
            {
                case AttackPowerId:
                    // 스탯 포인트 증폭은 여기서만 곱한다. 공격속도·치명타에는
                    // 붙지 않는다 - 증폭 축이 둘(공격력/체력)뿐이라는 것이
                    // 12단계의 설계이고, 네 화력 축에 고루 뿌리면 골드 축들의
                    // 상대 효율(UpgradeEfficiency가 재는 값)이 레벨에 따라 흔들린다
                    combat.Damage = track.Value * BigDouble.FromDouble(StatAmp(CharacterLevel.AttackAmpId));
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

                case CritRateId:
                    // 확률이라 상한이 트랙의 valueCeiling에서 이미 걸린다.
                    // PlayerCombat도 0~1로 자르지만 그건 이중 안전장치다
                    combat.CritChance = (float)track.Value.ToDouble();
                    break;

                case CritDamageId:
                    combat.CritMultiplier = (float)track.Value.ToDouble();
                    break;

                default:
                    Debug.LogWarning("[Onikiri] Upgrade track '" + track.Id + "' has no stat wired.");
                    break;
            }
        }

        /**
         * @brief 스탯 포인트 증폭 배수. 캐릭터 레벨이 없으면 1.
         *
         * 없을 때 1로 떨어지는 것이 중요하다. 강화 테스트와 전투 전용 테스트 씬은
         * CharacterLevel 없이 UpgradeSystem만 세우는데, 거기서 스탯이 0이 되면
         * 12단계 이전에 쓰던 테스트가 전부 이유 없이 깨진다.
         */
        private static double StatAmp(string axisId)
        {
            var character = CharacterLevel.Instance;
            return character != null ? StatPointCurve.Multiplier(character.PointsIn(axisId)) : 1d;
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
