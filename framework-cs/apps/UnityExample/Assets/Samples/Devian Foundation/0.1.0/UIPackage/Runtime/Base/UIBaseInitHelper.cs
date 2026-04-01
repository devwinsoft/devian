using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    internal static class UIBaseInitHelper
    {
        internal static void InitCanvasOwnedSubtree(Transform root, Canvas canvas)
        {
            InitComponentsOnTransform(root, canvas);

            for (var i = 0; i < root.childCount; i++)
                InitCanvasOwnedChild(root.GetChild(i), canvas);
        }

        internal static void InitPanelOwnedSubtree(Transform root, Canvas canvas, List<UIBaseFrame> ownedFrames)
        {
            InitComponentsOnTransform(root, canvas);

            for (var i = 0; i < root.childCount; i++)
                InitOwnedChild(root.GetChild(i), canvas, ownedFrames, null, stopAtPanels: true);
        }

        internal static void InitOwnedSubtree(
            Transform root,
            Canvas canvas,
            List<UIBaseFrame> ownedFrames = null,
            UIBaseFrame selfFrame = null)
        {
            InitComponentsOnTransform(root, canvas);

            for (var i = 0; i < root.childCount; i++)
                InitOwnedChild(root.GetChild(i), canvas, ownedFrames, selfFrame, stopAtPanels: false);
        }

        internal static void CollectOwnedContainers(Transform root, List<UIBaseContainer> containers)
        {
            if (root == null || containers == null)
                return;

            CollectOwnedContainersRecursive(root, root, containers);
        }

        internal static void ResetCanvasOwnedSubtree(Transform root)
        {
            ResetComponentsOnTransform(root);

            for (var i = 0; i < root.childCount; i++)
                ResetCanvasOwnedChild(root.GetChild(i));
        }

        internal static void ResetPanelOwnedSubtree(Transform root)
        {
            ResetComponentsOnTransform(root);

            for (var i = 0; i < root.childCount; i++)
                ResetOwnedChildForPool(root.GetChild(i), null, stopAtPanels: true);
        }

        internal static void ResetOwnedSubtree(Transform root, UIBaseFrame selfFrame = null)
        {
            ResetComponentsOnTransform(root);

            for (var i = 0; i < root.childCount; i++)
                ResetOwnedChildForPool(root.GetChild(i), selfFrame, stopAtPanels: false);
        }

        private static void InitCanvasOwnedChild(Transform current, Canvas canvas)
        {
            if (current.GetComponent<UIBasePanel>() != null
                || current.GetComponent<UIBaseContainer>() != null
                || current.GetComponent<UIBaseFrame>() != null)
                return;

            InitComponentsOnTransform(current, canvas);

            for (var i = 0; i < current.childCount; i++)
                InitCanvasOwnedChild(current.GetChild(i), canvas);
        }

        private static void InitOwnedChild(
            Transform current,
            Canvas canvas,
            List<UIBaseFrame> ownedFrames,
            UIBaseFrame selfFrame,
            bool stopAtPanels)
        {
            if (stopAtPanels && current.GetComponent<UIBasePanel>() != null)
                return;

            if (current.GetComponent<UIBaseContainer>() != null)
                return;

            var frames = current.GetComponents<UIBaseFrame>();
            if (frames.Length > 0)
            {
                for (var i = 0; i < frames.Length; i++)
                {
                    if (frames[i] == selfFrame) continue;
                    frames[i]._Init(canvas);
                    if (ownedFrames != null && !ownedFrames.Contains(frames[i]))
                        ownedFrames.Add(frames[i]);
                }
                return;
            }

            InitComponentsOnTransform(current, canvas);

            for (var i = 0; i < current.childCount; i++)
                InitOwnedChild(current.GetChild(i), canvas, ownedFrames, selfFrame, stopAtPanels);
        }

        private static void ResetCanvasOwnedChild(Transform current)
        {
            if (current.GetComponent<UIBasePanel>() != null)
                return;

            var containers = current.GetComponents<UIBaseContainer>();
            if (containers.Length > 0)
            {
                for (var i = 0; i < containers.Length; i++)
                    containers[i]._HandlePoolDespawnedRecursive();
                return;
            }

            var frames = current.GetComponents<UIBaseFrame>();
            if (frames.Length > 0)
            {
                for (var i = 0; i < frames.Length; i++)
                    frames[i]._HandlePoolDespawnedRecursive();
                return;
            }

            ResetComponentsOnTransform(current);

            for (var i = 0; i < current.childCount; i++)
                ResetCanvasOwnedChild(current.GetChild(i));
        }

        private static void ResetOwnedChildForPool(
            Transform current,
            UIBaseFrame selfFrame,
            bool stopAtPanels)
        {
            if (stopAtPanels && current.GetComponent<UIBasePanel>() != null)
                return;

            var containers = current.GetComponents<UIBaseContainer>();
            if (containers.Length > 0)
            {
                for (var i = 0; i < containers.Length; i++)
                    containers[i]._HandlePoolDespawnedRecursive();
                return;
            }

            var frames = current.GetComponents<UIBaseFrame>();
            if (frames.Length > 0)
            {
                for (var i = 0; i < frames.Length; i++)
                {
                    if (frames[i] == selfFrame) continue;
                    frames[i]._HandlePoolDespawnedRecursive();
                }
                return;
            }

            ResetComponentsOnTransform(current);

            for (var i = 0; i < current.childCount; i++)
                ResetOwnedChildForPool(current.GetChild(i), selfFrame, stopAtPanels);
        }

        private static void CollectOwnedContainersRecursive(
            Transform current,
            Transform traversalRoot,
            List<UIBaseContainer> containers)
        {
            if (current != traversalRoot && current.GetComponent<UIBasePanel>() != null)
                return;

            var ownedContainers = current.GetComponents<UIBaseContainer>();
            for (var i = 0; i < ownedContainers.Length; i++)
            {
                if (!containers.Contains(ownedContainers[i]))
                    containers.Add(ownedContainers[i]);
            }

            for (var i = 0; i < current.childCount; i++)
                CollectOwnedContainersRecursive(current.GetChild(i), traversalRoot, containers);
        }

        private static void InitComponentsOnTransform(Transform current, Canvas canvas)
        {
            var components = current.GetComponents<UIComponentBase>();
            for (var i = 0; i < components.Length; i++)
                components[i].Init(canvas);
        }

        private static void ResetComponentsOnTransform(Transform current)
        {
            var components = current.GetComponents<UIComponentBase>();
            for (var i = 0; i < components.Length; i++)
                components[i]._ResetForPool();
        }
    }
}
