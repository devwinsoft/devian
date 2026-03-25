using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    internal static class UILoadingUIUtil
    {
        private static Font s_builtinFont;

        public static RectTransform EnsureRectTransform(GameObject gameObject)
        {
            return gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        }

        public static RectTransform CreateUIObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        public static void StretchFull(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
        }

        public static void CenterRect(RectTransform rectTransform, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = size;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
        }

        public static Image GetOrAddImage(GameObject gameObject)
        {
            return gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        }

        public static CanvasRenderer EnsureCanvasRenderer(GameObject gameObject)
        {
            return gameObject.GetComponent<CanvasRenderer>() ?? gameObject.AddComponent<CanvasRenderer>();
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var rectTransform = CreateUIObject(name, parent);
            var gameObject = rectTransform.gameObject;
            EnsureCanvasRenderer(gameObject);

            var uiText = gameObject.AddComponent<Text>();
            uiText.font = ResolveBuiltinFont();
            uiText.text = text;
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = color;
            uiText.raycastTarget = false;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            return uiText;
        }

        public static Font ResolveBuiltinFont()
        {
            if (s_builtinFont == null)
            {
                s_builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return s_builtinFont;
        }
    }
}
