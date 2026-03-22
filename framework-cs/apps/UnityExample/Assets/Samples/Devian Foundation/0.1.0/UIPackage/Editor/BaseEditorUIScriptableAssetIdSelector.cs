#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UI 전용 ScriptableObject AssetId selector base class.
    /// SearchDir을 UISettings.GetSearchDir()에서 조회한다.
    /// </summary>
    public abstract class BaseEditorUIScriptableAssetIdSelector<TAsset> : BaseEditorScriptableAssetIdSelector<TAsset>
        where TAsset : ScriptableObject
    {
        protected override string ResolveSearchDir(string groupKey)
        {
            var uiSettings = AssetDatabase.LoadAssetAtPath<UISettings>(UISettings.DefaultResourcesAssetPath);
            if (uiSettings == null)
            {
                Debug.LogWarning($"[UIAssetId] UISettings not found at '{UISettings.DefaultResourcesAssetPath}'. Using fallback searchDir: Assets");
                return "Assets";
            }

            var dir = uiSettings.GetSearchDir(groupKey);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return "Assets";
            }

            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogWarning($"[UIAssetId] SearchDir '{dir}' does not exist. Using fallback: Assets");
                return "Assets";
            }

            return dir;
        }
    }
}

#endif
