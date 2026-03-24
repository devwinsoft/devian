using UnityEngine;

namespace Devian
{
    internal static class UIBaseInitHelper
    {
        internal static void InitOwnedSubtree(Transform root, Canvas canvas, UIBaseFrame selfFrame = null)
        {
            InitComponentsOnTransform(root, canvas);

            for (var i = 0; i < root.childCount; i++)
                InitOwnedChild(root.GetChild(i), canvas, selfFrame);
        }

        private static void InitOwnedChild(Transform current, Canvas canvas, UIBaseFrame selfFrame)
        {
            if (current.GetComponent<UIBaseContainer>() != null)
                return;

            var frames = current.GetComponents<UIBaseFrame>();
            if (frames.Length > 0)
            {
                for (var i = 0; i < frames.Length; i++)
                {
                    if (frames[i] == selfFrame) continue;
                    frames[i]._Init(canvas);
                }
                return;
            }

            InitComponentsOnTransform(current, canvas);

            for (var i = 0; i < current.childCount; i++)
                InitOwnedChild(current.GetChild(i), canvas, selfFrame);
        }

        private static void InitComponentsOnTransform(Transform current, Canvas canvas)
        {
            var components = current.GetComponents<UIComponentBase>();
            for (var i = 0; i < components.Length; i++)
                components[i].Init(canvas);
        }
    }
}
