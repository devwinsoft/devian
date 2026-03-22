#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    public sealed class UIPopupFrameTypeSelector : EditorWindow
    {
        private const int ColumnCount = 2;
        private const string NoneOption = "(none)";

        private SerializedProperty _targetProperty;
        private readonly List<Type> _types = new List<Type>();
        private readonly List<Type> _filteredTypes = new List<Type>();
        private string _searchText = string.Empty;
        private Vector2 _scrollPosition;
        private int _selectedIndex;

        public void Bind(SerializedProperty property)
        {
            _targetProperty = property;
        }

        public void Reload()
        {
            _types.Clear();

            var popupTypes = TypeCache.GetTypesDerivedFrom<UIPopupFrameBase>();
            foreach (var type in popupTypes)
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                _types.Add(type);
            }

            _types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            RefreshFilteredList();
        }

        public void Init(string currentValue)
        {
            _searchText = string.Empty;
            RefreshFilteredList();
            _selectedIndex = 0;

            if (string.IsNullOrWhiteSpace(currentValue))
            {
                return;
            }

            for (var i = 0; i < _filteredTypes.Count; i++)
            {
                var type = _filteredTypes[i];
                if (string.Equals(type.AssemblyQualifiedName, currentValue, StringComparison.Ordinal)
                    || string.Equals(type.FullName, currentValue, StringComparison.Ordinal))
                {
                    _selectedIndex = i + 1;
                    return;
                }
            }
        }

        private void RefreshFilteredList()
        {
            _filteredTypes.Clear();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                _filteredTypes.AddRange(_types);
                return;
            }

            var search = _searchText.Trim().ToLowerInvariant();
            foreach (var type in _types)
            {
                var fullName = type.FullName ?? type.Name;
                if (fullName.ToLowerInvariant().Contains(search) || type.Name.ToLowerInvariant().Contains(search))
                {
                    _filteredTypes.Add(type);
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50f));
            var newSearch = EditorGUILayout.TextField(_searchText);
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                RefreshFilteredList();
                _selectedIndex = 0;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            var options = new string[_filteredTypes.Count + 1];
            options[0] = NoneOption;
            for (var i = 0; i < _filteredTypes.Count; i++)
            {
                var type = _filteredTypes[i];
                options[i + 1] = type.FullName ?? type.Name;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            var newSelection = GUILayout.SelectionGrid(_selectedIndex, options, ColumnCount);
            EditorGUILayout.EndScrollView();

            if (newSelection != _selectedIndex)
            {
                _selectedIndex = newSelection;
                ApplySelection();
                Close();
                GUIUtility.ExitGUI();
            }
        }

        private void ApplySelection()
        {
            if (_targetProperty == null)
            {
                return;
            }

            _targetProperty.serializedObject.Update();

            if (_selectedIndex == 0)
            {
                _targetProperty.stringValue = string.Empty;
            }
            else
            {
                var selectedType = _filteredTypes[_selectedIndex - 1];
                _targetProperty.stringValue = selectedType.AssemblyQualifiedName ?? selectedType.FullName ?? string.Empty;
            }

            _targetProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
