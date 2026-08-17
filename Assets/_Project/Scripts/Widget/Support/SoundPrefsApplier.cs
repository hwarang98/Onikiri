using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 씬이 뜰 때 저장된 음소거를 적용한다 (37단계).
     *
     * 설정 판은 꺼진 채 저장되므로 그쪽 Start가 돌지 않는다. 항상 켜져 있는
     * Battle 루트에 이것이 붙어 판 대신 적용한다.
     */
    public sealed class SoundPrefsApplier : MonoBehaviour
    {
        private void Start()
        {
            SettingsPanel.ApplySavedVolume();
        }
    }
}
