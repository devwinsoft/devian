#nullable enable
using UnityEngine;
using UnityEditor;

namespace Devian
{
    /// <summary>
    /// Editor menu for one-click Network sample setup.
    /// Creates NetTickRunner and GameNetworkClientSample in the current scene.
    /// </summary>
    public static class NetworkSampleMenu
    {
        private const string MenuPath = "Devian/Samples/Network/Create Sample Setup";
        private const string DefaultUrl = "ws://localhost:8080";

        [MenuItem(MenuPath)]
        public static void CreateSampleSetup()
        {
            // 1. Find or create NetTickRunner
            var runner = Object.FindAnyObjectByType<NetTickRunner>();
            GameObject? runnerGo = null;

            if (runner == null)
            {
                runnerGo = new GameObject("Devian.NetTickRunner");
                runner = runnerGo.AddComponent<NetTickRunner>();
                Undo.RegisterCreatedObjectUndo(runnerGo, "Create NetTickRunner");
                Debug.Log("[NetworkSampleMenu] Created NetTickRunner.");
            }
            else
            {
                Debug.Log("[NetworkSampleMenu] NetTickRunner already exists in scene.");
            }

            // 2. Create GameNetworkClientSample
            var sampleGo = new GameObject("Devian.GameNetworkClientSample");
            var sample = sampleGo.AddComponent<GameNetworkClientSample>();
            Undo.RegisterCreatedObjectUndo(sampleGo, "Create GameNetworkClientSample");

            // 3. Set default URL via SerializedObject
            SetDefaultUrl(sample);

            // 4. Select the sample object
            Selection.activeGameObject = sampleGo;

            Debug.Log($"[NetworkSampleMenu] Created GameNetworkClientSample. Use menu Devian/Samples/Network or Play to test.");
        }

        private static void SetDefaultUrl(GameNetworkClientSample sample)
        {
            var so = new SerializedObject(sample);

            // Try common field names for URL
            var urlProp = so.FindProperty("_url");
            if (urlProp == null)
                urlProp = so.FindProperty("url");
            if (urlProp == null)
                urlProp = so.FindProperty("m_Url");

            if (urlProp != null && urlProp.propertyType == SerializedPropertyType.String)
            {
                urlProp.stringValue = DefaultUrl;
                so.ApplyModifiedProperties();
                Debug.Log($"[NetworkSampleMenu] Set URL to: {DefaultUrl}");
            }
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateCreateSampleSetup()
        {
            // Always enabled (can create multiple samples if needed)
            return true;
        }
    }
}
