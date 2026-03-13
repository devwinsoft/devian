using System;
using System.Collections.Generic;
using Devian.Domain.Game;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(InventorySetting))]
    public sealed class InventorySettingEditor : Editor
    {
        const string ApplicationPrefabPath = "Assets/Resources/Devian/Application.prefab";

        sealed class RewardRow
        {
            public REWARD_TYPE Type;
            public string Id;
            public int Amount;

            public RewardRow Clone()
            {
                return new RewardRow
                {
                    Type = Type,
                    Id = Id,
                    Amount = Amount
                };
            }
        }

        readonly List<RewardRow> _rows = new();
        RewardRow _pendingRow = createDefaultRow();

        string _statusMessage = string.Empty;
        MessageType _statusType = MessageType.Info;
        bool _loaded;

        void OnEnable()
        {
            loadFromAsset();
        }

        public override void OnInspectorGUI()
        {
            if (!_loaded)
                loadFromAsset();

            var setting = target as InventorySetting;
            if (setting == null)
                return;

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
            drawHeader();
            drawAddRow(setting);
            drawStatus();

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Save", GUILayout.Height(30f)))
            {
                persistRows(setting, "Save Initial Inventory");
                AssetDatabase.SaveAssets();
            }
        }

        static RewardRow createDefaultRow()
        {
            return new RewardRow
            {
                Type = REWARD_TYPE.CURRENCY,
                Id = "GOLD",
                Amount = 1000
            };
        }

        void drawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type", EditorStyles.miniBoldLabel, GUILayout.MinWidth(120f), GUILayout.MaxWidth(160f));
            EditorGUILayout.LabelField("Id", EditorStyles.miniBoldLabel, GUILayout.MinWidth(140f));
            EditorGUILayout.LabelField("Amount", EditorStyles.miniBoldLabel, GUILayout.Width(84f));
            EditorGUILayout.LabelField("Action", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            EditorGUILayout.EndHorizontal();
        }

        void drawAddRow(InventorySetting setting)
        {
            EditorGUILayout.BeginHorizontal();
            _pendingRow.Type = (REWARD_TYPE)EditorGUILayout.EnumPopup(_pendingRow.Type, GUILayout.MinWidth(120f), GUILayout.MaxWidth(160f));
            _pendingRow.Id = drawIdField(_pendingRow.Type, _pendingRow.Id, GUILayout.MinWidth(140f));
            _pendingRow.Amount = EditorGUILayout.IntField(_pendingRow.Amount, GUILayout.Width(84f));

            var canAdd = validateRow(_pendingRow, out var addError);
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("Add", GUILayout.Width(56f)))
                {
                    _rows.Add(_pendingRow.Clone());
                    _pendingRow = createDefaultRow();
                    persistRows(setting, "Add Initial Inventory Row");
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!canAdd && !string.IsNullOrEmpty(addError))
            {
                EditorGUILayout.HelpBox(addError, MessageType.Info);
            }
        }

        void drawStatus()
        {
            if (string.IsNullOrEmpty(_statusMessage))
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        void loadFromAsset()
        {
            _loaded = true;
            _rows.Clear();
            _pendingRow = createDefaultRow();
            _statusMessage = string.Empty;
            _statusType = MessageType.Info;

            var setting = target as InventorySetting;
            if (setting == null)
                return;

            var payload = ((string)setting.InitialInventory)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
                return;

            if (!tryGetCryptoConfig(out var key, out var iv, out var cryptoError))
            {
                _statusMessage = cryptoError;
                _statusType = MessageType.Error;
                return;
            }

            var decrypt = InventoryManager.DecryptInitialInventoryJson(payload, key, iv);
            if (decrypt.IsFailure)
            {
                var legacyParse = InventoryManager.ParseInitialInventoryRewardsJson(payload);
                if (legacyParse.IsFailure)
                {
                    _statusMessage = legacyParse.Error!.Message;
                    _statusType = MessageType.Error;
                    return;
                }

                applyRewardsToRows(legacyParse.Value ?? new RewardData[0]);
                _statusMessage = "Legacy plain JSON payload detected. Save once to migrate to encrypted payload.";
                _statusType = MessageType.Warning;
                return;
            }

            var parse = InventoryManager.ParseInitialInventoryRewardsJson(decrypt.Value ?? string.Empty);
            if (parse.IsFailure)
            {
                _statusMessage = parse.Error!.Message;
                _statusType = MessageType.Error;
                return;
            }

            applyRewardsToRows(parse.Value ?? new RewardData[0]);
        }

        void applyRewardsToRows(RewardData[] rewards)
        {
            _rows.Clear();
            if (rewards == null)
                return;

            for (var i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                _rows.Add(new RewardRow
                {
                    Type = reward.Type,
                    Id = reward.Id,
                    Amount = reward.Amount
                });
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

            var json = InventoryManager.SerializeInitialInventoryRewardsJson(rewards);
            if (!tryGetCryptoConfig(out var key, out var iv, out var cryptoError))
            {
                _statusMessage = cryptoError;
                _statusType = MessageType.Error;
                return;
            }

            var encrypt = InventoryManager.EncryptInitialInventoryJson(json, key, iv);
            if (encrypt.IsFailure)
            {
                _statusMessage = encrypt.Error!.Message;
                _statusType = MessageType.Error;
                return;
            }

            Undo.RecordObject(setting, undoName);
            setting.InitialInventory = encrypt.Value ?? string.Empty;
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

        static string drawIdField(REWARD_TYPE type, string currentId, params GUILayoutOption[] options)
        {
            switch (type)
            {
                case REWARD_TYPE.CURRENCY:
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(currentId, out var ct))
                        ct = CURRENCY_TYPE.GOLD;
                    ct = (CURRENCY_TYPE)EditorGUILayout.EnumPopup(ct, options);
                    return ct.ToString();
                }
                case REWARD_TYPE.CARD:
                    ensureTableLoaded(TB_ITEM_CARD.Count, "ITEM_CARD", TB_ITEM_CARD.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_ITEM_CARD.GetAll(), e => e.CardId, options);
                case REWARD_TYPE.EQUIP:
                    ensureTableLoaded(TB_ITEM_EQUIP.Count, "ITEM_EQUIP", TB_ITEM_EQUIP.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_ITEM_EQUIP.GetAll(), e => e.EquipId, options);
                case REWARD_TYPE.HERO:
                    ensureTableLoaded(TB_UNIT_HERO.Count, "UNIT_HERO", TB_UNIT_HERO.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_UNIT_HERO.GetAll(), e => e.UnitId, options);
                case REWARD_TYPE.RENTAL:
                    ensureTableLoaded(TB_ITEM_RENTAL.Count, "ITEM_RENTAL", TB_ITEM_RENTAL.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_ITEM_RENTAL.GetAll(), e => e.RentalId, options);
                case REWARD_TYPE.PASS:
                    ensureTableLoaded(TB_ITEM_PASS.Count, "ITEM_PASS", TB_ITEM_PASS.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_ITEM_PASS.GetAll(), e => e.PassId, options);
                case REWARD_TYPE.TREASURE:
                    ensureTableLoaded(TB_ITEM_TREASURE.Count, "ITEM_TREASURE", TB_ITEM_TREASURE.LoadFromNdjson);
                    return drawTableIdPopup(currentId, TB_ITEM_TREASURE.GetGroupKeys(), e => e, options);
                default:
                    return EditorGUILayout.TextField(currentId ?? string.Empty, options);
            }
        }

        static string drawTableIdPopup<T>(string currentId, IReadOnlyList<T> entries, Func<T, string> keySelector,
            GUILayoutOption[] options)
        {
            if (entries == null || entries.Count == 0)
                return EditorGUILayout.TextField(currentId ?? string.Empty, options);

            var keys = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++)
                keys[i] = keySelector(entries[i]);

            var currentIndex = Array.IndexOf(keys, currentId);
            if (currentIndex < 0)
                currentIndex = 0;

            currentIndex = EditorGUILayout.Popup(currentIndex, keys, options);
            return keys[currentIndex];
        }

        static void ensureTableLoaded(int currentCount, string tableName, Action<string> loadFromNdjson)
        {
            if (currentCount > 0)
                return;

            var textAssets = AssetManager.FindAssets<TextAsset>(tableName);
            foreach (var ta in textAssets)
            {
                var assetPath = AssetDatabase.GetAssetPath(ta);
                if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                loadFromNdjson(ta.text);
                break;
            }
        }

        static bool tryGetCryptoConfig(out string key, out string iv, out string error)
        {
            key = string.Empty;
            iv = string.Empty;

            InventoryManager manager = null;

            var applicationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationPrefabPath);
            if (applicationPrefab != null)
                manager = applicationPrefab.GetComponent<InventoryManager>();

            if (manager == null)
                manager = UnityEngine.Object.FindObjectOfType<InventoryManager>();

            if (manager == null)
            {
                error = $"InventoryManager not found. expected prefab={ApplicationPrefabPath}";
                return false;
            }

            key = (manager.InitialInventoryCryptoKey ?? string.Empty).Trim();
            iv = (manager.InitialInventoryCryptoIv ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(iv))
            {
                error = "InventoryManager crypto key/iv is empty.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
