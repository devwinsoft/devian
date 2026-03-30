using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [CustomEditor(typeof(UIPopupPanel))]
    public sealed class UIPopupPanelEditor : UnityEditor.Editor
    {
        private SerializedProperty _dimProperty;
        private SerializedProperty _popupRootProperty;

        private void OnEnable()
        {
            _dimProperty = serializedObject.FindProperty("_dim");
            _popupRootProperty = serializedObject.FindProperty("_popupRoot");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            var panel = (UIPopupPanel)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Popup Install", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Install buttons are only available in Edit Mode.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var hasDim = _dimProperty.objectReferenceValue != null;
            var hasPopupRoot = _popupRootProperty.objectReferenceValue != null;

            if (hasDim && hasPopupRoot)
            {
                EditorGUILayout.HelpBox("Dim and PopupRoot are installed.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Install Dim and PopupRoot children for popup modal layout.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Install Missing", GUILayout.Height(28)))
                {
                    Install(panel, installDim: true, installPopupRoot: true);
                }

                if (GUILayout.Button("Normalize Layout", GUILayout.Height(28)))
                {
                    Install(panel, installDim: false, installPopupRoot: false);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void Install(UIPopupPanel panel, bool installDim, bool installPopupRoot)
        {
            if (panel == null)
            {
                return;
            }

            serializedObject.Update();
            Undo.RecordObject(panel, "Install Popup Panel Layout");

            var dim = EnsureDim(panel, installDim);
            var popupRoot = EnsurePopupRoot(panel, installPopupRoot);

            if (dim != null)
            {
                _dimProperty.objectReferenceValue = dim;
                dim.transform.SetAsFirstSibling();
                EditorUtility.SetDirty(dim.gameObject);
            }

            if (popupRoot != null)
            {
                _popupRootProperty.objectReferenceValue = popupRoot;
                popupRoot.SetAsLastSibling();
                EditorUtility.SetDirty(popupRoot.gameObject);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
        }

        private static UIPopupDim EnsureDim(UIPopupPanel panel, bool createIfMissing)
        {
            var dimTransform = panel.transform.Find("Dim");
            RectTransform dimRect;

            if (dimTransform == null)
            {
                if (!createIfMissing)
                {
                    return null;
                }

                var dimObject = new GameObject("Dim", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(dimObject, "Create Popup Dim");
                Undo.SetTransformParent(dimObject.transform, panel.transform, "Parent Popup Dim");
                dimRect = dimObject.GetComponent<RectTransform>();
            }
            else
            {
                dimRect = dimTransform as RectTransform;
            }

            NormalizeStretchRect(dimRect);

            var image = dimRect.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(dimRect.gameObject);
            }

            var canvasGroup = dimRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = Undo.AddComponent<CanvasGroup>(dimRect.gameObject);
            }

            var dim = dimRect.GetComponent<UIPopupDim>();
            if (dim == null)
            {
                dim = Undo.AddComponent<UIPopupDim>(dimRect.gameObject);
            }

            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            dimRect.gameObject.SetActive(false);

            return dim;
        }

        private static RectTransform EnsurePopupRoot(UIPopupPanel panel, bool createIfMissing)
        {
            var popupRootTransform = panel.transform.Find("PopupRoot");
            RectTransform popupRoot;

            if (popupRootTransform == null)
            {
                if (!createIfMissing)
                {
                    return null;
                }

                var popupRootObject = new GameObject("PopupRoot", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(popupRootObject, "Create PopupRoot");
                Undo.SetTransformParent(popupRootObject.transform, panel.transform, "Parent PopupRoot");
                popupRoot = popupRootObject.GetComponent<RectTransform>();
            }
            else
            {
                popupRoot = popupRootTransform as RectTransform;
            }

            NormalizeStretchRect(popupRoot);
            return popupRoot;
        }

        private static void NormalizeStretchRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Undo.RecordObject(rect, "Normalize Popup Rect");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
