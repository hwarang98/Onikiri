using System.Globalization;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 장착 슬롯 칩 위의 쿨타임 표시 (슬레이어식).
     *
     * ## 자기 타이머가 없다
     *
     * 매 프레임 SkillSystem의 실제 남은 시간(SecondsUntilCast)을 읽어 그대로
     * 그린다. UI가 따로 시계를 돌리면 실제 시전과 어긋나는 순간이 반드시
     * 온다 - 이 게임의 쿨다운은 벨 것이 있을 때만 돌고(SkillSystem.Update),
     * 히트스톱 중에는 얼며(스케일 타임), 테스트 패널의 즉시 시전은 타이머를
     * 통째로 되감는다. 그 셋을 UI 시계가 전부 따라 하는 것보다 원본을 읽는
     * 것이 짧고 정확하다. 자동/수동(테스트 패널) 시전이 같은 타이머를 쓰므로
     * 표시도 저절로 같다.
     *
     * ## 매 프레임 도는 비용
     *
     * 폴링이지만 프레임당 하는 일은 정수 비교 몇 개다. 문자열은 0.1초 단위가
     * **바뀔 때만** 만든다(초당 최대 10번) - 쿨다운 표시 네 칸이 전투 프레임을
     * 갉아먹지 않아야 한다는 규칙은 진행 줄·가이드 카드와 같다.
     *
     * ## 상태
     *
     *   쿨다운 중   그늘(아이콘 전체) + 마스크(남은 비율만큼 위에서 덮음)
     *               + 가운데 숫자("12.1" - 소수 한 자리 고정)
     *   사용 가능   셋 다 즉시 꺼진다. 아이콘이 원래 밝기로 돌아온다
     *   빈 칸·잠김  표시 없음 (칩의 자물쇠/빈 칸 그림이 그대로 말한다)
     *
     * 장착이 바뀌면(프리셋) EquippedAt을 매 프레임 읽으므로 다음 프레임에
     * 곧바로 새 오의의 값으로 갱신된다.
     */
    public sealed class SkillCooldownOverlay : MonoBehaviour
    {
        [SerializeField] private SkillSystem system;
        [SerializeField] private int slot;

        [Tooltip("쿨다운 동안 아이콘 전체를 살짝 어둡게 누르는 판")]
        [SerializeField] private Image shade;

        [Tooltip("남은 비율만큼 위에서 내려 덮는 마스크. 앵커 높이로 그린다 - " +
                 "Filled는 스프라이트가 필요하고, 소프트 가장자리는 그라데이션으로 " +
                 "읽힌다(경험치 스트립과 같은 규칙)")]
        [SerializeField] private RectTransform mask;
        [SerializeField] private Image maskImage;

        [Tooltip("남은 초. 소수 한 자리 고정(개선안: 1초 미만도 0.8처럼)")]
        [SerializeField] private TMP_Text label;

        /** 지금 그려져 있는 0.1초 단위 값. 이것이 바뀔 때만 문자열을 만든다 */
        private int shownTenths = int.MinValue;

        /** null = 아직 한 번도 안 그렸다. 첫 Refresh가 어느 상태든 반드시 적용된다 */
        private bool? shownVisible;

        private void OnEnable()
        {
            // 씬에 저장된 상태와 무관하게 첫 프레임에 실제 값으로 선다
            shownTenths = int.MinValue;
            shownVisible = null;
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            int index = system != null && !system.IsSlotLocked(slot)
                ? system.EquippedAt(slot) : -1;

            float remaining = index >= 0 ? system.SecondsUntilCast(index) : 0f;
            bool cooling = remaining > 0f;

            if (shownVisible != cooling)
            {
                shownVisible = cooling;
                if (shade != null) shade.enabled = cooling;
                if (maskImage != null) maskImage.enabled = cooling;
                if (label != null) label.enabled = cooling;
                if (!cooling) shownTenths = int.MinValue;
            }

            if (!cooling) return;

            // 마스크는 남은 비율만큼 위에서 덮는다. 시간이 갈수록 아래에서부터
            // 아이콘이 드러난다 - 앵커 갱신은 값이 같으면 유니티가 걸러주지
            // 않으므로 직접 비교할 것도 없이 싼 대입이다
            if (mask != null)
            {
                float remainFraction = 1f - system.CooldownFraction(index);
                mask.anchorMin = new Vector2(0f, 1f - remainFraction);
                mask.anchorMax = Vector2.one;
                mask.offsetMin = Vector2.zero;
                mask.offsetMax = Vector2.zero;
            }

            // 올림으로 잰다. 0.01초가 남았는데 "0.0"이 서 있으면 표시가
            // "끝났다"고 거짓말하는 한 프레임이 생긴다
            int tenths = Mathf.CeilToInt(remaining * 10f);
            if (tenths == shownTenths || label == null) return;

            shownTenths = tenths;
            label.text = (tenths * 0.1f).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
