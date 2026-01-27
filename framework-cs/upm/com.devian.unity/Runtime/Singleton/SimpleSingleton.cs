// Unity Singleton - SimpleSingleton<T>
// SSOT: skills/devian-unity/30-unity-components/01-singleton/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using System.Reflection;
#endif

namespace Devian
{
    /// <summary>
    /// Pure C# singleton base using Lazy&lt;T&gt;.
    /// Thread-safe and simple.
    /// No Unity/MonoBehaviour dependency.
    /// </summary>
    /// <typeparam name="T">Concrete singleton type</typeparam>
    public abstract class SimpleSingleton<T>
        where T : SimpleSingleton<T>, new()
    {
        private static readonly Lazy<T> _instance = new(() =>
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _EnsureNotInCtorOrStaticInit();
#endif
            return new T();
        }, isThreadSafe: true);

        /// <summary>
        /// Gets the singleton instance. Created on first access.
        /// </summary>
        public static T Instance => _instance.Value;

        /// <summary>
        /// Protected constructor to prevent external instantiation.
        /// </summary>
        protected SimpleSingleton() { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Guard: Detects ctor/static init access patterns to avoid Unity Editor ScriptableSingleton issues.
        /// Only runs once during first Instance access (inside Lazy lambda).
        /// Ignores System/Unity internal frames; only checks user/editor script code.
        /// </summary>
        private static void _EnsureNotInCtorOrStaticInit()
        {
            var st = new StackTrace(fNeedFileInfo: false);
            var frames = st.GetFrames();
            if (frames == null) return;

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method == null) continue;

                // Skip system/unity internal frames
                if (_IsInternalFrame(method)) continue;

                var declaringType = method.DeclaringType;

                // Check: constructor (user code)
                if (method is ConstructorInfo)
                {
                    throw new InvalidOperationException(
                        $"[SimpleSingleton<{typeof(T).Name}>] " +
                        "Do not access Instance from constructors, static initializers, or InitializeOnLoad. " +
                        "Use EditorApplication.delayCall or an explicit Init() method.");
                }

                // Check: static constructor (.cctor, user code)
                if (method.IsStatic && method.Name == ".cctor")
                {
                    throw new InvalidOperationException(
                        $"[SimpleSingleton<{typeof(T).Name}>] " +
                        "Do not access Instance from constructors, static initializers, or InitializeOnLoad. " +
                        "Use EditorApplication.delayCall or an explicit Init() method.");
                }

#if UNITY_EDITOR
                // Check: InitializeOnLoad / InitializeOnLoadMethod (editor only, reflection-based)
                if (_HasInitializeOnLoadAttribute(method, declaringType))
                {
                    throw new InvalidOperationException(
                        $"[SimpleSingleton<{typeof(T).Name}>] " +
                        "Do not access Instance from constructors, static initializers, or InitializeOnLoad. " +
                        "Use EditorApplication.delayCall or an explicit Init() method.");
                }
#endif
            }
        }

        /// <summary>
        /// Returns true if the frame belongs to System, Microsoft, Unity internals (should be ignored).
        /// </summary>
        private static bool _IsInternalFrame(MethodBase method)
        {
            var declaringType = method.DeclaringType;
            if (declaringType == null) return true;

            // SimpleSingleton itself and Lazy<T> are always ignored
            if (declaringType == typeof(SimpleSingleton<T>)) return true;
            if (declaringType.FullName != null && declaringType.FullName.StartsWith("System.Lazy")) return true;

            var asm = declaringType.Assembly;
            if (asm == null) return true;

            var asmName = asm.GetName().Name;
            if (string.IsNullOrEmpty(asmName)) return true;

            // Ignore System/Microsoft/Unity assemblies
            if (asmName.StartsWith("System", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("Microsoft", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("mscorlib", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("netstandard", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("Unity", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("UnityEngine", StringComparison.Ordinal)) return true;
            if (asmName.StartsWith("UnityEditor", StringComparison.Ordinal)) return true;

            // Also check namespace for completeness
            var ns = declaringType.Namespace;
            if (!string.IsNullOrEmpty(ns))
            {
                if (ns.StartsWith("System", StringComparison.Ordinal)) return true;
                if (ns.StartsWith("Microsoft", StringComparison.Ordinal)) return true;
                if (ns.StartsWith("UnityEngine", StringComparison.Ordinal)) return true;
                if (ns.StartsWith("UnityEditor", StringComparison.Ordinal)) return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Checks if method or declaring type has InitializeOnLoad(Method) attribute via reflection.
        /// We avoid direct using UnityEditor to keep Development build safe.
        /// </summary>
        private static bool _HasInitializeOnLoadAttribute(MethodBase method, Type declaringType)
        {
            const string InitializeOnLoadAttr = "UnityEditor.InitializeOnLoadAttribute";
            const string InitializeOnLoadMethodAttr = "UnityEditor.InitializeOnLoadMethodAttribute";

            // Check method attributes
            try
            {
                var methodAttrs = method.GetCustomAttributes(false);
                foreach (var attr in methodAttrs)
                {
                    var attrTypeName = attr.GetType().FullName;
                    if (attrTypeName == InitializeOnLoadMethodAttr)
                        return true;
                }
            }
            catch { /* ignore reflection errors */ }

            // Check declaring type attributes
            if (declaringType != null)
            {
                try
                {
                    var typeAttrs = declaringType.GetCustomAttributes(false);
                    foreach (var attr in typeAttrs)
                    {
                        var attrTypeName = attr.GetType().FullName;
                        if (attrTypeName == InitializeOnLoadAttr)
                            return true;
                    }
                }
                catch { /* ignore reflection errors */ }
            }

            return false;
        }
#endif
#endif
    }
}
