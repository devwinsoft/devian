using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public abstract class UIBasePageCanvas : UIBaseCanvas
    {
        private readonly List<UIBasePagePanel> _pages = new List<UIBasePagePanel>();
        private UIBasePagePanel _currentPage;
        private int _navigationVersion;
        private bool _pagesInitialized;

        public UIBasePagePanel currentPage
        {
            get
            {
                EnsurePagesInitialized();
                return _currentPage;
            }
        }

        public bool isTransitioning { get; private set; }

        protected override void onInitComplete()
        {
            MarkPagesDirty();
            EnsurePagesInitialized();
            onPageCanvasInitComplete();
        }

        protected sealed override void onPoolSpawned()
        {
            _navigationVersion++;
            isTransitioning = false;

            MarkPagesDirty();
            EnsurePagesInitialized();
            onPageCanvasPoolSpawned();
        }

        protected sealed override void onPoolDespawned()
        {
            CancelAllPageTransitions();
            _navigationVersion++;
            _currentPage = null;
            isTransitioning = false;

            MarkPagesDirty();
            onPageCanvasPoolDespawned();
        }

        protected virtual void onPageCanvasInitComplete() { }
        protected virtual void onPageCanvasPoolSpawned() { }
        protected virtual void onPageCanvasPoolDespawned() { }

        public void ShowPage(int pageIndex, bool animated = true)
        {
            if (!TryGetPage(pageIndex, out var page))
            {
                Debug.LogWarning($"[{GetType().Name}] Page with index '{pageIndex}' was not found.", this);
                return;
            }

            ShowPage(page, animated);
        }

        public void ShowPage(UIBasePagePanel page, bool animated = true)
        {
            if (page == null)
            {
                Debug.LogWarning($"[{GetType().Name}] ShowPage: target page is null.", this);
                return;
            }

            EnsurePagesInitialized();

            if (!IsOwnedPage(page))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] ShowPage: page '{page.name}' does not belong to this canvas.",
                    this);
                return;
            }

            if (isTransitioning)
            {
                if (_currentPage == page)
                    return;

                NormalizeToCurrentPage();
            }

            if (_currentPage == page)
                return;

            if (_currentPage == null)
            {
                NormalizeToPage(page);
                return;
            }

            StartTransition(page, animated);
        }

        public bool TryGetPage(int pageIndex, out UIBasePagePanel page)
        {
            EnsurePagesInitialized();

            for (var i = 0; i < _pages.Count; i++)
            {
                if (_pages[i].pageIndex == pageIndex)
                {
                    page = _pages[i];
                    return true;
                }
            }

            page = null;
            return false;
        }

        public override bool Validate(out string reason)
        {
            if (!base.Validate(out reason))
                return false;

            CollectPages(_pages);

            var pageIndexes = new HashSet<int>();
            for (var i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (!pageIndexes.Add(page.pageIndex))
                {
                    reason = $"Duplicate pageIndex detected: {page.pageIndex}";
                    return false;
                }

                if (!page._ValidateTransitionSetup(out reason))
                    return false;
            }

            reason = null;
            return true;
        }

        private void StartTransition(UIBasePagePanel targetPage, bool animated)
        {
            var fromPage = _currentPage;
            var direction = fromPage.pageIndex < targetPage.pageIndex
                ? UIPageTransitionDirection.Left
                : UIPageTransitionDirection.Right;

            _navigationVersion++;
            var version = _navigationVersion;

            isTransitioning = true;
            _currentPage = targetPage;

            for (var i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (page == fromPage || page == targetPage)
                    continue;

                page._NormalizeHidden();
            }

            var pendingCompletions = 2;

            void CompleteStep()
            {
                if (version != _navigationVersion)
                    return;

                pendingCompletions--;
                if (pendingCompletions > 0)
                    return;

                isTransitioning = false;
                NormalizeToPage(targetPage);
            }

            targetPage._Enter(direction, animated, fromPage, CompleteStep);
            fromPage._Exit(direction, animated, targetPage, CompleteStep);
        }

        private void EnsurePagesInitialized()
        {
            if (!isInitialized || _pagesInitialized)
                return;

            _pagesInitialized = true;
            CollectPages(_pages);

            UIBasePagePanel selectedPage = null;
            for (var i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (!page.gameObject.activeSelf)
                    continue;

                if (selectedPage == null || page.pageIndex < selectedPage.pageIndex)
                    selectedPage = page;
            }

            NormalizeToPage(selectedPage);
        }

        private void NormalizeToCurrentPage()
        {
            if (_currentPage != null && !IsOwnedPage(_currentPage))
                _currentPage = null;

            NormalizeToPage(_currentPage);
        }

        private void NormalizeToPage(UIBasePagePanel targetPage)
        {
            CancelAllPageTransitions();

            for (var i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (page == targetPage)
                {
                    page._NormalizeVisible(makeCurrent: true);
                    continue;
                }

                page._NormalizeHidden();
            }

            _currentPage = targetPage;
            isTransitioning = false;
        }

        private void CancelAllPageTransitions()
        {
            for (var i = 0; i < _pages.Count; i++)
                _pages[i]._CancelPageTransition();
        }

        private void CollectPages(List<UIBasePagePanel> pages)
        {
            pages.Clear();

            var candidates = GetComponentsInChildren<UIBasePagePanel>(true);
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

            pages.Sort(ComparePages);
        }

        private bool IsOwnedPage(UIBasePagePanel page)
        {
            return page != null && _pages.Contains(page);
        }

        private void MarkPagesDirty()
        {
            _pagesInitialized = false;
            _pages.Clear();
        }

        private static int ComparePages(UIBasePagePanel x, UIBasePagePanel y)
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
