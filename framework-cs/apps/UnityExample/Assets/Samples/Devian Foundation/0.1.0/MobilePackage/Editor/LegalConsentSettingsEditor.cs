#if UNITY_EDITOR

using Devian.Domain.Common;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(LegalConsentSettings))]
    public sealed class LegalConsentSettingsEditor : Editor
    {
        const string KoreanLanguageCode = "ko";

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            drawKoreanUrlExamples((LegalConsentSettings)target);
        }

        static void drawKoreanUrlExamples(LegalConsentSettings settings)
        {
            if (settings == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Korean URL Examples", EditorStyles.boldLabel);

            var documents = settings.Documents;
            if (documents == null || documents.Length <= 0)
            {
                EditorGUILayout.HelpBox("No legal documents are configured.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var languageCode = CommonUtils.GetLanguageCodeOrEmpty(nameof(SystemLanguage.Korean));
            if (string.IsNullOrWhiteSpace(languageCode))
                languageCode = KoreanLanguageCode;

            var hasExample = false;
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null || !document.IsConfigured)
                    continue;

                var url = buildDocumentUrl(settings, document, languageCode);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                hasExample = true;
                EditorGUILayout.LabelField(document.DocumentType.ToString(), EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(
                    url,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Open", GUILayout.Width(52f)))
                {
                    UnityEngine.Application.OpenURL(url);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!hasExample)
            {
                EditorGUILayout.HelpBox("Set CDN base URL and configured filenames to build document URLs.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        static string buildDocumentUrl(LegalConsentSettings settings, LegalDocumentConfig document, string languageCode)
        {
            if (settings == null || document == null || !document.IsConfigured)
                return string.Empty;

            var cdnBaseUrl = (settings.CdnBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var version = settings.Version.ToString();
            var filename = (document.Filename ?? string.Empty).Trim().TrimStart('/');

            if (string.IsNullOrWhiteSpace(cdnBaseUrl)
                || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(languageCode)
                || string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }

            return $"{cdnBaseUrl}/{version}/{languageCode}/{filename}";
        }
    }
}

#endif
