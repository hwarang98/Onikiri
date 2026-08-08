using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 다음 일일 리셋까지 남은 시간.
     *
     * ## 왜 필요한가
     *
     * "매일 리셋"이라고 적어두면 언제인지 알 수 없다. 자정이라고 적어도
     * **UTC 자정**이라 플레이어의 자정과 다르다(한국은 오전 9시). 남은 시간을
     * 적는 것이 시간대를 설명하지 않고도 정확한 유일한 방법이다.
     *
     * ## 초 단위로 갱신한다
     *
     * 매 프레임 문자열을 만들 이유가 없다. 표시가 분 단위로 바뀌더라도 초를
     * 함께 적으므로 1초마다는 갱신해야 한다.
     *
     * unscaled 시간을 쓴다. 히트스톱이 걸린 0.1초 동안 시계가 멈추는 것은
     * 연출이지 사실이 아니다.
     */
    public sealed class DailyResetTimer : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "초기화까지 ";

        private QuestSystem quests;
        private float tick;

        private void Start()
        {
            quests = QuestSystem.Instance;
            Refresh();
        }

        private void OnEnable()
        {
            if (quests == null) quests = QuestSystem.Instance;
            tick = 0f;
            Refresh();
        }

        private void Update()
        {
            tick += Time.unscaledDeltaTime;
            if (tick < 1f) return;

            tick = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (label == null) return;

            var left = quests != null
                ? quests.UntilDailyReset(DateTime.UtcNow)
                : TimeSpan.Zero;

            label.text = prefix + string.Format("{0:00}:{1:00}:{2:00}",
                (int)left.TotalHours, left.Minutes, left.Seconds);
        }
    }
}
