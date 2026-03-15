using System;
using System.Collections.Generic;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(InventorySetting))]
    public sealed class InventorySettingEditor : Editor
    {
        sealed class RewardRow
        {
            public REWARD_TYPE Type;
            public string Id;
            public int Amount;
        }

        readonly List<RewardRow> _rows = new();
        REWARD_TYPE _addType = REWARD_TYPE.CURRENCY;
        int _addAmount = 1000;

        string _statusMessage = string.Empty;
        MessageType _statusType = MessageType.Info;
        bool _loaded;

        // Cached SerializedProperty for editor-only *_ID fields
        SerializedProperty _propCardId;
        SerializedProperty _propEquipId;
        SerializedProperty _propHeroId;
        SerializedProperty _propRentalId;
        SerializedProperty _propPassId;
        SerializedProperty _propChestId;

        // CURRENCY has no *_ID type, tracked as plain field
        CURRENCY_TYPE _currencyType = CURRENCY_TYPE.GOLD;

        void OnEnable()
        {
            cacheIdProperties();
            loadFromAsset();
        }

        void cacheIdProperties()
        {
            _propCardId   = serializedObject.FindProperty("_editorCardId");
            _propEquipId  = serializedObject.FindProperty("_editorEquipId");
            _propHeroId   = serializedObject.FindProperty("_editorHeroId");
            _propRentalId = serializedObject.FindProperty("_editorRentalId");
            _propPassId   = serializedObject.FindProperty("_editorPassId");
            _propChestId  = serializedObject.FindProperty("_editorChestId");
        }

        public override void OnInspectorGUI()
        {
            if (!_loaded)
                loadFromAsset();

            var setting = target as InventorySetting;
            if (setting == null)
                return;

            serializedObject.Update();

            EditorGUILayout.LabelField("Initial Inventory (RewardData[])", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            var removeIndex = -1;

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                EditorGUILayout.BeginHorizontal();
                var display = $"{row.Type}  |  {row.Id}  |  {row.Amount}";
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(display);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(56f)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                _rows.RemoveAt(removeIndex);
                persistRows(setting, "Delete Initial Inventory Row");
                GUIUtility.ExitGUI();
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Add New Reward", EditorStyles.boldLabel);
            drawAddRow(setting);
            drawStatus();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Save", GUILayout.Height(30f)))
            {
                clearEditorIdFields();
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                persistRows(setting, "Save Initial Inventory");
                AssetDatabase.SaveAssets();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void drawAddRow(InventorySetting setting)
        {
            var prevType = _addType;
            _addType = (REWARD_TYPE)EditorGUILayout.EnumPopup("Type", _addType);

            if (_addType != prevType)
                initEditorIdForType(_addType);

            drawIdPropertyField(_addType);
            _addAmount = EditorGUILayout.IntField("Amount", _addAmount);

            var addId = readIdFromProperty(_addType);
            var tempRow = new RewardRow { Type = _addType, Id = addId, Amount = _addAmount };
            var canAdd = validateRow(tempRow, out var addError);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add", GUILayout.Width(80f), GUILayout.Height(24f)))
                {
                    _rows.Add(tempRow);
                    _addType = REWARD_TYPE.CURRENCY;
                    _currencyType = CURRENCY_TYPE.GOLD;
                    _addAmount = 1000;
                    clearEditorIdFields();
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    GUIUtility.ExitGUI();
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!canAdd && !string.IsNullOrEmpty(addError))
                EditorGUILayout.HelpBox(addError, MessageType.Info);
        }

        /// <summary>
        /// Draw the Id field using generated PropertyDrawer for the given type.
        /// CURRENCY uses EnumPopup directly (no *_ID type exists).
        /// </summary>
        void drawIdPropertyField(REWARD_TYPE type)
        {
            switch (type)
            {
                case REWARD_TYPE.CURRENCY:
                {
                    var newCt = (CURRENCY_TYPE)EditorGUILayout.EnumPopup("Id", _currencyType);
                    if (newCt != _currencyType)
                        _currencyType = newCt;
                    break;
                }
                case REWARD_TYPE.CARD:
                    EditorGUILayout.PropertyField(_propCardId, new GUIContent("Id"));
                    break;
                case REWARD_TYPE.EQUIP:
                    EditorGUILayout.PropertyField(_propEquipId, new GUIContent("Id"));
                    break;
                case REWARD_TYPE.HERO:
                    EditorGUILayout.PropertyField(_propHeroId, new GUIContent("Id"));
                    break;
                case REWARD_TYPE.RENTAL:
                    EditorGUILayout.PropertyField(_propRentalId, new GUIContent("Id"));
                    break;
                case REWARD_TYPE.PASS:
                    EditorGUILayout.PropertyField(_propPassId, new GUIContent("Id"));
                    break;
                case REWARD_TYPE.TREASURE:
                    EditorGUILayout.PropertyField(_propChestId, new GUIContent("Id"));
                    break;
                default:
                    EditorGUILayout.LabelField("Id", "(unsupported type)");
                    break;
            }
        }

        /// <summary>
        /// Read the current Id string from the appropriate *_ID property.
        /// </summary>
        string readIdFromProperty(REWARD_TYPE type)
        {
            switch (type)
            {
                case REWARD_TYPE.CURRENCY:
                    return _currencyType.ToString();
                case REWARD_TYPE.CARD:
                    return _propCardId?.FindPropertyRelative("Value")?.stringValue ?? string.Empty;
                case REWARD_TYPE.EQUIP:
                    return _propEquipId?.FindPropertyRelative("Value")?.stringValue ?? string.Empty;
                case REWARD_TYPE.HERO:
                    return _propHeroId?.FindPropertyRelative("Value")?.stringValue ?? string.Empty;
                case REWARD_TYPE.RENTAL:
                    return _propRentalId?.FindPropertyRelative("Value")?.stringValue ?? string.Empty;
                case REWARD_TYPE.PASS:
                    return _propPassId?.FindPropertyRelative("Value")?.stringValue ?? string.Empty;
                case REWARD_TYPE.TREASURE:
                {
                    var valueProp = _propChestId?.FindPropertyRelative("Value");
                    if (valueProp == null) return TREASURE_GRADE_TYPE.COMMON.ToString();
                    var enumIndex = valueProp.enumValueIndex;
                    return enumIndex >= 0 && enumIndex < valueProp.enumNames.Length
                        ? valueProp.enumNames[enumIndex]
                        : TREASURE_GRADE_TYPE.COMMON.ToString();
                }
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Set the editor *_ID field to a sensible default when the user switches type.
        /// </summary>
        void initEditorIdForType(REWARD_TYPE type)
        {
            switch (type)
            {
                case REWARD_TYPE.CURRENCY:
                    _currencyType = CURRENCY_TYPE.GOLD;
                    break;
                case REWARD_TYPE.TREASURE:
                {
                    var valueProp = _propChestId?.FindPropertyRelative("Value");
                    if (valueProp != null)
                    {
                        var idx = Array.FindIndex(valueProp.enumNames,
                            n => string.Equals(n, TREASURE_GRADE_TYPE.COMMON.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) valueProp.enumValueIndex = idx;
                    }
                    break;
                }
                // For string-based *_ID types, clear so the user can select via Selector
                case REWARD_TYPE.CARD:
                    setStringIdValue(_propCardId, string.Empty);
                    break;
                case REWARD_TYPE.EQUIP:
                    setStringIdValue(_propEquipId, string.Empty);
                    break;
                case REWARD_TYPE.HERO:
                    setStringIdValue(_propHeroId, string.Empty);
                    break;
                case REWARD_TYPE.RENTAL:
                    setStringIdValue(_propRentalId, string.Empty);
                    break;
                case REWARD_TYPE.PASS:
                    setStringIdValue(_propPassId, string.Empty);
                    break;
            }
        }

        void clearEditorIdFields()
        {
            _currencyType = CURRENCY_TYPE.GOLD;
            setStringIdValue(_propCardId, string.Empty);
            setStringIdValue(_propEquipId, string.Empty);
            setStringIdValue(_propHeroId, string.Empty);
            setStringIdValue(_propRentalId, string.Empty);
            setStringIdValue(_propPassId, string.Empty);

            var chestValue = _propChestId?.FindPropertyRelative("Value");
            if (chestValue != null)
                chestValue.enumValueIndex = 0;
        }

        static void setStringIdValue(SerializedProperty idProp, string value)
        {
            var valueProp = idProp?.FindPropertyRelative("Value");
            if (valueProp != null)
                valueProp.stringValue = value;
        }

        void drawStatus()
        {
            if (string.IsNullOrEmpty(_statusMessage))
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        // ── Load / Persist (mirrors InventoryManager.parseInitialInventoryRewards) ──

        void loadFromAsset()
        {
            _loaded = true;
            _rows.Clear();
            _addType = REWARD_TYPE.CURRENCY;
            _currencyType = CURRENCY_TYPE.GOLD;
            _addAmount = 1000;
            _statusMessage = string.Empty;
            _statusType = MessageType.Info;

            var setting = target as InventorySetting;
            if (setting == null)
                return;

            var json = ((string)setting.InitialInventory)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return;

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (Exception ex)
            {
                _statusMessage = $"JSON parse failed: {ex.Message}";
                _statusType = MessageType.Error;
                return;
            }

            JArray rewardsArray = null;
            if (root is JArray rootArray)
                rewardsArray = rootArray;
            else if (root is JObject rootObj && rootObj["rewards"] is JArray nestedArray)
                rewardsArray = nestedArray;

            if (rewardsArray == null)
            {
                _statusMessage = "InitialInventory must be RewardData[] JSON or {\"rewards\": RewardData[]}.";
                _statusType = MessageType.Error;
                return;
            }

            for (var i = 0; i < rewardsArray.Count; i++)
            {
                if (rewardsArray[i] is not JObject rewardObj)
                    continue;

                var typeText = (rewardObj.Value<string>("type") ?? string.Empty).Trim();
                if (string.Equals(typeText, "SEASON_PASS", StringComparison.OrdinalIgnoreCase))
                    typeText = "PASS";

                if (!Enum.TryParse(typeText, true, out REWARD_TYPE rewardType))
                    continue;

                var id = (rewardObj.Value<string>("id") ?? string.Empty).Trim();
                var amount = rewardObj.Value<int?>("amount") ?? 0;

                _rows.Add(new RewardRow { Type = rewardType, Id = id, Amount = amount });
            }
        }

        void persistRows(InventorySetting setting, string undoName)
        {
            var rewards = new RewardData[_rows.Count];
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                row.Id = (row.Id ?? string.Empty).Trim();
                _rows[i] = row;

                if (!validateRow(row, out var rowError))
                {
                    _statusMessage = $"Row {i} invalid: {rowError}";
                    _statusType = MessageType.Error;
                    return;
                }

                rewards[i] = new RewardData(row.Type, row.Id, row.Amount);
            }

            var array = new JArray();
            for (var i = 0; i < rewards.Length; i++)
            {
                array.Add(new JObject
                {
                    ["type"] = rewards[i].Type.ToString(),
                    ["id"] = rewards[i].Id,
                    ["amount"] = rewards[i].Amount,
                });
            }

            Undo.RecordObject(setting, undoName);
            setting.InitialInventory = array.ToString(Newtonsoft.Json.Formatting.None);
            EditorUtility.SetDirty(setting);

            _statusMessage = string.Empty;
            _statusType = MessageType.Info;
        }

        static bool validateRow(RewardRow row, out string error)
        {
            if (string.IsNullOrWhiteSpace(row.Id))
            {
                error = "id is required.";
                return false;
            }

            if (row.Amount <= 0)
            {
                error = "amount must be greater than 0.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
