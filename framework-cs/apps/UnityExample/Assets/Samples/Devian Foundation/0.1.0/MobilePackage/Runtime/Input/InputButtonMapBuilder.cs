using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Devian
{
    /// <summary>
    /// InputActionAsset에서 key("Map/Action") → button index 맵을 빌드한다.
    /// key는 ordinal sort 후 0..63 인덱스 부여. 64 초과 시 예외.
    /// </summary>
    public static class InputButtonMapBuilder
    {
        /// <summary>
        /// expectedKeys 목록으로 buttonMap을 빌드한다.
        /// </summary>
        public static Dictionary<string, int> Build(InputActionAsset asset, string[] expectedKeys)
        {
            if (expectedKeys == null || expectedKeys.Length == 0)
                return new Dictionary<string, int>();

            if (expectedKeys.Length > 64)
                throw new InvalidOperationException(
                    $"[InputButtonMapBuilder] expectedKeys.Length({expectedKeys.Length}) exceeds 64. " +
                    "ulong bitset supports max 64 buttons.");

            // Ordinal sort
            var sorted = new List<string>(expectedKeys);
            sorted.Sort(StringComparer.Ordinal);

            var map = new Dictionary<string, int>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                string key = sorted[i];

                // Validate that the action exists in the asset
                var action = TryFindActionByKey(asset, key);
                if (action == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[InputButtonMapBuilder] Action not found for key '{key}'. Skipping.");
                    continue;
                }

                map[key] = i;
            }

            return map;
        }

        /// <summary>
        /// "Map/Action" 형식의 key로 InputAction을 찾는다.
        /// </summary>
        public static InputAction TryFindActionByKey(InputActionAsset asset, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            int slashIndex = key.IndexOf('/');
            if (slashIndex < 0) return null;

            string mapName = key.Substring(0, slashIndex);
            string actionName = key.Substring(slashIndex + 1);

            var actionMap = asset.FindActionMap(mapName);
            if (actionMap == null) return null;

            return actionMap.FindAction(actionName);
        }
    }
}
