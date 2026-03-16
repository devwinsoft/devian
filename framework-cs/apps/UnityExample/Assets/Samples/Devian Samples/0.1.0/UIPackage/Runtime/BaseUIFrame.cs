using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base class for UI frames.
    /// Provides initialization lifecycle with canvas owner reference.
    /// </summary>
    public abstract class BaseUIFrame : MonoBehaviour
    {
        /// <summary>
        /// Whether this frame has been initialized.
        /// </summary>
        public bool isInitialized { get; private set; }

        /// <summary>
        /// The owner (canvas) that initialized this frame.
        /// Available after _InitFromCanvas is called.
        /// </summary>
        protected MonoBehaviour ownerBase { get; private set; }

        /// <summary>
        /// Unity Awake callback. Not virtual - use onAwake for custom initialization.
        /// </summary>
        protected void Awake()
        {
            onAwake();
        }

        /// <summary>
        /// Override this for custom Awake logic.
        /// Called before _InitFromCanvas.
        /// </summary>
        protected virtual void onAwake() { }

        /// <summary>
        /// Initializes this frame from the canvas owner.
        /// Called by UICanvas during initialization.
        /// Can only be called once.
        /// </summary>
        /// <param name="owner">The canvas owner (MonoBehaviour).</param>
        internal void _InitFromCanvas(MonoBehaviour owner)
        {
            if (isInitialized)
            {
                return;
            }

            ownerBase = owner;
            isInitialized = true;
            onInitFromCanvas(owner);
        }

        /// <summary>
        /// Called after ownerBase is set and isInitialized is true.
        /// Derived classes must implement this to handle initialization.
        /// </summary>
        /// <param name="owner">The canvas owner.</param>
        protected abstract void onInitFromCanvas(MonoBehaviour owner);
    }

    /// <summary>
    /// Type-safe UIFrame with strongly-typed canvas reference.
    /// </summary>
    /// <typeparam name="TCanvas">The canvas type.</typeparam>
    public abstract class UIFrame<TCanvas> : BaseUIFrame
        where TCanvas : MonoBehaviour
    {
        /// <summary>
        /// Strongly-typed canvas owner reference.
        /// </summary>
        public TCanvas owner { get; private set; }

        /// <summary>
        /// Handles initialization from canvas.
        /// Casts owner to TCanvas and calls typed onInit.
        /// </summary>
        /// <param name="canvasOwner">The canvas owner.</param>
        protected sealed override void onInitFromCanvas(MonoBehaviour canvasOwner)
        {
            owner = canvasOwner as TCanvas;
            if (owner == null)
            {
                Debug.LogError(
                    $"UIFrame<{typeof(TCanvas).Name}>.onInitFromCanvas: " +
                    $"Canvas owner is not of type {typeof(TCanvas).Name}. " +
                    $"Actual type: {canvasOwner?.GetType().Name ?? "null"}");
                return;
            }

            onInit(owner);
        }

        /// <summary>
        /// Override this for custom initialization logic with typed canvas.
        /// </summary>
        /// <param name="canvas">The strongly-typed canvas owner.</param>
        protected virtual void onInit(TCanvas canvas) { }
    }
}
