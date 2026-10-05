using System.Collections.Generic;
using Onikiri.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief **화면을 덮는 것은 전부 팝업 층위(20) 위에 그린다** - 70단계.
     *
     * 66단계의 포인트 초기화 팝업(`StatResetPopup`)이 층위 캔버스 없이 세워져
     * 바탕(0)에 그려졌고, 층위 10인 가이드 카드 · EXP 띠가 그 본문 위에
     * 올라왔다(70단계 실기). 귀문 진입 안내 · 결과(`TrialNotice` · `TrialResult`)도
     * 같은 모양이었다. 빌더마다 `RaiseToLayer`를 기억해야 하는 구조라 다음
     * 팝업도 같은 자리에서 빠질 수 있다 - 그래서 **씬 쪽에서 한 규칙으로** 잰다.
     *
     * 팝업으로 보는 것 (SafeArea 직속 자식):
     *   - `PopupPanel`을 가졌다 (PopupBuilder가 세운 것)
     *   - 이름이 "Popup"으로 끝난다
     *   - 화면 전체를 덮는 이미지다 (앵커 0~1 · 여백 0) - 배경(`PanelBackdrop`)은 뺀다
     *
     * 빌더는 경고로(`LogProblems`), EditMode 검사는 실패로 쓴다(`PopupLayerTests`).
     */
    public static class PopupLayerAudit
    {
        /** 화면 전체를 덮지만 팝업이 아닌 것 - 패널들 뒤의 배경 */
        private static readonly string[] NotPopups = { "PanelBackdrop" };

        public static List<string> Problems(Transform safeArea)
        {
            var problems = new List<string>();
            if (safeArea == null) return problems;

            foreach (Transform child in safeArea)
            {
                if (!IsPopup(child)) continue;

                var canvas = child.GetComponent<Canvas>();
                int order = canvas != null && canvas.overrideSorting ? canvas.sortingOrder : 0;
                if (order < DisplayConfig.SortingPopup)
                    problems.Add(string.Format("{0} draws at layer {1} (needs >= {2}) - "
                                               + "BattleContentBuilder.RaiseToLayer(root, DisplayConfig.SortingPopup, true)",
                                               child.name, order, DisplayConfig.SortingPopup));
            }
            return problems;
        }

        public static bool IsPopup(Transform child)
        {
            if (System.Array.IndexOf(NotPopups, child.name) >= 0) return false;
            if (child.GetComponent<Onikiri.UI.PopupPanel>() != null) return true;
            if (child.name.EndsWith("Popup")) return true;

            var rect = child as RectTransform;
            if (rect == null || child.GetComponent<Image>() == null) return false;
            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
                   && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
        }

        /** 빌더 끝에서 부른다. 문제가 있으면 경고 한 줄씩 */
        public static void LogProblems(Transform safeArea)
        {
            foreach (var problem in Problems(safeArea))
                Debug.LogWarning("[Onikiri] Popup layer: " + problem);
        }
    }
}
