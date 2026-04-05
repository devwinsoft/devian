using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    public sealed class UIToastGroup
    {
        private readonly UIToastCanvas _canvas;
        private readonly RectTransform _root;
        private readonly UI_TOAST_FRAME_ID _toastFrameId;
        private readonly ToastGroupConfig _config;
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly List<UIToastFrame> _active = new List<UIToastFrame>();

        public UIToastGroup(
            UIToastCanvas canvas,
            RectTransform root,
            UI_TOAST_FRAME_ID toastFrameId,
            ToastGroupConfig config)
        {
            _canvas = canvas;
            _root = root;
            _toastFrameId = toastFrameId;
            _config = config ?? new ToastGroupConfig();

            ApplyRootLayout();
        }

        public void Enqueue(string message)
        {
            if (TryHandleDuplicate(message))
            {
                return;
            }

            if (_active.Count < ResolveMaxVisibleCount())
            {
                ShowImmediate(message);
                return;
            }

            _pending.Enqueue(message);
        }

        public void Clear()
        {
            _pending.Clear();

            for (var i = 0; i < _active.Count; i++)
            {
                var frame = _active[i];
                if (frame != null)
                {
                    BundlePool.Despawn(frame);
                }
            }

            _active.Clear();
        }

        private bool TryHandleDuplicate(string message)
        {
            switch (_config.DuplicatePolicy)
            {
                case ToastDuplicatePolicy.IgnoreIfVisible:
                    return FindVisibleFrame(message) != null;

                case ToastDuplicatePolicy.RefreshDurationIfVisible:
                    var frame = FindVisibleFrame(message);
                    if (frame == null)
                    {
                        return false;
                    }

                    frame.RefreshDuration(ResolveDuration());
                    return true;

                default:
                    return false;
            }
        }

        private UIToastFrame FindVisibleFrame(string message)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                var frame = _active[i];
                if (frame == null || frame.isHiding)
                {
                    continue;
                }

                if (frame.HasMessage(message))
                {
                    return frame;
                }
            }

            return null;
        }

        private void ShowImmediate(string message)
        {
            if (_toastFrameId == null || !_toastFrameId.IsValid)
            {
                Debug.LogError($"[UIToastGroup] ToastFrameId is invalid for group '{_config.GroupId}'.");
                return;
            }

            UIToastFrame frame;
            try
            {
                frame = BundlePool.Spawn<UIToastFrame>(_toastFrameId, parent: _root);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIToastGroup] Failed to spawn toast frame '{_toastFrameId}': {e.Message}");
                return;
            }

            if (!frame.isFrameInitialized)
            {
                frame._Init(_canvas.canvas);
                frame._InitComplete();
            }

            frame.Bind(message);
            frame.transform.SetAsLastSibling();
            ApplyFrameOffset(frame, ResolveNextFrameOffset());

            _active.Add(frame);

            frame.Show(ResolveDuration(), OnFrameHidden);
        }

        private void OnFrameHidden(UIToastFrame frame)
        {
            var removed = _active.Remove(frame);
            if (removed && frame != null)
            {
                BundlePool.Despawn(frame);
            }

            Relayout();
            FlushQueue();
        }

        private void FlushQueue()
        {
            while (_active.Count < ResolveMaxVisibleCount() && _pending.Count > 0)
            {
                var next = _pending.Dequeue();
                if (TryHandleDuplicate(next))
                {
                    continue;
                }

                ShowImmediate(next);
            }
        }

        private void Relayout()
        {
            ApplyRootLayout();
            var offset = Vector2.zero;

            for (var i = 0; i < _active.Count; i++)
            {
                var frame = _active[i];
                if (frame == null)
                {
                    continue;
                }

                var size = ApplyFrameOffset(frame, offset);
                offset += ResolveStep(size);
            }
        }

        private Vector2 ResolveNextFrameOffset()
        {
            var offset = Vector2.zero;

            for (var i = 0; i < _active.Count; i++)
            {
                var frame = _active[i];
                if (frame == null)
                {
                    continue;
                }

                var size = ResolveFrameSize(frame);
                offset += ResolveStep(size);
            }

            return offset;
        }

        private Vector2 ApplyFrameOffset(UIToastFrame frame, Vector2 localOffset)
        {
            if (frame == null)
            {
                return Vector2.zero;
            }

            var frameRect = frame.rectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(frameRect);
            var frameSize = ResolveSize(frameRect);

            frame.ApplyGroupOffset(ResolveAnchorOffset(frameSize) + _config.AnchoredOffset + localOffset);
            LayoutRebuilder.ForceRebuildLayoutImmediate(frameRect);
            return frameSize;
        }

        private static Vector2 ResolveFrameSize(UIToastFrame frame)
        {
            if (frame == null)
            {
                return Vector2.zero;
            }

            var frameRect = frame.rectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(frameRect);
            return ResolveSize(frameRect);
        }

        private void ApplyRootLayout()
        {
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = Vector2.zero;
            _root.localScale = Vector3.one;
        }

        private Vector2 ResolveAnchorOffset(Vector2 frameSize)
        {
            var anchor = ResolveAnchorPreset(_config.AnchorPreset);
            var rootSize = _root.rect.size;
            return new Vector2(
                (rootSize.x - frameSize.x) * (anchor.x - 0.5f),
                (rootSize.y - frameSize.y) * (anchor.y - 0.5f));
        }

        private static Vector2 ResolveAnchorPreset(ToastAnchorPreset anchorPreset)
        {
            switch (anchorPreset)
            {
                case ToastAnchorPreset.TopLeft:
                    return new Vector2(0f, 1f);
                case ToastAnchorPreset.TopCenter:
                    return new Vector2(0.5f, 1f);
                case ToastAnchorPreset.TopRight:
                    return new Vector2(1f, 1f);
                case ToastAnchorPreset.MiddleLeft:
                    return new Vector2(0f, 0.5f);
                case ToastAnchorPreset.MiddleCenter:
                    return new Vector2(0.5f, 0.5f);
                case ToastAnchorPreset.MiddleRight:
                    return new Vector2(1f, 0.5f);
                case ToastAnchorPreset.BottomLeft:
                    return new Vector2(0f, 0f);
                case ToastAnchorPreset.BottomCenter:
                    return new Vector2(0.5f, 0f);
                case ToastAnchorPreset.BottomRight:
                    return new Vector2(1f, 0f);
                default:
                    return new Vector2(0.5f, 1f);
            }
        }

        private Vector2 ResolveStep(Vector2 size)
        {
            var spacing = UIToastDefaults.DefaultSpacing;
            var step = size.y + spacing;
            return _config.LayoutDirection == ToastLayoutDirection.Up
                ? new Vector2(0f, step)
                : new Vector2(0f, -step);
        }

        private static Vector2 ResolveSize(RectTransform rect)
        {
            var width = LayoutUtility.GetPreferredWidth(rect);
            if (width <= 0f)
            {
                width = rect.rect.width;
            }

            var height = LayoutUtility.GetPreferredHeight(rect);
            if (height <= 0f)
            {
                height = rect.rect.height;
            }

            return new Vector2(width, height);
        }

        private int ResolveMaxVisibleCount()
        {
            return Mathf.Max(1, _config.MaxVisibleCount);
        }

        private float ResolveDuration()
        {
            if (_config.DefaultDuration > 0f)
            {
                return _config.DefaultDuration;
            }

            return UIToastDefaults.DefaultDuration;
        }

    }
}
