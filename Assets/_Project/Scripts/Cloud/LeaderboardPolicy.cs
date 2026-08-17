using Onikiri.Progression;

namespace Onikiri.Cloud
{
    /**
     * @brief 언제 · 무엇을 제출할지 정하는 규칙. **Firebase가 한 줄도 안 들어온다.**
     *
     * 갈라 둔 이유는 이 규칙이 네트워크의 성질이 아니라 게임의 성질이기 때문이다.
     * "낮은 값이 기록을 덮으면 안 된다"와 "층마다 쓰면 안 된다"는 Firestore를
     * 다른 것으로 바꿔도 그대로 참이고, 여기 있어야 EditMode 테스트가 Firebase
     * 없이 그것을 검사할 수 있다(CloudScores는 실기 logcat으로만 증명된다).
     */
    public static class LeaderboardPolicy
    {
        /**
         * @brief 도달층이 오른 뒤 이만큼 조용하면 제출한다 (초).
         *
         * 층마다 제출하면 안 되는 이유는 요금이 아니라 **모양**이다. 후반 한 시간에
         * 수백 층이 오르는 구간이 있고(52단계 도달층 계약), 그때마다 쓰기가 나가면
         * 서버 문서의 updatedAt이 초 단위로 갱신되어 동점 tie-break("먼저 도달한
         * 사람이 위")가 사실상 "가장 최근에 논 사람이 아래"가 된다.
         *
         * 20초인 근거: 이 게임에서 20초는 보통 보스 한 번의 길이라, 연속으로
         * 오르는 구간에서는 한 덩어리로 묶이고 띄엄띄엄 오르는 구간에서는
         * 층마다 한 번씩 나간다. 정확한 값이 중요한 자리가 아니다 - 중요한 것은
         * "0이 아니다"와 "사람이 기다린다고 느낄 만큼 길지 않다" 둘뿐이다.
         */
        public const float DebounceSeconds = 20f;

        /**
         * @brief 이 값을 서버에 올려도 되는가 (로컬 새니티).
         *
         * 서버 규칙도 같은 범위를 검사한다(firestore.rules 4-B). 두 곳에 적는
         * 이유는 역할이 다르기 때문이다 - 여기는 "실수로 이상한 값을 보내지
         * 않는다"이고, 규칙은 "보내도 안 받는다"다. 조작 클라는 이 코드를
         * 지나지 않으므로 규칙 쪽만이 강제다.
         */
        public static bool IsSubmittable(int stage)
        {
            return stage >= 1 && stage <= StageProgress.ReachSanityCap;
        }

        /**
         * @brief 서버에 있는 값을 이 값으로 덮어써도 되는가.
         *
         * **후퇴는 거부한다.** 재설치로 세이브가 사라졌거나 낮은 스테이지에서
         * 파밍 중인 기기가 기록을 덮으면 도달층의 단조성(52단계)이 서버에서만
         * 깨진다 - 로컬은 안 내려가는데 랭킹은 내려가는 상태가 된다.
         *
         * 같은 값도 거부한다. 쓸 이유가 없고, 쓰면 updatedAt만 갱신되어 동점
         * tie-break에서 자기 순위를 스스로 내린다.
         *
         * @param hasServerValue 서버에 문서가 이미 있는가 (없으면 첫 제출)
         */
        public static bool ShouldSubmit(int local, int server, bool hasServerValue)
        {
            return ShouldSubmit(local, server, hasServerValue, false);
        }

        /**
         * @brief 이름이 서버와 다르면 도달층이 안 올라도 보낸다.
         *
         * 이것이 없으면 **이름을 바꿔도 랭킹표에는 옛 이름이 남는다** - 다음
         * 제출이 도달층 상승에 묶여 있고, 최전선에 오래 머무는 사람에게 그
         * 순간은 며칠 뒤이거나 영영 오지 않는다. 실기에서 잡은 결함이다
         * (도달층 3에서 이름을 바꿨는데 서버는 계속 옛 이름이었다).
         *
         * 이 경로가 후퇴 방어를 뚫지 않는다는 것이 중요하다 - 보내는 도달층은
         * 여전히 로컬 값이고, 서버 규칙의 단조 검사가 그대로 걸린다. 로컬이
         * 낮으면 이름만 바꾸려 해도 거부된다(그것이 맞다 - 낮은 값을 들고
         * 있는 기기는 그 문서의 주인이라 말할 자격이 이미 없다).
         */
        public static bool ShouldSubmit(int local, int server, bool hasServerValue, bool nameIsStale)
        {
            if (!IsSubmittable(local)) return false;
            if (!hasServerValue) return true;
            if (local > server) return true;

            // 같은 값일 때만 이름을 이유로 쓴다. 로컬이 더 낮으면 쓰지 않는다 -
            // 규칙이 어차피 막고, 막히는 쓰기를 보내는 것은 요금만 쓴다
            return nameIsStale && local == server;
        }
    }
}
