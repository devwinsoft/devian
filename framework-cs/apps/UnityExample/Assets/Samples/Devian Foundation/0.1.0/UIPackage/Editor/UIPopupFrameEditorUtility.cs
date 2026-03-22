#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    internal readonly struct UIPopupFramePrefabInfo
    {
        public UIPopupFramePrefabInfo(GameObject prefab, string assetPath, Type frameType)
        {
            Prefab = prefab;
            AssetPath = assetPath;
            FrameType = frameType;
        }

        public GameObject Prefab { get; }
        public string AssetPath { get; }
        public Type FrameType { get; }
        public string PrefabName => Prefab == null ? string.Empty : Prefab.name;
    }

    internal static class UIPopupFrameEditorUtility
    {
        private const string PopupFrameGroupKey = "UI_POPUP_FRAME_ID";

        internal static bool TryResolvePopupFrameType(string typeName, out Type type)
        {
            type = null;

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            type = Type.GetType(typeName, throwOnError: false);
            if (IsValidPopupFrameType(type))
            {
                return true;
            }

            var popupTypes = TypeCache.GetTypesDerivedFrom<UIPopupFrameBase>();
            foreach (var candidate in popupTypes)
            {
                if (!IsValidPopupFrameType(candidate))
                {
                    continue;
                }

                var shortAssemblyName = candidate.Assembly.GetName().Name;
                var shortTypeName = string.IsNullOrWhiteSpace(candidate.FullName)
                    ? candidate.Name
                    : $"{candidate.FullName}, {shortAssemblyName}";

                if (string.Equals(candidate.AssemblyQualifiedName, typeName, StringComparison.Ordinal)
                    || string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                    || string.Equals(shortTypeName, typeName, StringComparison.Ordinal)
                    || string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                {
                    type = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryAutoFillEntry(SerializedProperty entryProperty, out string message)
        {
            message = string.Empty;

            if (!TryGetEntryProperties(entryProperty, out var frameTypeNameProp, out var frameIdValueProp))
            {
                message = "[UIPopupFrameEditor] Invalid popup frame mapping entry.";
                return false;
            }

            var hasTypeName = !string.IsNullOrWhiteSpace(frameTypeNameProp.stringValue);
            var hasFrameId = !string.IsNullOrWhiteSpace(frameIdValueProp.stringValue);

            if (!hasTypeName && !hasFrameId)
            {
                message = "[UIPopupFrameEditor] Auto Fill needs either Frame Type or Frame Id.";
                return false;
            }

            if (!TryCollectPopupFramePrefabs(out var prefabs, out message))
            {
                return false;
            }

            if (hasTypeName && hasFrameId)
            {
                if (!TryResolvePopupFrameType(frameTypeNameProp.stringValue, out var currentType))
                {
                    message = $"[UIPopupFrameEditor] Invalid popup frame type '{frameTypeNameProp.stringValue}'.";
                    return false;
                }

                if (!TryFindPrefabInfoByFrameId(prefabs, frameIdValueProp.stringValue, out var frameInfo, out message))
                {
                    return false;
                }

                if (frameInfo.FrameType != currentType)
                {
                    message =
                        $"[UIPopupFrameEditor] Mapping mismatch: '{frameIdValueProp.stringValue}' resolves to " +
                        $"'{frameInfo.FrameType.FullName}', but entry has '{currentType.FullName}'.";
                    return false;
                }

                message = "[UIPopupFrameEditor] Popup mapping is already filled.";
                return false;
            }

            entryProperty.serializedObject.Update();

            if (hasTypeName)
            {
                if (!TryResolvePopupFrameType(frameTypeNameProp.stringValue, out var frameType))
                {
                    message = $"[UIPopupFrameEditor] Invalid popup frame type '{frameTypeNameProp.stringValue}'.";
                    return false;
                }

                if (!TryFindPrefabInfoByType(prefabs, frameType, out var frameInfo, out message))
                {
                    return false;
                }

                frameIdValueProp.stringValue = frameInfo.PrefabName;
                entryProperty.serializedObject.ApplyModifiedProperties();
                message = $"[UIPopupFrameEditor] Filled Frame Id with '{frameInfo.PrefabName}'.";
                return true;
            }

            if (!TryFindPrefabInfoByFrameId(prefabs, frameIdValueProp.stringValue, out var resolvedInfo, out message))
            {
                return false;
            }

            frameTypeNameProp.stringValue = resolvedInfo.FrameType.AssemblyQualifiedName ?? resolvedInfo.FrameType.FullName ?? string.Empty;
            entryProperty.serializedObject.ApplyModifiedProperties();
            message = $"[UIPopupFrameEditor] Filled Frame Type with '{resolvedInfo.FrameType.FullName}'.";
            return true;
        }

        internal static int AutoFillMissingMappings(SerializedProperty mappingsProperty, out string message)
        {
            message = string.Empty;

            if (mappingsProperty == null || !mappingsProperty.isArray)
            {
                message = "[UIPopupFrameEditor] PopupFrameMappings property is invalid.";
                return 0;
            }

            if (!TryCollectPopupFramePrefabs(out var prefabs, out message))
            {
                return 0;
            }

            var changedCount = 0;
            var addedCount = 0;
            var warningMessages = new List<string>();

            mappingsProperty.serializedObject.Update();

            for (var i = 0; i < mappingsProperty.arraySize; i++)
            {
                var entryProperty = mappingsProperty.GetArrayElementAtIndex(i);
                if (TryGetEntryProperties(entryProperty, out var frameTypeNameProp, out var frameIdValueProp))
                {
                    var hasTypeName = !string.IsNullOrWhiteSpace(frameTypeNameProp.stringValue);
                    var hasFrameId = !string.IsNullOrWhiteSpace(frameIdValueProp.stringValue);

                    if (hasTypeName && hasFrameId)
                    {
                        continue;
                    }

                    if (TryAutoFillEntry(entryProperty, out var localMessage))
                    {
                        changedCount++;
                    }
                    else if (!string.IsNullOrWhiteSpace(localMessage))
                    {
                        warningMessages.Add(localMessage);
                    }
                }
            }

            var existingTypeNames = new HashSet<string>(StringComparer.Ordinal);
            var existingFrameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < mappingsProperty.arraySize; i++)
            {
                var entryProperty = mappingsProperty.GetArrayElementAtIndex(i);
                if (!TryGetEntryProperties(entryProperty, out var frameTypeNameProp, out var frameIdValueProp))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(frameTypeNameProp.stringValue))
                {
                    existingTypeNames.Add(frameTypeNameProp.stringValue);
                }

                if (!string.IsNullOrWhiteSpace(frameIdValueProp.stringValue))
                {
                    existingFrameIds.Add(frameIdValueProp.stringValue);
                }
            }

            foreach (var info in GetAutoFillCandidates(prefabs))
            {
                var typeName = info.FrameType.AssemblyQualifiedName ?? info.FrameType.FullName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                if (existingTypeNames.Contains(typeName) || existingFrameIds.Contains(info.PrefabName))
                {
                    continue;
                }

                var insertIndex = mappingsProperty.arraySize;
                mappingsProperty.InsertArrayElementAtIndex(insertIndex);

                var entryProperty = mappingsProperty.GetArrayElementAtIndex(insertIndex);
                if (!TryGetEntryProperties(entryProperty, out var frameTypeNameProp, out var frameIdValueProp))
                {
                    continue;
                }

                frameTypeNameProp.stringValue = typeName;
                frameIdValueProp.stringValue = info.PrefabName;

                existingTypeNames.Add(typeName);
                existingFrameIds.Add(info.PrefabName);
                addedCount++;
            }

            mappingsProperty.serializedObject.ApplyModifiedProperties();

            message = $"[UIPopupFrameEditor] Auto Fill completed. Filled: {changedCount}, Added: {addedCount}.";
            if (warningMessages.Count > 0)
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, warningMessages.Distinct());
            }

            return changedCount + addedCount;
        }

        private static bool TryCollectPopupFramePrefabs(out List<UIPopupFramePrefabInfo> prefabs, out string message)
        {
            prefabs = new List<UIPopupFramePrefabInfo>();
            message = string.Empty;

            if (!TryGetPopupFrameSearchDir(out var searchDir, out message))
            {
                return false;
            }

            var prefabAssets = AssetManager.FindPrefabs(searchDir);
            foreach (var prefab in prefabAssets)
            {
                if (prefab == null || string.IsNullOrWhiteSpace(prefab.name) || prefab.name.StartsWith("@"))
                {
                    continue;
                }

                Type poolType;
                try
                {
                    poolType = BundlePoolFactory.Instance.GetPoolType(prefab);
                }
                catch
                {
                    continue;
                }

                if (!IsValidPopupFrameType(poolType))
                {
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(prefab);
                prefabs.Add(new UIPopupFramePrefabInfo(prefab, assetPath, poolType));
            }

            if (prefabs.Count == 0)
            {
                message = $"[UIPopupFrameEditor] No popup frame prefabs found in '{searchDir}'.";
                return false;
            }

            return true;
        }

        private static bool TryFindPrefabInfoByType(
            IReadOnlyList<UIPopupFramePrefabInfo> prefabs,
            Type frameType,
            out UIPopupFramePrefabInfo info,
            out string message)
        {
            info = default;
            message = string.Empty;

            var matches = prefabs.Where(p => p.FrameType == frameType).ToList();
            if (matches.Count == 1)
            {
                info = matches[0];
                return true;
            }

            if (matches.Count == 0)
            {
                message = $"[UIPopupFrameEditor] No popup prefab found for '{frameType.FullName}'.";
                return false;
            }

            message =
                $"[UIPopupFrameEditor] Multiple popup prefabs found for '{frameType.FullName}': " +
                string.Join(", ", matches.Select(m => $"{m.PrefabName} ({m.AssetPath})"));
            return false;
        }

        private static bool TryFindPrefabInfoByFrameId(
            IReadOnlyList<UIPopupFramePrefabInfo> prefabs,
            string frameId,
            out UIPopupFramePrefabInfo info,
            out string message)
        {
            info = default;
            message = string.Empty;

            var matches = prefabs
                .Where(p => string.Equals(p.PrefabName, frameId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
            {
                info = matches[0];
                return true;
            }

            if (matches.Count == 0)
            {
                message = $"[UIPopupFrameEditor] No popup prefab found for id '{frameId}'.";
                return false;
            }

            message =
                $"[UIPopupFrameEditor] Multiple popup prefabs share id '{frameId}': " +
                string.Join(", ", matches.Select(m => $"{m.AssetPath}"));
            return false;
        }

        private static IEnumerable<UIPopupFramePrefabInfo> GetAutoFillCandidates(IReadOnlyList<UIPopupFramePrefabInfo> prefabs)
        {
            var duplicateTypes = new HashSet<Type>(
                prefabs.GroupBy(p => p.FrameType).Where(g => g.Count() > 1).Select(g => g.Key));

            var duplicatePrefabNames = new HashSet<string>(
                prefabs
                    .GroupBy(p => p.PrefabName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key),
                StringComparer.OrdinalIgnoreCase);

            foreach (var info in prefabs)
            {
                if (duplicateTypes.Contains(info.FrameType))
                {
                    continue;
                }

                if (duplicatePrefabNames.Contains(info.PrefabName))
                {
                    continue;
                }

                yield return info;
            }
        }

        private static bool TryGetPopupFrameSearchDir(out string searchDir, out string message)
        {
            searchDir = string.Empty;
            message = string.Empty;

            var settings = AssetDatabase.LoadAssetAtPath<UISettings>(UISettings.DefaultResourcesAssetPath);
            if (settings == null)
            {
                message = $"[UIPopupFrameEditor] UISettings not found at '{UISettings.DefaultResourcesAssetPath}'.";
                return false;
            }

            searchDir = settings.GetSearchDir(PopupFrameGroupKey);
            if (string.IsNullOrWhiteSpace(searchDir))
            {
                message = $"[UIPopupFrameEditor] SearchDir for '{PopupFrameGroupKey}' is empty.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(searchDir))
            {
                message = $"[UIPopupFrameEditor] SearchDir '{searchDir}' does not exist.";
                return false;
            }

            return true;
        }

        private static bool TryGetEntryProperties(
            SerializedProperty entryProperty,
            out SerializedProperty frameTypeNameProp,
            out SerializedProperty frameIdValueProp)
        {
            frameTypeNameProp = entryProperty?.FindPropertyRelative(nameof(UIPopupFrameMapEntry.FrameTypeName));
            var frameIdProp = entryProperty?.FindPropertyRelative(nameof(UIPopupFrameMapEntry.FrameId));
            frameIdValueProp = frameIdProp?.FindPropertyRelative(nameof(UI_POPUP_FRAME_ID.Value));
            return frameTypeNameProp != null && frameIdValueProp != null;
        }

        private static bool IsValidPopupFrameType(Type type)
        {
            return type != null
                && typeof(UIPopupFrameBase).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition;
        }
    }
}

#endif
