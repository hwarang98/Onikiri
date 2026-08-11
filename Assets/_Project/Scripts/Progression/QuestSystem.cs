using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 퀘스트 진행·수령·일일 리셋을 관장한다.
     *
     * ## 카운터가 두 벌인 이유
     *
     *   오늘치   일일 퀘스트가 읽는다. 자정에 0으로 돌아간다
     *   누적     반복 퀘스트가 읽는다. 영원히 는다
     *
     * 한 벌로 합칠 수는 없다. 일일을 "누적 - 어제까지의 누적"으로 계산하는 방법도
     * 있지만, 그러면 스냅샷을 저장해야 하고 그 스냅샷이 리셋과 어긋나는 경로가
     * 생긴다. 두 벌은 각자가 자기 사실만 말한다.
     *
     * ## 업적은 카운터를 쓰지 않는다
     *
     * 스테이지·레벨·강화 총합은 **지금 상태**다. 세지 않고 그때그때 읽는다.
     * 세면 세이브에 값이 하나 더 늘고, 그 값이 실제 상태와 어긋나는 날
     * "레벨 50인데 레벨 50 업적이 안 열린다"가 된다.
     *
     * ## 지급은 수령 버튼에서만
     *
     * 조건을 만족하는 순간 자동으로 주지 않는다. 방치형에서 자동 지급은 **화면을
     * 안 보고 있을 때 일어나므로**, 플레이어가 보상을 받았다는 사실을 모른다.
     * 받는 행동이 있어야 배지와 빨간 점이 뜻을 갖는다.
     */
    public sealed class QuestSystem : MonoBehaviour
    {
        public static QuestSystem Instance { get; private set; }

        [SerializeField] private GemWallet gems;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private CharacterLevel character;
        [SerializeField] private StageProgress stage;
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private SkillSystem skills;

        [Tooltip("업적 골드 환산의 기준. 스포너의 잡몹 평균 골드와 같아야 한다")]
        [SerializeField] private Onikiri.Battle.EnemySpawner spawner;

        /** 진행·수령 상태가 바뀔 때마다. UI가 이것으로 다시 그린다 */
        public event Action Changed;

        // ---------------------------------------------------------------- 카운터

        private double todayMobKills, todayBossKills, todaySkillCasts, todayUpgrades;
        private BigDouble todayGold;

        private double totalMobKills, totalBossKills, totalSkillCasts, totalUpgrades;
        private BigDouble totalGold;

        /** 일일 수령 여부. QuestCatalog.Daily와 같은 순서 */
        private bool[] dailyClaimed = new bool[QuestCatalog.DailyCount];

        /** 반복에서 **이미 받은 티어 수**. 열린 티어가 이보다 많으면 받을 것이 있다 */
        private int[] repeatClaimed = new int[QuestCatalog.RepeatCount];

        private bool[] achievementClaimed = new bool[QuestCatalog.AchievementCount];

        /** 마지막 일일 리셋 기준 시각 (UTC). 퀘스트일 경계 비교의 기준 */
        private DateTime lastDailyReset = DateTime.MinValue;

        // ---------------------------------------------------------------- 퀘스트일

        /**
         * @brief 일일 리셋 시각 - **KST 새벽 4시** (39단계, UTC 자정에서 이동).
         *
         * UTC 자정은 한국의 오전 9시다. 아침에 켠 플레이어가 어제 밤에 하다 만
         * 일일 퀘스트를 보고, 출근길에 그것이 눈앞에서 리셋된다 - 하루의 경계가
         * 생활의 경계와 어긋나 있었다. 새벽 4시는 그 반대다: 자정 넘어서까지 한
         * 판은 "오늘"에 남고, 아침에 켜면 새 하루가 시작돼 있다.
         *
         * 고정 오프셋(UTC+9)이지 기기 시간대가 아니다. 로컬 자정을 쓰면 시간대를
         * 넘나드는 플레이어에게 리셋이 두 번 오거나 건너뛴다는 원칙은 그대로다 -
         * 기준 시각만 한국 생활권에 맞춘 것이다. 서버가 없어 기기 시계 조작을
         * 막지 못하는 것도 그대로다(수익화 단계의 몫).
         */
        public const int ResetHourKst = 4;
        private const int KstUtcOffsetHours = 9;

        /**
         * @brief 이 시각이 속한 "퀘스트일". KST 새벽 4시마다 하루가 넘어간다.
         *
         * KST 04:00 = UTC 전날 19:00이므로, UTC에 (9-4)=5시간을 더한 날짜가
         * 곧 퀘스트일이다. 정적 순수 함수인 이유는 테스트가 씬 없이 경계를
         * 검사하기 때문이다(QuestTests).
         */
        public static DateTime QuestDayOf(DateTime utc)
        {
            return utc.AddHours(KstUtcOffsetHours - ResetHourKst).Date;
        }

        /** 다음 리셋의 UTC 시각. 퀘스트일 경계는 UTC (퀘스트일 + 19:00)이다 */
        public static DateTime NextResetUtc(DateTime utcNow)
        {
            return QuestDayOf(utcNow).AddDays(1).AddHours(-(KstUtcOffsetHours - ResetHourKst));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /**
         * @brief 매 프레임 자정을 확인한다.
         *
         * 폴링이 맞는 자리다. 앱을 켜둔 채 자정을 넘기는 일이 실제로 일어나는데,
         * 로드 시점에만 확인하면 그 플레이어는 **다음에 앱을 껐다 켤 때까지**
         * 어제 퀘스트를 보게 된다.
         *
         * 비용은 DateTime.UtcNow 하나다. 날짜가 바뀌는 프레임에만 일이 생긴다.
         */
        private void Update()
        {
            RollDailyIfNeeded(DateTime.UtcNow);
        }

        // ---------------------------------------------------------------- 일일 리셋

        /**
         * @brief 퀘스트일이 넘어갔으면 오늘치를 비운다. 경계는 KST 새벽 4시다
         * (QuestDayOf 주석 참고).
         *
         * ⚠️ **기기 시간 조작은 막지 않는다.** 서버가 없으므로 UtcNow를 믿는 것
         * 외에 방법이 없고, 폰 시계를 돌리면 일일 퀘스트를 반복해서 받을 수 있다.
         * 보상이 보석뿐이고 보석에 소비처가 없는 지금은 실익이 없으며, 방어는
         * 수익화 단계(서버 시각)의 몫이다.
         *
         * 며칠이 지났든 **한 번만** 리셋한다. 이틀 치를 주는 것이 아니라 오늘 것을
         * 새로 여는 것이 일일 퀘스트다.
         *
         * ## 예전 세이브와의 경계 (39단계 이동)
         *
         * 예전 필드에는 UTC 자정으로 자른 날짜가 들어 있다. 그 값을 그대로
         * QuestDayOf에 넣으면 자정+5시간이 같은 날짜라 **그 날의 퀘스트일**이
         * 되는데, 이는 항상 실제 마지막 리셋의 퀘스트일과 같거나 이르다 -
         * 그래서 리셋이 **건너뛰는 경로는 없다.** 마지막 저장이 UTC 19시 이후였던
         * 세이브만 로드 직후 리셋이 한 번 더 오는데, 그 시각은 새 규칙에서 이미
         * 새 퀘스트일(KST 새벽 4시 경과)이므로 이중이 아니라 경계 이동이다.
         * 세이브 필드는 그대로다 - 이후 저장부터 전체 시각이 들어간다.
         */
        public void RollDailyIfNeeded(DateTime utcNow)
        {
            if (lastDailyReset != DateTime.MinValue
                && QuestDayOf(utcNow) <= QuestDayOf(lastDailyReset)) return;

            lastDailyReset = utcNow;

            todayMobKills = 0d;
            todayBossKills = 0d;
            todaySkillCasts = 0d;
            todayUpgrades = 0d;
            todayGold = BigDouble.Zero;

            for (int i = 0; i < dailyClaimed.Length; i++) dailyClaimed[i] = false;

            Raise();
        }

        /** 다음 리셋(KST 새벽 4시)까지 남은 시간. 화면의 타이머가 읽는다 */
        public TimeSpan UntilDailyReset(DateTime utcNow)
        {
            var left = NextResetUtc(utcNow) - utcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        // ---------------------------------------------------------------- 진행 훅

        public void ReportMobKill()
        {
            todayMobKills += 1d;
            totalMobKills += 1d;
            Raise();
        }

        public void ReportBossKill()
        {
            todayBossKills += 1d;
            totalBossKills += 1d;
            Raise();
        }

        /**
         * @brief 획득한 골드를 센다.
         *
         * **소비는 세지 않는다.** "골드 2,000 획득"은 번 것을 말하고, 잔액이
         * 줄어드는 것과 무관해야 한다 - 강화를 사면 목표가 뒤로 가는 퀘스트는
         * 사지 말라는 뜻이 된다.
         */
        public void ReportGoldEarned(BigDouble amount)
        {
            if (amount.IsZero || amount.IsNegative) return;

            todayGold += amount;
            totalGold += amount;
            Raise();
        }

        public void ReportSkillCast()
        {
            todaySkillCasts += 1d;
            totalSkillCasts += 1d;
            Raise();
        }

        public void ReportUpgradePurchase()
        {
            todayUpgrades += 1d;
            totalUpgrades += 1d;
            Raise();
        }

        // ---------------------------------------------------------------- 진행 읽기

        /** 지금 상태에서 이 지표의 값. 업적은 카운터가 아니라 여기서 읽는다 */
        private double CurrentStateOf(QuestMetric metric)
        {
            switch (metric)
            {
                case QuestMetric.StageReached:
                    // "도달"은 최전선이다(37단계 재선택). 현재 스테이지를 읽으면
                    // 클리어한 지역으로 되돌아간 순간 달성했던 도달 업적이
                    // 화면에서 미달성으로 되돌아간다
                    return stage != null ? stage.MaxStageReached : 1d;

                case QuestMetric.LevelReached:
                    return character != null ? character.Level : 1d;

                case QuestMetric.UpgradeLevelTotal:
                    return upgrades != null ? upgrades.TotalLevels : 0d;

                case QuestMetric.SkillLevelTotal:
                    return skills != null ? skills.TotalLevels : 0d;

                default:
                    return 0d;
            }
        }

        private double CounterOf(QuestMetric metric, bool today)
        {
            switch (metric)
            {
                case QuestMetric.MobKills: return today ? todayMobKills : totalMobKills;
                case QuestMetric.BossKills: return today ? todayBossKills : totalBossKills;
                case QuestMetric.SkillCasts: return today ? todaySkillCasts : totalSkillCasts;
                case QuestMetric.UpgradePurchases: return today ? todayUpgrades : totalUpgrades;
                case QuestMetric.GoldEarned: return (today ? todayGold : totalGold).ToDouble();
                default: return CurrentStateOf(metric);
            }
        }

        /**
         * @brief 이 퀘스트의 진행 (0~목표치). 반복은 **이번 티어 안에서**의 진행이다.
         */
        public double ProgressOf(QuestKind kind, int index)
        {
            var specs = QuestCatalog.Of(kind);
            if (index < 0 || index >= specs.Length) return 0d;
            var spec = specs[index];

            if (kind == QuestKind.Daily)
                return Math.Min(CounterOf(spec.Metric, true), spec.Target);

            if (kind == QuestKind.Achievement)
                return Math.Min(CurrentStateOf(spec.Metric), spec.Target);

            // 반복: 받은 티어까지를 빼면 이번 티어의 진행이 남는다
            double counter = CounterOf(spec.Metric, false);
            double consumed = repeatClaimed[index] * spec.Target;
            double inTier = counter - consumed;
            if (inTier < 0d) inTier = 0d;
            return Math.Min(inTier, spec.Target);
        }

        /** 이 퀘스트에서 지금 받을 수 있는 횟수. 반복만 2 이상이 될 수 있다 */
        public int ClaimableCount(QuestKind kind, int index)
        {
            var specs = QuestCatalog.Of(kind);
            if (index < 0 || index >= specs.Length) return 0;
            var spec = specs[index];

            if (kind == QuestKind.Daily)
            {
                if (dailyClaimed[index]) return 0;
                return CounterOf(spec.Metric, true) >= spec.Target ? 1 : 0;
            }

            if (kind == QuestKind.Achievement)
            {
                if (achievementClaimed[index]) return 0;
                return CurrentStateOf(spec.Metric) >= spec.Target ? 1 : 0;
            }

            if (spec.Target <= 0d) return 0;
            int opened = (int)Math.Floor(CounterOf(spec.Metric, false) / spec.Target);
            int left = opened - repeatClaimed[index];
            return left > 0 ? left : 0;
        }

        /** 이미 받아서 더는 열리지 않는가. 일일/업적만 true가 된다 */
        public bool IsClaimed(QuestKind kind, int index)
        {
            if (kind == QuestKind.Daily)
                return index >= 0 && index < dailyClaimed.Length && dailyClaimed[index];

            if (kind == QuestKind.Achievement)
                return index >= 0 && index < achievementClaimed.Length && achievementClaimed[index];

            return false;
        }

        /** 반복에서 지금까지 받은 티어 수. 화면이 "3단계"처럼 적는다 */
        public int RepeatTier(int index)
        {
            return index >= 0 && index < repeatClaimed.Length ? repeatClaimed[index] : 0;
        }

        /** 받을 것이 하나라도 있는가. 진입 아이콘의 빨간 배지가 이것을 본다 */
        public bool AnyClaimable
        {
            get { return TotalClaimable > 0; }
        }

        public int TotalClaimable
        {
            get
            {
                int total = 0;
                for (int i = 0; i < QuestCatalog.DailyCount; i++)
                    total += ClaimableCount(QuestKind.Daily, i);
                for (int i = 0; i < QuestCatalog.RepeatCount; i++)
                    total += ClaimableCount(QuestKind.Repeat, i);
                for (int i = 0; i < QuestCatalog.AchievementCount; i++)
                    total += ClaimableCount(QuestKind.Achievement, i);
                return total;
            }
        }

        // ---------------------------------------------------------------- 수령

        /**
         * @brief 한 번 받는다. 반복이면 티어 하나만 오른다.
         *
         * 여러 티어가 열려 있어도 한 번에 하나씩이다. 한꺼번에 주면 화면에서
         * 숫자가 한 번 튀고 끝나는데, 한 번에 하나면 누를 때마다 보상이 뜬다 -
         * 쌓아둔 보람이 눌린 횟수로 나온다.
         */
        public bool TryClaim(QuestKind kind, int index)
        {
            if (ClaimableCount(kind, index) <= 0) return false;

            var spec = QuestCatalog.Of(kind)[index];

            if (gems == null) gems = GemWallet.Instance;
            if (gems != null) gems.Add(spec.Gems);

            if (kind == QuestKind.Achievement)
            {
                GrantAchievementSpoils(spec);
                achievementClaimed[index] = true;
            }
            else if (kind == QuestKind.Daily)
            {
                dailyClaimed[index] = true;
            }
            else
            {
                repeatClaimed[index]++;
            }

            Raise();
            return true;
        }

        /**
         * @brief 업적의 골드/경험치. **여기가 밴드를 건드리는 유일한 지점이다.**
         *
         * 잡몹 평균 골드를 스포너에서 읽는다 - StageSimulation도 같은 값
         * (Field.AverageMobGold)에서 계산하므로 둘이 같은 크기를 낸다. 여기서
         * 상수를 쓰면 시뮬레이션과 게임이 다른 보상을 주고, 그 차이는 밸런스
         * 표에 나타나지 않는다.
         */
        private void GrantAchievementSpoils(QuestSpec spec)
        {
            var mobGold = spawner != null ? spawner.AverageBaseGold : BigDouble.Zero;

            // **받는 순간의 스테이지**로 환산한다. 업적마다 기준 스테이지를 적어
            // 두는 방법도 있었는데, 추정이 빗나가면 크기가 통째로 틀어진다 -
            // 시뮬레이션에서 st11 여유가 2.79에서 18.73으로 튀어 잡았다.
            // QuestCatalog.Achievement 표 주석 참고.
            // 재선택(37단계) 뒤로는 최전선이다 - 클리어한 지역에 내려가 받으면
            // 보상이 쪼그라드는 것도, 그 반대로 부풀는 것도 없어야 한다
            int at = stage != null ? stage.MaxStageReached : 1;

            var gold = QuestCatalog.AchievementGold(spec, mobGold, at);
            if (gold > BigDouble.Zero)
            {
                if (wallet == null) wallet = PlayerWallet.Instance;
                if (wallet != null) wallet.Add(gold);
            }

            var exp = QuestCatalog.AchievementExp(spec, at);
            if (exp > BigDouble.Zero)
            {
                if (character == null) character = CharacterLevel.Instance;
                if (character != null) character.AddExp(exp);
            }
        }

        // ---------------------------------------------------------------- 세이브

        /**
         * @brief 세이브에서 복원한다. **리셋 판정은 복원 뒤에 한다.**
         *
         * 순서가 뒤바뀌면 방금 비운 오늘치 위에 어제 값이 덮인다 - 자정을 넘겨
         * 접속한 플레이어의 일일 퀘스트가 이미 완료된 채로 뜬다.
         */
        public void Restore(SaveData data)
        {
            if (data == null) return;

            todayMobKills = data.questTodayMobKills;
            todayBossKills = data.questTodayBossKills;
            todaySkillCasts = data.questTodaySkillCasts;
            todayUpgrades = data.questTodayUpgrades;
            todayGold = data.questTodayGold;

            totalMobKills = data.questTotalMobKills;
            totalBossKills = data.questTotalBossKills;
            totalSkillCasts = data.questTotalSkillCasts;
            totalUpgrades = data.questTotalUpgrades;
            totalGold = data.questTotalGold;

            ReadClaims(data);

            lastDailyReset = data.LastDailyResetUtc ?? DateTime.MinValue;

            if (gems == null) gems = GemWallet.Instance;
            if (gems != null) gems.SetBalance(data.gems);

            RollDailyIfNeeded(DateTime.UtcNow);
            Raise();
        }

        /**
         * @brief 수령 상태를 id로 읽는다. 인덱스가 아니다.
         *
         * 강화 축·오의와 같은 규칙이다(SaveData 주석) - 목록 중간에 퀘스트가
         * 하나 추가되면 인덱스 저장은 엉뚱한 퀘스트에 수령 표시를 밀어 넣는다.
         */
        private void ReadClaims(SaveData data)
        {
            for (int i = 0; i < dailyClaimed.Length; i++) dailyClaimed[i] = false;
            for (int i = 0; i < repeatClaimed.Length; i++) repeatClaimed[i] = 0;
            for (int i = 0; i < achievementClaimed.Length; i++) achievementClaimed[i] = false;

            if (data.questIds == null || data.questClaims == null) return;

            int count = Mathf.Min(data.questIds.Length, data.questClaims.Length);
            for (int i = 0; i < count; i++)
            {
                string id = data.questIds[i];
                int value = data.questClaims[i];
                if (string.IsNullOrEmpty(id)) continue;

                int index = IndexIn(QuestCatalog.Daily, id);
                if (index >= 0) { dailyClaimed[index] = value > 0; continue; }

                index = IndexIn(QuestCatalog.Repeat, id);
                if (index >= 0) { repeatClaimed[index] = value > 0 ? value : 0; continue; }

                index = IndexIn(QuestCatalog.Achievement, id);
                if (index >= 0) achievementClaimed[index] = value > 0;

                // 모르는 id는 조용히 건너뛴다. 표에서 퀘스트를 빼도 예전 세이브가
                // 예외를 던지지 않게 - 강화 축 복원과 같은 안전장치다
            }
        }

        private static int IndexIn(QuestSpec[] specs, string id)
        {
            for (int i = 0; i < specs.Length; i++)
                if (specs[i].Id == id) return i;
            return -1;
        }

        /** 세이브에 적는다. GameSession이 부른다 */
        public void Write(SaveData data)
        {
            if (data == null) return;

            data.questTodayMobKills = todayMobKills;
            data.questTodayBossKills = todayBossKills;
            data.questTodaySkillCasts = todaySkillCasts;
            data.questTodayUpgrades = todayUpgrades;
            data.questTodayGold = todayGold;

            data.questTotalMobKills = totalMobKills;
            data.questTotalBossKills = totalBossKills;
            data.questTotalSkillCasts = totalSkillCasts;
            data.questTotalUpgrades = totalUpgrades;
            data.questTotalGold = totalGold;

            data.lastDailyResetUtcTicks =
                lastDailyReset == DateTime.MinValue ? 0L : lastDailyReset.Ticks;

            var ids = new string[QuestCatalog.TotalCount];
            var claims = new int[ids.Length];
            int at = 0;

            for (int i = 0; i < QuestCatalog.DailyCount; i++, at++)
            {
                ids[at] = QuestCatalog.Daily[i].Id;
                claims[at] = dailyClaimed[i] ? 1 : 0;
            }
            for (int i = 0; i < QuestCatalog.RepeatCount; i++, at++)
            {
                ids[at] = QuestCatalog.Repeat[i].Id;
                claims[at] = repeatClaimed[i];
            }
            for (int i = 0; i < QuestCatalog.AchievementCount; i++, at++)
            {
                ids[at] = QuestCatalog.Achievement[i].Id;
                claims[at] = achievementClaimed[i] ? 1 : 0;
            }

            data.questIds = ids;
            data.questClaims = claims;

            if (gems == null) gems = GemWallet.Instance;
            data.gems = gems != null ? gems.Gems : 0L;
        }

        // ---------------------------------------------------------------- 테스트 패널

        /** 진행·수령을 전부 비운다. 보석 잔액은 건드리지 않는다 */
        public void DebugResetProgress()
        {
            todayMobKills = todayBossKills = todaySkillCasts = todayUpgrades = 0d;
            totalMobKills = totalBossKills = totalSkillCasts = totalUpgrades = 0d;
            todayGold = BigDouble.Zero;
            totalGold = BigDouble.Zero;

            for (int i = 0; i < dailyClaimed.Length; i++) dailyClaimed[i] = false;
            for (int i = 0; i < repeatClaimed.Length; i++) repeatClaimed[i] = 0;
            for (int i = 0; i < achievementClaimed.Length; i++) achievementClaimed[i] = false;

            Raise();
        }

        /** 일일 리셋을 지금 강제한다. 자정을 기다리지 않고 확인하는 경로 */
        public void DebugForceDailyReset()
        {
            lastDailyReset = DateTime.MinValue;
            RollDailyIfNeeded(DateTime.UtcNow);
        }

        /** 카운터를 목표치까지 채운다. 수령 화면을 확인하는 경로 */
        public void DebugFillCounters()
        {
            todayMobKills = Math.Max(todayMobKills, 1000d);
            todayBossKills = Math.Max(todayBossKills, 100d);
            todaySkillCasts = Math.Max(todaySkillCasts, 1000d);
            todayUpgrades = Math.Max(todayUpgrades, 1000d);
            todayGold += BigDouble.FromDouble(1e6);

            totalMobKills = Math.Max(totalMobKills, 1000d);
            totalBossKills = Math.Max(totalBossKills, 100d);
            totalSkillCasts = Math.Max(totalSkillCasts, 1000d);
            totalUpgrades = Math.Max(totalUpgrades, 1000d);
            totalGold += BigDouble.FromDouble(1e6);

            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
