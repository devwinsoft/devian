using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public abstract class UIBasePageCanvas : UIBaseCanvas
    {
        [SerializeField] private int _initialPageIndex = -1;

        private readonly List<UIBasePageMain> _mainPages = new List<UIBasePageMain>();
        private readonly List<UIBasePageSub> _subPages = new List<UIBasePageSub>();

        private UIBasePageMain _currentMain;
        private UIBasePageSub _currentSub;
        private int _navigationVersion;
        private bool _pagesInitialized;

        public int initialPageIndex => _initialPageIndex;

        public UIBasePageMain currentMain
        {
            get
            {
                EnsurePagesInitialized();
                return _currentMain;
            }
        }

        public UIBasePageSub currentSub
        {
            get
            {
                EnsurePagesInitialized();
                return _currentSub;
            }
        }

        public bool isTransitioningMain { get; private set; }

        protected override void onInitComplete()
        {
            MarkPagesDirty();
            EnsurePagesInitialized();
            onPageCanvasInitComplete();
        }

        protected sealed override void onPoolSpawned()
        {
            _navigationVersion++;
            isTransitioningMain = false;

            MarkPagesDirty();
            EnsurePagesInitialized();
            onPageCanvasPoolSpawned();
        }

        protected sealed override void onPoolDespawned()
        {
            CancelAllTransitions();
            _navigationVersion++;
            _currentMain = null;
            _currentSub = null;
            isTransitioningMain = false;

            MarkPagesDirty();
            onPageCanvasPoolDespawned();
        }

        protected virtual void onPageCanvasInitComplete() { }
        protected virtual void onPageCanvasPoolSpawned() { }
        protected virtual void onPageCanvasPoolDespawned() { }

        public void ShowPage(int pageIndex, bool animated = true)
        {
            if (!TryGetMainPage(pageIndex, out var page))
            {
                Debug.LogWarning($"[{GetType().Name}] Main page with index '{pageIndex}' was not found.", this);
                return;
            }

            ShowPage(page, animated);
        }

        public void ShowPage(UIBasePageMain page, bool animated = true)
        {
            if (page == null)
            {
                Debug.LogWarning($"[{GetType().Name}] ShowPage: target main page is null.", this);
                return;
            }

            EnsurePagesInitialized();

            if (!IsOwnedMain(page))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] ShowPage: main page '{page.name}' does not belong to this canvas.",
                    this);
                return;
            }

            if (isTransitioningMain)
            {
                if (_currentMain == page && _currentSub == null)
                    return;

                NormalizeToCurrentMain();
            }

            if (_currentMain == page)
            {
                CloseCurrentSubImmediate(restoreMain: true);
                NormalizeToMain(page);
                return;
            }

            CloseCurrentSubImmediate(restoreMain: true);

            if (_currentMain == null || !animated)
            {
                NormalizeToMain(page);
                return;
            }

            StartTransition(page, animated);
        }

        public bool TryGetMainPage(int pageIndex, out UIBasePageMain page)
        {
            EnsurePagesInitialized();
            return TryGetCollectedMainPage(pageIndex, out page);
        }

        public override bool Validate(out string reason)
        {
            if (!base.Validate(out reason))
                return false;

            CollectMainPages(_mainPages);
            CollectSubPages(_subPages);

            var mainPageIndexes = new HashSet<int>();
            for (var i = 0; i < _mainPages.Count; i++)
            {
                var page = _mainPages[i];
                if (!mainPageIndexes.Add(page.pageIndex))
                {
                    reason = $"Duplicate main pageIndex detected: {page.pageIndex}";
                    return false;
                }

                if (!page._ValidateTransitionSetup(out reason))
                    return false;
            }

            for (var i = 0; i < _subPages.Count; i++)
            {
                var sub = _subPages[i];
                if (!mainPageIndexes.Contains(sub.pageIndex))
                {
                    reason = $"Sub page '{sub.name}' references missing main pageIndex '{sub.pageIndex}'.";
                    return false;
                }

                if (!sub._ValidateTransitionSetup(out reason))
                    return false;
            }

            if (_initialPageIndex >= 0 && !mainPageIndexes.Contains(_initialPageIndex))
            {
                reason = $"Initial main pageIndex '{_initialPageIndex}' was not found.";
                return false;
            }

            reason = null;
            return true;
        }

        internal bool TryShowSub(UIBasePageSub sub, bool animated = true)
        {
            if (sub == null)
            {
                Debug.LogWarning($"[{GetType().Name}] TryShowSub: target sub page is null.", this);
                return false;
            }

            EnsurePagesInitialized();

            if (!IsOwnedSub(sub))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] TryShowSub: sub page '{sub.name}' does not belong to this canvas.",
                    this);
                return false;
            }

            if (isTransitioningMain)
                NormalizeToCurrentMain();

            if (_currentMain == null)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] TryShowSub: current main page is null.",
                    this);
                return false;
            }

            if (_currentMain.pageIndex != sub.pageIndex)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] TryShowSub: sub '{sub.name}' pageIndex '{sub.pageIndex}' " +
                    $"does not match current main pageIndex '{_currentMain.pageIndex}'.",
                    this);
                return false;
            }

            if (_currentSub != null && _currentSub != sub)
                CloseCurrentSubImmediate(restoreMain: false);

            _currentMain._SetShownImmediate(false);
            _currentSub = sub;
            sub._ShowInternal(animated, null);
            return true;
        }

        internal bool TryHideSub(
            UIBasePageSub sub,
            UIPageSubCloseReason reason,
            bool animated = true)
        {
            if (sub == null)
            {
                Debug.LogWarning($"[{GetType().Name}] TryHideSub: target sub page is null.", this);
                return false;
            }

            EnsurePagesInitialized();

            if (!IsOwnedSub(sub))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] TryHideSub: sub page '{sub.name}' does not belong to this canvas.",
                    this);
                return false;
            }

            var shouldRestoreMain = reason == UIPageSubCloseReason.Normal;
            if (_currentSub != sub)
            {
                sub._HideInternal(animated: false, null);
                return false;
            }

            sub._HideInternal(animated, () =>
            {
                if (_currentSub == sub)
                    _currentSub = null;

                if (shouldRestoreMain
                    && _currentMain != null
                    && _currentMain.pageIndex == sub.pageIndex)
                {
                    _currentMain._SetShownImmediate(true);
                }
            });

            return true;
        }

        private void StartTransition(UIBasePageMain targetPage, bool animated)
        {
            var fromPage = _currentMain;
            var exitDirection = fromPage.pageIndex < targetPage.pageIndex
                ? UIPageTransitionDirection.Left
                : UIPageTransitionDirection.Right;
            var enterDirection = exitDirection == UIPageTransitionDirection.Left
                ? UIPageTransitionDirection.Right
                : UIPageTransitionDirection.Left;

            _navigationVersion++;
            var version = _navigationVersion;

            isTransitioningMain = true;
            _currentMain = targetPage;
            _currentSub = null;

            for (var i = 0; i < _mainPages.Count; i++)
            {
                var mainPage = _mainPages[i];
                if (mainPage == fromPage || mainPage == targetPage)
                    continue;

                mainPage._NormalizeHidden();
            }

            for (var i = 0; i < _subPages.Count; i++)
                _subPages[i]._NormalizeHidden();

            var pendingCompletions = 2;

            void CompleteStep()
            {
                if (version != _navigationVersion)
                    return;

                pendingCompletions--;
                if (pendingCompletions > 0)
                    return;

                isTransitioningMain = false;
                NormalizeToMain(targetPage);
            }

            targetPage._Enter(enterDirection, animated, fromPage, CompleteStep);
            fromPage._Exit(exitDirection, animated, targetPage, CompleteStep);
        }

        private void EnsurePagesInitialized()
        {
            if (!isInitialized || _pagesInitialized)
                return;

            _pagesInitialized = true;
            CollectMainPages(_mainPages);
            CollectSubPages(_subPages);
            NormalizeToMain(ResolveInitialMainPage());
        }

        private void NormalizeToCurrentMain()
        {
            if (_currentMain != null && !IsOwnedMain(_currentMain))
                _currentMain = null;

            if (_currentSub != null && !IsOwnedSub(_currentSub))
                _currentSub = null;

            NormalizeToMain(_currentMain);
        }

        private void NormalizeToMain(UIBasePageMain targetMain)
        {
            CancelAllTransitions();

            for (var i = 0; i < _mainPages.Count; i++)
            {
                var mainPage = _mainPages[i];
                if (mainPage == targetMain)
                {
                    mainPage._NormalizeVisible(makeCurrent: true);
                    continue;
                }

                mainPage._NormalizeHidden();
            }

            for (var i = 0; i < _subPages.Count; i++)
                _subPages[i]._NormalizeHidden();

            _currentMain = targetMain;
            _currentSub = null;
            isTransitioningMain = false;
        }

        private void CloseCurrentSubImmediate(bool restoreMain)
        {
            if (_currentSub == null)
                return;

            var sub = _currentSub;
            _currentSub = null;
            sub._HideInternal(animated: false, null);

            if (restoreMain
                && _currentMain != null
                && _currentMain.pageIndex == sub.pageIndex)
            {
                _currentMain._SetShownImmediate(true);
            }
        }

        private void CancelAllTransitions()
        {
            for (var i = 0; i < _mainPages.Count; i++)
                _mainPages[i]._CancelPageTransition();

            for (var i = 0; i < _subPages.Count; i++)
                _subPages[i]._CancelTransition();
        }

        private void CollectMainPages(List<UIBasePageMain> pages)
        {
            pages.Clear();

            var candidates = GetComponentsInChildren<UIBasePageMain>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null)
                    continue;

                var nearestCanvas = candidate.GetComponentInParent<UIBasePageCanvas>(true);
                if (nearestCanvas != this)
                    continue;

                pages.Add(candidate);
            }

            pages.Sort(ComparePagePanels);
        }

        private void CollectSubPages(List<UIBasePageSub> pages)
        {
            pages.Clear();

            var candidates = GetComponentsInChildren<UIBasePageSub>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null)
                    continue;

                var nearestCanvas = candidate.GetComponentInParent<UIBasePageCanvas>(true);
                if (nearestCanvas != this)
                    continue;

                pages.Add(candidate);
            }

            pages.Sort(ComparePagePanels);
        }

        private UIBasePageMain ResolveInitialMainPage()
        {
            if (TryGetCollectedMainPage(_initialPageIndex, out var configuredPage))
                return configuredPage;

            UIBasePageMain selectedPage = null;
            for (var i = 0; i < _mainPages.Count; i++)
            {
                var page = _mainPages[i];
                if (!page.gameObject.activeSelf)
                    continue;

                if (selectedPage == null || page.pageIndex < selectedPage.pageIndex)
                    selectedPage = page;
            }

            return selectedPage;
        }

        private bool TryGetCollectedMainPage(int pageIndex, out UIBasePageMain page)
        {
            for (var i = 0; i < _mainPages.Count; i++)
            {
                if (_mainPages[i].pageIndex == pageIndex)
                {
                    page = _mainPages[i];
                    return true;
                }
            }

            page = null;
            return false;
        }

        private bool IsOwnedMain(UIBasePageMain page)
        {
            return page != null && _mainPages.Contains(page);
        }

        private bool IsOwnedSub(UIBasePageSub page)
        {
            return page != null && _subPages.Contains(page);
        }

        private void MarkPagesDirty()
        {
            _pagesInitialized = false;
            _mainPages.Clear();
            _subPages.Clear();
        }

        private static int ComparePagePanels(UIBasePagePanel x, UIBasePagePanel y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            var compare = x.pageIndex.CompareTo(y.pageIndex);
            if (compare != 0)
                return compare;

            return x.transform.GetSiblingIndex().CompareTo(y.transform.GetSiblingIndex());
        }

    }

    public abstract class UIBasePageCanvas<TCanvas> : UIBasePageCanvas
        where TCanvas : UIBasePageCanvas
    {
        public static TCanvas Instance { get; private set; }

        protected void Awake()
        {
            Instance = this as TCanvas;
            _CacheCanvas();
            onAwake();
        }

        protected void OnDestroy()
        {
            if (Application.isPlaying
                && !BaseApplication.IsShuttingDown
                && !BaseApplication.IsApplicationQuitting)
                onDestroy();

            if (Instance == (this as TCanvas))
                Instance = null;
        }
    }
}
