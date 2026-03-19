using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base class for UI panels.
    /// Provides initialization lifecycle with canvas owner reference.
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        /// <summary>Whether this panel has been initialized.</summary>
        public bool isInitialized { get; private set; }

        /// <summary>캐시된 RectTransform.</summary>
        public RectTransform rectTransform { get; private set; }

        /// <summary>The owner (canvas) that initialized this panel.</summary>
        protected MonoBehaviour ownerBase { get; private set; }

        /// <summary>Owner canvas (비제네릭). ownerBase as UICanvas.</summary>
        protected UICanvas ownerCanvas => ownerBase as UICanvas;

        /// <summary>Unity Awake callback. Not virtual - use onAwake.</summary>
        protected void Awake()
        {
            rectTransform = (RectTransform)transform;
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
            isInitialized = true;
            onInitFromCanvas(owner);
        }

        internal void _InitComplete()
        {
            onInitComplete();
        }

        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete() { }
        protected virtual void onDestroy() { }

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
            if (ownerCanvas == null)
            {
                throw new InvalidOperationException(
                    "UIPanel.CreateContainer: ownerCanvas is null. " +
                    "Panel must be initialized before creating containers.");
            }

            var instance = BundlePool.Spawn<T>(
                prefabName,
                parent: parent ?? transform);

            ownerCanvas.RegisterDynamicContainerTree(instance);
            return instance;
        }
    }

    /// <summary>
    /// Type-safe UIPanel with strongly-typed canvas reference.
    /// </summary>
    public abstract class UIPanel<TCanvas> : UIPanel
        where TCanvas : UICanvas
    {
        /// <summary>Strongly-typed canvas owner reference.</summary>
        public TCanvas owner { get; private set; }

        protected sealed override void onInitFromCanvas(MonoBehaviour canvasOwner)
        {
            owner = canvasOwner as TCanvas;
            if (owner == null)
            {
                Debug.LogError(
                    $"UIPanel<{typeof(TCanvas).Name}>.onInitFromCanvas: " +
                    $"Canvas owner is not of type {typeof(TCanvas).Name}. " +
                    $"Actual type: {canvasOwner?.GetType().Name ?? "null"}");
                return;
            }

            onInit(owner);
        }

        protected virtual void onInit(TCanvas canvas) { }
    }
}
