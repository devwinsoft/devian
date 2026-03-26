using System;
using UnityEngine;

namespace Devian
{
    public enum LegalDocumentType
    {
        None = 0,
        TermsOfService = 1,
        PrivacyPolicy = 2,
    }

    [Serializable]
    public sealed class LegalDocumentConfig
    {
        [SerializeField] LegalDocumentType _documentType = LegalDocumentType.None;
        [SerializeField] string _filename = string.Empty;

        public LegalDocumentType DocumentType => _documentType;
        public string Filename => _filename;
        public bool IsConfigured => _documentType != LegalDocumentType.None && !string.IsNullOrWhiteSpace(_filename);
    }

    [CreateAssetMenu(fileName = "LegalConsentSettings", menuName = "Devian/MobilePackage/Legal Consent Settings")]
    public sealed class LegalConsentSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/LegalConsentSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/LegalConsentSettings.asset";

        [SerializeField] VersionNumber _version = new(1, 0, 0);
        [SerializeField] string _cdnBaseUrl = string.Empty;
        [SerializeField] LegalDocumentConfig[] _documents = Array.Empty<LegalDocumentConfig>();

        public VersionNumber Version => _version;
        public string CdnBaseUrl => _cdnBaseUrl;
        public LegalDocumentConfig[] Documents => _documents ?? Array.Empty<LegalDocumentConfig>();

        public bool TryGetDocument(LegalDocumentType documentType, out LegalDocumentConfig document)
        {
            var documents = Documents;
            for (var i = 0; i < documents.Length; i++)
            {
                var current = documents[i];
                if (current != null && current.DocumentType == documentType)
                {
                    document = current;
                    return true;
                }
            }

            document = null;
            return false;
        }
    }
}
