using UnityEngine;
using UnityEditor;
using Devian.Templates.Network;

namespace Devian.Templates.Network.Editor
{
    /// <summary>
    /// Custom Inspector for EchoWsClientSample.
    /// Provides buttons for Connect/Disconnect/Ping/Echo operations.
    /// </summary>
    [CustomEditor(typeof(EchoWsClientSample))]
    public class EchoWsClientSampleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sample = (EchoWsClientSample)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Connection buttons (always enabled)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect"))
            {
                sample.ConnectWithInspectorUrl();
            }
            if (GUILayout.Button("Disconnect"))
            {
                sample.Disconnect();
            }
            EditorGUILayout.EndHorizontal();

            // Message buttons (only enabled in Play mode + connected)
            EditorGUI.BeginDisabledGroup(!Application.isPlaying || !sample.IsConnected);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Send Ping"))
            {
                sample.SendPing();
            }
            if (GUILayout.Button("Send Echo"))
            {
                sample.SendEcho();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            // Status display
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Connected:", sample.IsConnected ? "Yes" : "No");
        }
    }
}
