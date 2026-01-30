// Unity Singleton - MonoSingleton<T>
// SSOT: skills/devian-unity/30-unity-components/01-singleton/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// [Obsolete] Use SceneSingleton&lt;T&gt; for scene-placed singletons,
    /// or AutoSingleton&lt;T&gt; for auto-creating singletons.
    /// 
    /// This class is a thin wrapper over SceneSingleton for backwards compatibility.
    /// </summary>
    /// <typeparam name="T">Concrete singleton type</typeparam>
    [Obsolete("Use SceneSingleton<T> or AutoSingleton<T> instead.")]
    public abstract class MonoSingleton<T> : SceneSingleton<T> where T : MonoSingleton<T>
    {
        // All functionality inherited from SceneSingleton<T>
    }
}
