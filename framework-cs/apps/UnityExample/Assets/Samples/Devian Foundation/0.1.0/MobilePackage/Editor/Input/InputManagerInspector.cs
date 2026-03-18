using UnityEditor;

namespace Devian
{
    /// <summary>
    /// InputManager 커스텀 인스펙터.
    /// 설정 편집(Refresh, VirtualGamepad)은 InputSettingsInspector에서 제공한다.
    /// </summary>
    [CustomEditor(typeof(InputManager))]
    public sealed class InputManagerInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
