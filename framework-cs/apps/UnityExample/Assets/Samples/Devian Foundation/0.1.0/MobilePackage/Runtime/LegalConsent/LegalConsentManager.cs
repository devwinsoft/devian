using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;
using UnityEngine.Networking;

namespace Devian
{
    public sealed class LegalConsentManager : CompoSingleton<LegalConsentManager>
    {
        const string StoragePrefsKey = "Devian.LegalConsent.Storage";

        readonly JsonPrefs<LegalConsentStorage> _prefs = new(StoragePrefsKey, new LegalConsentStorage());

        LegalConsentSettings _settings;
        bool _settingsLoaded;

        public LegalConsentStorage Storage => _prefs.Value;
        public LegalConsentSettings Settings => ensureSettings();

        protected override void onInitAwake()
        {
            ensureSettings();
            Storage.EnsureInitialized();
            _prefs.Save();
        }

        public LegalDocumentConfig[] GetDocuments()
        {
            var settings = ensureSettings();
            return settings != null ? settings.Documents : Array.Empty<LegalDocumentConfig>();
        }

        public bool TryGetDocument(LegalDocumentType documentType, out LegalDocumentConfig document)
        {
            var settings = ensureSettings();
            if (settings == null)
            {
                document = null;
                return false;
            }

            return settings.TryGetDocument(documentType, out document);
        }

        public bool NeedsConsent()
        {
            var settings = ensureSettings();
            if (settings == null)
                return false;

            var documents = settings.Documents;
            if (documents == null || documents.Length <= 0)
                return false;

            Storage.EnsureInitialized();
            return !Storage.isAccepted || Storage.acceptedVersion != settings.Version;
        }

        public string GetDocumentUrl(LegalDocumentType documentType)
        {
            if (!TryGetDocument(documentType, out var document))
                return string.Empty;

            return buildDocumentUrl(document);
        }

        public async Task<CommonResult<string>> DownloadDocumentAsync(
            LegalDocumentType documentType,
            CancellationToken ct = default)
        {
            if (!TryGetDocument(documentType, out var document) || document == null || !document.IsConfigured)
            {
                return CommonResult<string>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Legal document is not configured: {documentType}");
            }

            var url = buildDocumentUrl(document);
            if (string.IsNullOrWhiteSpace(url))
            {
                return CommonResult<string>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Legal document URL is not configured: {documentType}");
            }

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 10;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                return CommonResult<string>.Failure(
                    COMMON_ERROR_TYPE.COMMON_NETWORK,
                    $"Legal document request failed: {request.error}");
            }

            var content = request.downloadHandler?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                return CommonResult<string>.Failure(
                    COMMON_ERROR_TYPE.COMMON_UNKNOWN,
                    $"Legal document response is empty: {documentType}");
            }

            return CommonResult<string>.Success(content);
        }

        public VersionNumber GetAcceptedVersion()
        {
            Storage.EnsureInitialized();
            return Storage.acceptedVersion;
        }

        public long GetAcceptedAtUtcMs()
        {
            Storage.EnsureInitialized();
            return Storage.acceptedAtUtcMs;
        }

        public bool AcceptCurrentVersion(long acceptedAtUtcMs = 0L)
        {
            var settings = ensureSettings();
            if (settings == null)
                return false;

            _prefs.Edit(storage =>
            {
                storage.EnsureInitialized();
                storage.SetAccepted(settings.Version, resolveAcceptedAtUtcMs(acceptedAtUtcMs));
            }, true);

            return true;
        }

        public void ClearStorage()
        {
            _prefs.Edit(storage => storage.Clear(), true);
        }

        LegalConsentSettings ensureSettings()
        {
            if (!_settingsLoaded)
            {
                _settings = Resources.Load<LegalConsentSettings>(LegalConsentSettings.ResourcesPath);
                _settingsLoaded = true;
            }

            return _settings;
        }

        string buildDocumentUrl(LegalDocumentConfig document)
        {
            var settings = ensureSettings();
            if (settings == null || document == null || !document.IsConfigured)
                return string.Empty;

            var cdnBaseUrl = (settings.CdnBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var version = settings.Version.ToString();
            var language = getCurrentLanguageCode();
            var filename = (document.Filename ?? string.Empty).Trim().TrimStart('/');

            if (string.IsNullOrWhiteSpace(cdnBaseUrl)
                || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(language)
                || string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }

            return $"{cdnBaseUrl}/{version}/{language}/{filename}";
        }

        string getCurrentLanguageCode()
        {
            var languageCode = CommonUtil.GetLanguageCodeOrEmpty(UnityEngine.Application.systemLanguage.ToString());
            if (!string.IsNullOrWhiteSpace(languageCode))
                return languageCode;

            var fallbackLanguage = MobileApplication.Instance != null
                ? MobileApplication.Instance.DefaultLanguage
                : SystemLanguage.English;

            languageCode = CommonUtil.GetLanguageCodeOrEmpty(fallbackLanguage.ToString());
            return !string.IsNullOrWhiteSpace(languageCode) ? languageCode : "en";
        }

        static long resolveAcceptedAtUtcMs(long acceptedAtUtcMs)
        {
            return acceptedAtUtcMs > 0L
                ? acceptedAtUtcMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
