using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 도달층이 오르면 랭킹에 자동으로 올린다. **게임에 대한 영향은 0이다.**
     *
     * 스파이크(53단계)에서는 디버그 버튼만이 제출 경로였다. 리더보드가 서려면
     * 사람이 누르지 않아도 올라가야 하는데, 그렇다고 층마다 쓰면 안 된다 -
     * 이 컴포넌트가 그 사이를 맡는다.
     *
     * ## 세 가지 계기로만 제출한다
     *
     *   갱신 후 조용해지면   도달층이 오르고 DebounceSeconds 동안 더 안 오르면
     *   앱이 뒤로 갈 때      OnApplicationPause(true) - 모바일의 실제 종료다
     *   종료                 OnApplicationQuit (에디터·데스크톱)
     *
     * 두 번째가 특히 중요하다. 방치형은 홈 버튼으로 나가는 것이 정상 종료이고
     * (GameSession 머리 주석), 그 순간을 놓치면 마지막 구간의 진행이 다음
     * 실행까지 랭킹에 안 보인다.
     *
     * ## 실패는 전부 조용하다
     *
     * 오프라인이면 Firestore가 로컬 큐에 담고 복귀할 때 보낸다. 실패해도 게임은
     * 아무것도 모른 채 계속 돈다 - 랭킹은 곁다리이고, 곁다리가 본체를 멈추면
     * 그건 기능이 아니라 사고다.
     */
    [DefaultExecutionOrder(50)]
    public sealed class LeaderboardSubmitter : MonoBehaviour
    {
        [SerializeField] private StageProgress progress;

        [Tooltip("도달층이 오른 뒤 이만큼 조용하면 제출한다 (초)")]
        [SerializeField] private float debounceSeconds = LeaderboardPolicy.DebounceSeconds;

        /**
         * @brief 마지막으로 제출을 **시도한** 도달층.
         *
         * "성공한"이 아니다. 실패를 기억하면 오프라인 구간에서 매 갱신마다
         * 같은 값으로 다시 시도하게 되고, Firestore는 그것을 전부 큐에 쌓는다.
         * 성공 여부는 서버가 알고(높을 때만 쓰기), 이쪽은 "같은 값을 두 번
         * 시도하지 않는다"만 지키면 된다.
         */
        private int lastAttempted;

        /** 남은 디바운스. 0 이하이면 대기 중인 제출이 없다 */
        private float pending;

        private void Awake()
        {
            if (progress == null) progress = StageProgress.Instance;
        }

        private void OnEnable()
        {
            if (progress == null) progress = StageProgress.Instance;
            if (progress != null) progress.Changed += OnProgressChanged;

            // 개명도 제출 계기다. 도달층 상승에만 묶어두면 최전선에 머무는
            // 사람의 새 이름이 랭킹표에 영영 안 닿는다(실기에서 잡은 결함)
            PlayerProfile.Changed += OnNameChanged;

            // 계정 복구(55단계)로 문서가 갈리는 순간. 기억을 안 지우면 새
            // 계정의 문서에는 이번 실행의 진행이 한 번도 안 나간다 - 옛 uid로
            // 이미 보냈던 값이라 "같은 값 두 번 금지"에 걸린다
            CloudScores.UidChanged += OnUidChanged;
        }

        private void OnDisable()
        {
            if (progress != null) progress.Changed -= OnProgressChanged;
            PlayerProfile.Changed -= OnNameChanged;
            CloudScores.UidChanged -= OnUidChanged;
        }

        /**
         * @brief uid가 바뀌었다. 기억만 지우고 **제출은 안 한다**.
         *
         * 여기서 곧바로 보내면 복구 직후 병합 쓰기와 겹친다(AccountLink가
         * 병합값으로 이미 조건부 제출을 부른다). 둘 다 나가면 하나는 규칙에
         * 막히고, 막히는 쓰기를 보내는 것은 요금만 쓴다.
         */
        private void OnUidChanged()
        {
            lastAttempted = 0;
        }

        /**
         * @brief 이름이 바뀌었다. 같은 도달층이라도 한 번 더 보낸다.
         *
         * lastAttempted를 지우는 이유는 그 기억이 "같은 값을 두 번 안 보낸다"를
         * 위한 것이기 때문이다 - 이름이 바뀐 지금은 같은 값을 보내는 것이 바로
         * 목적이다. 실제로 쓸지는 서버 값과 비교해 CloudScores가 정한다.
         */
        private void OnNameChanged()
        {
            lastAttempted = 0;
            pending = debounceSeconds;
        }

        private void OnProgressChanged()
        {
            if (progress == null) return;
            if (progress.MaxStageReached <= lastAttempted) return;

            // 창을 **다시 연다**. 연속으로 오르는 구간에서는 마지막 상승에서
            // 20초를 세게 되어 한 덩어리가 한 번의 쓰기로 묶인다
            pending = debounceSeconds;
        }

        private void Update()
        {
            if (pending <= 0f) return;

            // 배속 치트(테스트 패널)와 히트스톱이 timeScale을 흔든다. 제출
            // 간격은 게임 시간이 아니라 사람의 시간이라 unscaled가 맞다
            pending -= Time.unscaledDeltaTime;
            if (pending > 0f) return;

            pending = 0f;
            Submit("도달층 갱신");
        }

        private void OnApplicationPause(bool paused)
        {
            // 복귀(paused=false)에서는 보내지 않는다. 방금 돌아온 시점의
            // 도달층은 나갈 때와 같고, 같은 값을 다시 미는 것은 요금만 쓴다
            if (paused) Submit("앱 이탈");
        }

        private void OnApplicationQuit()
        {
            Submit("종료");
        }

        /**
         * @brief 지금 도달층을 올린다. 이미 시도한 값이면 아무것도 안 한다.
         *
         * public인 이유는 테스트 패널이 "지금 당장"을 눌러볼 수 있어야 하기
         * 때문이다 - 디바운스 20초를 기다려야만 확인되는 기능은 확인 비용이
         * 확인보다 커진다(테스트 패널 머리 주석).
         */
        public void Submit(string reason)
        {
            if (progress == null) progress = StageProgress.Instance;
            if (progress == null) return;

            int stage = progress.MaxStageReached;
            if (stage <= lastAttempted) return;
            if (!LeaderboardPolicy.IsSubmittable(stage)) return;

            lastAttempted = stage;
            pending = 0f;

            Debug.Log("[Leaderboard] 제출 시도 (" + reason + "): 도달층 " + stage);

            // 결과를 기다리지 않는다. 이 줄 뒤에 게임의 어떤 것도 걸려 있지
            // 않아야 한다 - CloudScores가 예외를 전부 안에서 잡는다
            var _ = CloudScores.SubmitIfHigherAsync(stage);
        }

        /** 테스트 패널의 "다시 제출". 같은 값을 한 번 더 밀 수 있게 기억을 지운다 */
        public void ForgetLastAttempt()
        {
            lastAttempted = 0;
        }
    }
}
