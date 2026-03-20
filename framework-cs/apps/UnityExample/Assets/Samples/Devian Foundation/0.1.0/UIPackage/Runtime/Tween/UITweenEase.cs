using UnityEngine;

namespace Devian
{
    public enum UITweenEase
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad
    }

    internal static class UITweenEaseUtil
    {
        public static float Evaluate(UITweenEase ease, float t)
        {
            t = Mathf.Clamp01(t);

            switch (ease)
            {
                case UITweenEase.InQuad:
                    return t * t;

                case UITweenEase.OutQuad:
                    return 1f - ((1f - t) * (1f - t));

                case UITweenEase.InOutQuad:
                    if (t < 0.5f)
                    {
                        return 2f * t * t;
                    }

                    var inv = -2f * t + 2f;
                    return 1f - ((inv * inv) * 0.5f);

                default:
                    return t;
            }
        }
    }
}
