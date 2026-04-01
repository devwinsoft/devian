using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base class for UI panels.
    /// Provides initialization lifecycle with canvas owner reference.
    /// </summary>
    public abstract class UIBasePanel : MonoBehaviour
    {
        private static readonly MethodInfo s_spawnPoolableFrameMethod =
            typeof(UIBasePanel).GetMethod(nameof(SpawnPoolableFrame), BindingFlags.Static | BindingFlags.NonPublic);
        private bool _isInitComplete;
        private readonly List<UIBaseContainer> _containers = new List<UIBaseContainer>();
        private readonly List<UIBaseFrame> _ownedFrames = new List<UIBaseFrame>();

        /// <summary>Whether this panel has been initialized.</summary>
        public bool isInitialized { get; private set; }
        public bool isShown { get; private set; }

        /// <summary>캐시된 RectTransform. edit mode에서는 Awake 전에 접근될 수 있어 lazy resolve한다.</summary>
        public RectTransform rectTransform => _rectTransform != null ? _rectTransform : _rectTransform = transform as RectTransform;

        /// <summary>The owner (canvas) that initialized this panel.</summary>
        protected MonoBehaviour ownerBase { get; private set; }

        /// <summary>Unity Awake callback. Not virtual - use onAwake.</summary>
        protected void Awake()
        {
            _rectTransform = transform as RectTransform;
            isShown = gameObject.activeSelf;
            onAwake();
        }

        protected virtual void onAwake() { }

        protected void OnDestroy()
        {
            if (Application.isPlaying
                && !BaseApplication.IsShuttingDown
                && !BaseApplication.IsApplicationQuitting)
                onDestroy();
        }

        internal void _InitFromCanvas(MonoBehaviour owner)
        {
            if (isInitialized) return;
            ownerBase = owner;

            var canvasOwner = resolveOwnerCanvas();

            if (canvasOwner != null && canvasOwner.canvas != null)
            {
                _ownedFrames.Clear();
                _containers.Clear();

                UIBaseInitHelper.InitPanelOwnedSubtree(transform, canvasOwner.canvas, _ownedFrames);
                UIBaseInitHelper.CollectOwnedContainers(transform, _containers);

                for (var i = 0; i < _containers.Count; i++)
                    _containers[i]._Init(canvasOwner.canvas);
            }

            isInitialized = true;
            onInitFromCanvas(owner);
        }

        internal void _InitComplete()
        {
            if (_isInitComplete) return;
            _isInitComplete = true;

            for (var i = 0; i < _containers.Count; i++)
                _containers[i]._InitComplete();

            for (var i = 0; i < _ownedFrames.Count; i++)
                _ownedFrames[i]._InitComplete();

            onInitComplete();
            _containers.Clear();
            _ownedFrames.Clear();
        }

        internal void _HandleOwnerPoolSpawned()
        {
            isShown = gameObject.activeSelf;
            onPoolSpawned();

            var canvasOwner = resolveOwnerCanvas();
            if (isInitialized && canvasOwner != null && canvasOwner.canvas != null)
            {
                var ownedFrames = new List<UIBaseFrame>();
                UIBaseInitHelper.InitPanelOwnedSubtree(transform, canvasOwner.canvas, ownedFrames);

                var containers = new List<UIBaseContainer>();
                UIBaseInitHelper.CollectOwnedContainers(transform, containers);
                for (var i = 0; i < containers.Count; i++)
                    containers[i]._Init(canvasOwner.canvas);

                for (var i = 0; i < containers.Count; i++)
                    containers[i]._InitComplete();

                for (var i = 0; i < ownedFrames.Count; i++)
                    ownedFrames[i]._InitComplete();
            }
        }

        internal void _HandleOwnerPoolDespawned()
        {
            onPoolDespawned();

            if (isInitialized)
                UIBaseInitHelper.ResetPanelOwnedSubtree(transform);

            _containers.Clear();
            _ownedFrames.Clear();
            isShown = gameObject.activeSelf;
        }

        public void Show()
        {
            if (isShown && gameObject.activeSelf) return;

            isShown = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            onShow();
        }

        public void Hide()
        {
            if (!isShown && !gameObject.activeSelf) return;

            isShown = false;
            onHide();
        }

        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete() { }
        protected virtual void onPoolSpawned() { }
        protected virtual void onPoolDespawned() { }
        protected virtual void onShow() { }
        protected virtual void onHide() { gameObject.SetActive(false); }
        protected virtual void onDestroy() { }

        private RectTransform _rectTransform;

        // ─── CreateContainer ───

        /// <summary>
        /// 동적으로 container를 생성하고 현재 canvas lifecycle에 편입한다.
        /// root 이하 모든 UIBaseContainer subtree가 초기화된다.
        /// </summary>
        /// <typeparam name="T">Container 타입. UIBaseContainer + IPoolable.</typeparam>
        /// <param name="prefabName">BundlePool 에셋 이름.</param>
        /// <param name="parent">부모 Transform. null이면 이 panel의 transform.</param>
        /// <returns>생성된 container 인스턴스.</returns>
        public T CreateContainer<T>(string prefabName, Transform parent = null)
            where T : UIBaseContainer
        {
            var canvasOwner = resolveOwnerCanvas();
            if (canvasOwner == null)
            {
                throw new InvalidOperationException(
                    "UIBasePanel.CreateContainer: ownerCanvas is null. " +
                    "Panel must be initialized before creating containers.");
            }

            var instance = BundlePool.Spawn<T>(
                prefabName,
                parent: parent ?? transform);

            RegisterDynamicContainerTree(instance);
            return instance;
        }

        // ─── CreateFrame ───

        /// <summary>
        /// 동적으로 frame를 생성하고 현재 canvas lifecycle에 편입한다.
        /// root 이하 모든 UIBaseFrame subtree가 초기화된다.
        /// </summary>
        /// <typeparam name="T">Frame 타입. UIBaseFrame.</typeparam>
        /// <param name="prefabName">BundlePool 에셋 이름.</param>
        /// <param name="parent">부모 Transform. null이면 이 panel의 transform.</param>
        /// <returns>생성된 frame 인스턴스.</returns>
        public T CreateFrame<T>(string prefabName, Transform parent = null)
            where T : UIBaseFrame
        {
            var canvasOwner = resolveOwnerCanvas();
            if (canvasOwner == null)
            {
                throw new InvalidOperationException(
                    "UIBasePanel.CreateFrame: ownerCanvas is null. " +
                    "Panel must be initialized before creating frames.");
            }

            var instance = SpawnFrameInstance<T>(
                prefabName,
                parent ?? transform);

            RegisterDynamicFrameTree(instance);
            return instance;
        }

        private void RegisterDynamicContainerTree(UIBaseContainer root)
        {
            var canvasOwner = resolveOwnerCanvas();
            if (root == null || canvasOwner == null || canvasOwner.canvas == null)
                return;

            var containers = new List<UIBaseContainer>();
            UIBaseInitHelper.CollectOwnedContainers(root.transform, containers);

            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (!container.isContainerInitialized)
                    container._Init(canvasOwner.canvas);

                if (!_isInitComplete && !_containers.Contains(container))
                    _containers.Add(container);

                if (_isInitComplete)
                    container._InitComplete();
            }
        }

        private void RegisterDynamicFrameTree(UIBaseFrame root)
        {
            var canvasOwner = resolveOwnerCanvas();
            if (root == null || canvasOwner == null || canvasOwner.canvas == null)
                return;

            if (!root.isFrameInitialized)
                root._Init(canvasOwner.canvas);

            if (!_isInitComplete && !_ownedFrames.Contains(root))
                _ownedFrames.Add(root);

            if (_isInitComplete)
                root._InitComplete();
        }

        private static T SpawnFrameInstance<T>(string prefabName, Transform parent)
            where T : UIBaseFrame
        {
            if (typeof(IPoolable).IsAssignableFrom(typeof(T)))
            {
                try
                {
                    return s_spawnPoolableFrameMethod
                        .MakeGenericMethod(typeof(T))
                        .Invoke(null, new object[] { prefabName, parent }) as T;
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    throw e.InnerException;
                }
            }

            var prefab = AssetManager.GetAsset<GameObject>(prefabName);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"UIBasePanel.CreateFrame: prefab '{prefabName}' was not found in AssetManager cache.");
            }

            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            var frame = go.GetComponent<T>();
            if (frame != null)
                return frame;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);

            throw new InvalidOperationException(
                $"UIBasePanel.CreateFrame: prefab '{prefabName}' does not contain component '{typeof(T).Name}'.");
        }

        private static TFrame SpawnPoolableFrame<TFrame>(string prefabName, Transform parent)
            where TFrame : UIBaseFrame, IPoolable
        {
            return BundlePool.Spawn<TFrame>(prefabName, parent: parent);
        }

        private UIBaseCanvas resolveOwnerCanvas()
        {
            return ownerBase as UIBaseCanvas;
        }
    }

    /// <summary>
    /// Type-safe UIBasePanel with strongly-typed canvas reference.
    /// </summary>
    public abstract class UIBasePanel<TCanvas> : UIBasePanel
        where TCanvas : UIBaseCanvas
    {
        /// <summary>Strongly-typed canvas owner reference.</summary>
        protected TCanvas ownerCanvas { get; private set; }

        protected sealed override void onInitFromCanvas(MonoBehaviour canvasOwner)
        {
            ownerCanvas = canvasOwner as TCanvas;
            if (ownerCanvas == null)
            {
                Debug.LogError(
                    $"UIBasePanel<{typeof(TCanvas).Name}>.onInitFromCanvas: " +
                    $"Canvas owner is not of type {typeof(TCanvas).Name}. " +
                    $"Actual type: {canvasOwner?.GetType().Name ?? "null"}");
                return;
            }

            onInit(ownerCanvas);
        }

        protected virtual void onInit(TCanvas canvas) { }
    }
}
