using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(InventorySettings))]
    public sealed class InventorySettingsEditor : Editor
    {
        const string ApplicationPrefabPath = "Assets/Resources/Devian/Application.prefab";

        int _maxStamina = 30;
        int _staminaIntervalSeconds = 300;

        string _statusMessage = string.Empty;
        MessageType _statusType = MessageType.Info;
        bool _loaded;

        void OnEnable()
        {
            _loadFromAsset();
        }

        public override void OnInspectorGUI()
        {
            if (!_loaded)
                _loadFromAsset();

            var setting = target as InventorySettings;
            if (setting == null)
                return;

            serializedObject.Update();

            EditorGUILayout.LabelField("Inventory Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _maxStamina = EditorGUILayout.IntField("Max Stamina", _maxStamina);
            _staminaIntervalSeconds = EditorGUILayout.IntField("Stamina Interval (seconds)", _staminaIntervalSeconds);

            _drawStatus();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Save", GUILayout.Height(30f)))
            {
                _persistSettings(setting, "Save Inventory Settings");
                AssetDatabase.SaveAssets();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void _drawStatus()
        {
            if (string.IsNullOrEmpty(_statusMessage))
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        // ── Load / Persist ──

        void _loadFromAsset()
        {
            _loaded = true;
            _maxStamina = 30;
            _staminaIntervalSeconds = 300;
            _statusMessage = string.Empty;
            _statusType = MessageType.Info;

            var setting = target as InventorySettings;
            if (setting == null)
                return;

            var payload = ((string)setting.SettingsPayload)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
                return;

            // AES 복호화
            string json;
            var (keyBase64, ivBase64) = _loadCryptoKeyIv();
            if (!string.IsNullOrEmpty(keyBase64) && !string.IsNullOrEmpty(ivBase64))
            {
                try
                {
                    json = MobileApplication.DecryptJson(payload, keyBase64, ivBase64);
                }
                catch (Exception ex)
                {
                    _statusMessage = $"AES decrypt failed: {ex.Message}";
                    _statusType = MessageType.Error;
                    return;
                }
            }
            else
            {
                json = payload;
            }

            if (string.IsNullOrWhiteSpace(json))
                return;

            JObject obj;
            try
            {
                obj = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                _statusMessage = $"JSON parse failed: {ex.Message}";
                _statusType = MessageType.Error;
                return;
            }

            _maxStamina = obj.Value<int?>("maxStamina") ?? 30;
            _staminaIntervalSeconds = obj.Value<int?>("staminaIntervalSeconds") ?? 300;
        }

        void _persistSettings(InventorySettings setting, string undoName)
        {
            if (_maxStamina <= 0)
            {
                _statusMessage = "MaxStamina must be greater than 0.";
                _statusType = MessageType.Error;
                return;
            }

            if (_staminaIntervalSeconds <= 0)
            {
                _statusMessage = "StaminaIntervalSeconds must be greater than 0.";
                _statusType = MessageType.Error;
                return;
            }

            var obj = new JObject
            {
                ["maxStamina"] = _maxStamina,
                ["staminaIntervalSeconds"] = _staminaIntervalSeconds,
            };

            var json = obj.ToString(Newtonsoft.Json.Formatting.None);

            // AES 암호화
            string payload;
            var (keyBase64, ivBase64) = _loadCryptoKeyIv();
            if (!string.IsNullOrEmpty(keyBase64) && !string.IsNullOrEmpty(ivBase64))
            {
                try
                {
                    payload = MobileApplication.EncryptJson(json, keyBase64, ivBase64);
                }
                catch (Exception ex)
                {
                    _statusMessage = $"AES encrypt failed: {ex.Message}";
                    _statusType = MessageType.Error;
                    return;
                }
            }
            else
            {
                payload = json;
                _statusMessage = "WARNING: Crypto key/iv not found on MobileApplication. Saved without encryption.";
                _statusType = MessageType.Warning;
            }

            Undo.RecordObject(setting, undoName);
            setting.SettingsPayload = payload;
            EditorUtility.SetDirty(setting);

            if (_statusType != MessageType.Warning)
            {
                _statusMessage = string.Empty;
                _statusType = MessageType.Info;
            }
        }

        static (string keyBase64, string ivBase64) _loadCryptoKeyIv()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationPrefabPath);
            if (prefab == null)
                return (string.Empty, string.Empty);

            var app = prefab.GetComponent<MobileApplication>();
            if (app == null)
                return (string.Empty, string.Empty);

            return (app.CryptoKey ?? string.Empty, app.CryptoIv ?? string.Empty);
        }
    }
}
