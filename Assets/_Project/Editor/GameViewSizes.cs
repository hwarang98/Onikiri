using System;
using System.Reflection;
using UnityEditor;

namespace Onikiri.EditorTools
{
    /**
     * @brief 게임 뷰 해상도를 코드로 바꾼다.
     *
     * 유니티가 공개 API를 주지 않아 리플렉션으로 접근한다. 그래도 필요한 이유는
     * 이 게임의 레이아웃이 전부 실제 화면 크기에서 계산되기 때문이다. 카메라를
     * RenderTexture에 직접 그려 확인하면 레이아웃은 게임 뷰 비율로 계산된 채라
     * 프레이밍이 어긋난 그림이 나오고, 그것을 버그로 착각하게 된다.
     *
     * CropFrame.None이라 세로가 긴 기기일수록 월드가 더 보인다. 9:16 / 9:19.5 / 9:21
     * 세 비율에서 지면선과 UI 밴드가 유지되는지는 실제로 그 크기로 띄워봐야 안다.
     */
    public static class GameViewSizes
    {
        /**
         * @brief 해당 해상도를 선택한다. 목록에 없으면 커스텀으로 추가한다.
         *
         * 성공 여부를 반환한다. 리플렉션이라 유니티 버전이 바뀌면 조용히 실패할 수
         * 있으므로, 실패를 삼키지 않고 경고로 남긴다.
         */
        public static bool Select(int width, int height, string displayName)
        {
            try
            {
                var editorAssembly = typeof(Editor).Assembly;
                var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                var sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                var singletonType = editorAssembly
                    .GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(sizesType);

                var instance = singletonType
                    .GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null, null);

                var groupType = sizesType.GetProperty("currentGroupType").GetValue(instance, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)groupType });

                int builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                int custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                var getSize = group.GetType().GetMethod("GetGameViewSize");

                int index = -1;
                for (int i = 0; i < builtin + custom; i++)
                {
                    var size = getSize.Invoke(group, new object[] { i });
                    if ((int)sizeType.GetProperty("width").GetValue(size, null) == width &&
                        (int)sizeType.GetProperty("height").GetValue(size, null) == height)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    var fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                    var constructor = sizeType.GetConstructor(
                        new[] { sizeKindType, typeof(int), typeof(int), typeof(string) });
                    var newSize = constructor.Invoke(new[] { fixedResolution, (object)width, height, displayName });

                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    index = builtin + custom;
                }

                var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                gameViewType
                    .GetMethod("SizeSelectionCallback",
                               BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(gameView, new object[] { index, null });
                gameView.Repaint();

                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Onikiri] Could not switch the game view to " + width + "x" + height +
                    " - Unity's internal API may have changed. " + exception.Message);
                return false;
            }
        }
    }
}
