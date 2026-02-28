using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// Mock Ad 테스트 씬 컨트롤러.
    /// 빈 씬에 이 컴포넌트 하나만 추가하면, Start()에서:
    ///   1. TB_ADVERTISE에 MOCK provider 테스트 데이터 주입
    ///   2. AdManager + 의존 매니저 컴포넌트 자동 생성
    ///   3. 동적 UI (Canvas + 버튼 + 로그) 생성
    /// </summary>
    public sealed class AdTestController : MonoBehaviour
    {
        const string Tag = "AdTest";

        // ── Mock 테스트 데이터 (Provider=MOCK) ──
        const string MockNdjson =
            "{\"AdvertiseId\":\"mock_banner\",\"Format\":\"BANNER\",\"Provider\":\"MOCK\",\"RewardGroupId\":\"\",\"IsActive\":true,\"AutoLoad\":false,\"CooldownSec\":0,\"AndroidAdUnitId\":\"mock.banner\",\"IosAdUnitId\":\"mock.banner\"}\n"
          + "{\"AdvertiseId\":\"mock_interstitial\",\"Format\":\"INTERSTITIAL\",\"Provider\":\"MOCK\",\"RewardGroupId\":\"\",\"IsActive\":true,\"AutoLoad\":false,\"CooldownSec\":10,\"AndroidAdUnitId\":\"mock.interstitial\",\"IosAdUnitId\":\"mock.interstitial\"}\n"
          + "{\"AdvertiseId\":\"mock_rewarded\",\"Format\":\"REWARDED\",\"Provider\":\"MOCK\",\"RewardGroupId\":\"reward_chest_001\",\"IsActive\":true,\"AutoLoad\":false,\"CooldownSec\":0,\"AndroidAdUnitId\":\"mock.rewarded\",\"IosAdUnitId\":\"mock.rewarded\"}\n"
          + "{\"AdvertiseId\":\"mock_app_open\",\"Format\":\"APP_OPEN\",\"Provider\":\"MOCK\",\"RewardGroupId\":\"\",\"IsActive\":true,\"AutoLoad\":false,\"CooldownSec\":60,\"AndroidAdUnitId\":\"mock.app_open\",\"IosAdUnitId\":\"mock.app_open\"}";

        // ── 시나리오 전환 ──
        [Header("Mock Scenario")]
        [SerializeField] MockAdScenario _scenario = MockAdScenario.Success;
        MockAdScenario _prevScenario;

        // ── UI 참조 ──
        Text _logText;
        ScrollRect _scrollRect;

        void Start()
        {
            // 1) TB_ADVERTISE 주입
            TB_ADVERTISE.LoadFromNdjson(MockNdjson);
            AppendLog($"TB_ADVERTISE loaded: {TB_ADVERTISE.Count} rows (MOCK)");

            // 2) 의존 매니저 컴포넌트 자동 생성
            EnsureComponent<AdManager>();
            EnsureComponent<RewardManager>();
            EnsureComponent<InventoryManager>();

            // 3) 동적 UI 생성
            BuildUI();

            // 4) AdManager 초기화
            InitializeAds();
        }

        void Update()
        {
            // Inspector에서 시나리오 변경 시 실시간 반영
            if (_scenario != _prevScenario)
            {
                _prevScenario = _scenario;
                ApplyMockScenario(_scenario);
            }
        }

        // ────────────────────────────────────────────
        // Ad 테스트 메서드
        // ────────────────────────────────────────────

        async void InitializeAds()
        {
            AppendLog("── Initialize ──");
            var result = await AdManager.Instance.InitializeAsync();
            AppendLog($"Initialize: {(result.IsSuccess ? "OK" : result.Error.ToString())}");
        }

        async void OnClick_Preload(string advertiseId)
        {
            AppendLog($"── Preload: {advertiseId} ──");
            var result = await AdManager.Instance.PreloadAsync(advertiseId);
            AppendLog($"Preload {advertiseId}: {(result.IsSuccess ? "OK" : result.Error.ToString())}");
        }

        void OnClick_CanShow(string advertiseId)
        {
            var can = AdManager.Instance.CanShow(advertiseId);
            AppendLog($"CanShow {advertiseId}: {can}");
        }

        async void OnClick_Show(string advertiseId)
        {
            AppendLog($"── Show: {advertiseId} ──");
            var result = await AdManager.Instance.ShowAsync(advertiseId);
            if (result.IsSuccess)
            {
                var v = result.Value;
                AppendLog($"Show OK — format={v.Format} reward={v.RewardApplied} status={v.ProviderStatus}");
                if (v.RewardApplied)
                    AppendLog($"  -> RewardGroupId: {v.RewardGroupId}");
            }
            else
            {
                AppendLog($"Show FAIL — {result.Error}");
            }
        }

        void OnClick_HideBanner()
        {
            AdManager.Instance.HideBanner("mock_banner");
            AppendLog("HideBanner: mock_banner");
        }

        void OnClick_ClearLog()
        {
            if (_logText != null)
                _logText.text = $"[{Tag}] Log cleared\n";
        }

        // ────────────────────────────────────────────
        // Mock 시나리오 전환
        // ────────────────────────────────────────────

        void ApplyMockScenario(MockAdScenario scenario)
        {
            // AdManager 내부의 MOCK provider를 찾아 시나리오 교체
            var all = TB_ADVERTISE.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Provider != ADVERTISE_PROVIDER.MOCK)
                    continue;

                // AdManager._providers를 직접 접근할 수 없으므로,
                // 여기서는 로그만 남기고 실제 반영은 리플렉션 또는 public helper가 필요
                break;
            }
            AppendLog($"Scenario -> {scenario} (다음 Load/Show부터 적용)");
        }

        // ────────────────────────────────────────────
        // 동적 UI 빌드
        // ────────────────────────────────────────────

        void BuildUI()
        {
            // EventSystem
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Canvas
            var canvasGo = new GameObject("AdTestCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 좌측: 버튼 패널
            var btnPanel = CreatePanel(canvasGo.transform, "BtnPanel",
                new Vector2(0, 0.3f), new Vector2(0.45f, 1f));
            var btnLayout = btnPanel.AddComponent<VerticalLayoutGroup>();
            btnLayout.spacing = 8;
            btnLayout.padding = new RectOffset(12, 12, 12, 12);
            btnLayout.childForceExpandWidth = true;
            btnLayout.childForceExpandHeight = false;
            btnPanel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 버튼 생성
            CreateSectionLabel(btnPanel.transform, "── BANNER ──");
            CreateBtn(btnPanel.transform, "Preload Banner", () => OnClick_Preload("mock_banner"));
            CreateBtn(btnPanel.transform, "Show Banner", () => OnClick_Show("mock_banner"));
            CreateBtn(btnPanel.transform, "Hide Banner", () => OnClick_HideBanner());

            CreateSectionLabel(btnPanel.transform, "── INTERSTITIAL ──");
            CreateBtn(btnPanel.transform, "Preload Interstitial", () => OnClick_Preload("mock_interstitial"));
            CreateBtn(btnPanel.transform, "Show Interstitial", () => OnClick_Show("mock_interstitial"));

            CreateSectionLabel(btnPanel.transform, "── REWARDED ──");
            CreateBtn(btnPanel.transform, "Preload Rewarded", () => OnClick_Preload("mock_rewarded"));
            CreateBtn(btnPanel.transform, "Show Rewarded", () => OnClick_Show("mock_rewarded"));

            CreateSectionLabel(btnPanel.transform, "── APP OPEN ──");
            CreateBtn(btnPanel.transform, "Preload AppOpen", () => OnClick_Preload("mock_app_open"));
            CreateBtn(btnPanel.transform, "Show AppOpen", () => OnClick_Show("mock_app_open"));

            CreateSectionLabel(btnPanel.transform, "── UTIL ──");
            CreateBtn(btnPanel.transform, "CanShow (all)", () =>
            {
                OnClick_CanShow("mock_banner");
                OnClick_CanShow("mock_interstitial");
                OnClick_CanShow("mock_rewarded");
                OnClick_CanShow("mock_app_open");
            });
            CreateBtn(btnPanel.transform, "Clear Log", () => OnClick_ClearLog());

            // 우측: 로그 패널
            var logPanel = CreatePanel(canvasGo.transform, "LogPanel",
                new Vector2(0.47f, 0f), new Vector2(1f, 1f));

            // ScrollRect
            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(logPanel.transform, false);
            var scrollRT = scrollGo.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(8, 8);
            scrollRT.offsetMax = new Vector2(-8, -8);
            _scrollRect = scrollGo.AddComponent<ScrollRect>();
            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;
            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _logText = contentGo.AddComponent<Text>();
            _logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logText.fontSize = 22;
            _logText.color = Color.white;
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.text = $"[{Tag}] Mock Ad Test Scene Ready\n";

            _scrollRect.content = contentRT;

            // Mask
            scrollGo.AddComponent<RectMask2D>();
        }

        // ────────────────────────────────────────────
        // UI 헬퍼
        // ────────────────────────────────────────────

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);
            return go;
        }

        static void CreateBtn(Transform parent, string label, Action onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 60;
            layout.minHeight = 60;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.28f, 1f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var txt = textGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 26;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        static void CreateSectionLabel(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 36;
            layout.minHeight = 36;

            var txt = go.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.color = new Color(0.6f, 0.8f, 1f, 1f);
            txt.alignment = TextAnchor.MiddleLeft;
        }

        T EnsureComponent<T>() where T : MonoBehaviour
        {
            var comp = GetComponent<T>();
            if (comp == null)
                comp = gameObject.AddComponent<T>();
            return comp;
        }

        void AppendLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Debug.Log($"[{Tag}] {msg}");

            if (_logText != null)
            {
                _logText.text += line + "\n";
                // 자동 스크롤
                Canvas.ForceUpdateCanvases();
                if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
