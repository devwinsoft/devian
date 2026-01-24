using UnityEngine;
using UnityEditor;
using Devian.Templates.Network;

namespace Devian.Templates.Network.Editor
{
    /// <summary>
    /// Custom Inspector for EchoWsClientSample.
    /// Provides buttons for Connect/Disconnect/Ping/Echo operations.
    /// 
    /// IMPORTANT: RequiresConstantRepaint() ensures Inspector updates when
    /// connection state changes (e.g., after Disconnect).
    /// </summary>
    [CustomEditor(typeof(EchoWsClientSample))]
    public class EchoWsClientSampleEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Enable constant repaint in Play mode to reflect connection state changes.
        /// Without this, Inspector won't update when OnClosed sets IsConnected=false.
        /// </summary>
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sample = (EchoWsClientSample)target;
            bool isPlaying = Application.isPlaying;
            bool isConnected = sample.IsConnected;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Connection buttons (only enabled in Play mode)
            EditorGUI.BeginDisabledGroup(!isPlaying);
            EditorGUILayout.BeginHorizontal();
            
            // Connect: enabled when playing and not connected
            EditorGUI.BeginDisabledGroup(isConnected);
            if (GUILayout.Button("Connect"))
            {
                sample.ConnectWithInspectorUrl();
            }
            EditorGUI.EndDisabledGroup();
            
            // Disconnect: enabled when playing and connected
            EditorGUI.BeginDisabledGroup(!isConnected);
            if (GUILayout.Button("Disconnect"))
            {
                sample.Disconnect();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            // Message buttons (only enabled in Play mode + connected)
            EditorGUI.BeginDisabledGroup(!isPlaying || !isConnected);
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
            
            // Play mode indicator
            EditorGUILayout.LabelField("Play Mode:", isPlaying ? "Active" : "Inactive");
            
            // Connection status with color
            var statusStyle = new GUIStyle(EditorStyles.label);
            if (isPlaying)
            {
                statusStyle.normal.textColor = isConnected ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            }
            EditorGUILayout.LabelField("Connected:", isConnected ? "Yes" : "No", statusStyle);
            
            // Help box when not in Play mode
            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to use Connect/Disconnect buttons.", MessageType.Info);
            }
        }
    }
}
