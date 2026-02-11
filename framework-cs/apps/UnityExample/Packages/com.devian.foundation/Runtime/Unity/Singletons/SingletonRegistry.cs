// SSOT: skills/devian-unity/10-base-system/31-singleton/SKILL.md

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 싱글톤 레지스트리 (SSOT 저장소).
    /// 직접 사용하지 말고 Singleton 파사드를 통해 접근.
    ///
    /// 우선순위: Compo > Boot > Auto
    /// - 높은 우선순위가 낮은 우선순위를 대체(Adopt)
    /// - 동일 우선순위 중복은 예외
    /// </summary>
    public static class SingletonRegistry
    {
        private readonly struct Entry
        {
            public readonly object Instance;
            public readonly SingletonSource Source;
            public readonly string DebugSource;

            public Entry(object instance, SingletonSource source, string debugSource)
            {
                Instance = instance;
                Source = source;
                DebugSource = debugSource ?? "unknown";
            }
        }

        private static readonly Dictionary<Type, Entry> _entries = new Dictionary<Type, Entry>();

        /// <summary>
        /// 인스턴스 등록. 우선순위에 따라 Adopt 또는 예외.
        /// </summary>
        /// <returns>true면 등록 성공, false면 기존 인스턴스 유지(신규가 낮은 우선순위)</returns>
        public static bool Register<T>(T instance, SingletonSource source, string debugSource = null)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = typeof(T);
            var newEntry = new Entry(instance, source, debugSource);

            if (_entries.TryGetValue(type, out var existing))
            {
                // 동일 인스턴스 재등록은 idempotent (동일 source일 때만)
                if (existing.Source == source && ReferenceEquals(existing.Instance, instance))
                {
                    return true; // 이미 등록된 동일 인스턴스 - no-op
                }

                // 동일 source 중복은 예외 (다른 인스턴스)
                if (existing.Source == source)
                {
                    throw new InvalidOperationException(
                        $"[SingletonRegistry] Duplicate {source} registration for '{type.Name}'. " +
                        $"Existing: '{existing.DebugSource}', New: '{debugSource ?? "unknown"}'");
                }

                // 우선순위 비교: 신규가 더 높으면 Adopt
                if (source > existing.Source)
                {
                    // Adopt: 기존 인스턴스 파괴 + 신규 등록
                    AdoptAndDestroy(type, existing, newEntry);
                    _entries[type] = newEntry;
                    return true;
                }
                else
                {
                    // 기존이 더 높음: 신규 무시 (신규가 Auto인데 이미 Compo/Boot가 있는 경우)
                    Debug.LogWarning(
                        $"[SingletonRegistry] Ignoring {source} registration for '{type.Name}' " +
                        $"(existing {existing.Source} has higher priority)");
                    return false;
                }
            }

            // 신규 등록
            _entries[type] = newEntry;
            return true;
        }

        /// <summary>
        /// Adopt 처리: 기존 인스턴스를 파괴하고 신규로 교체.
        /// </summary>
        private static void AdoptAndDestroy(Type type, Entry oldEntry, Entry newEntry)
        {
            var oldSource = oldEntry.Source;
            var newSource = newEntry.Source;

            // Error 로그 (Compo가 Auto/Boot를 대체하는 경우)
            if (newSource == SingletonSource.Compo)
            {
                Debug.LogError(
                    $"[SingletonRegistry] ADOPT: {newSource} is replacing {oldSource} for '{type.Name}'. " +
                    $"Old: '{oldEntry.DebugSource}', New: '{newEntry.DebugSource}'. " +
                    "This may indicate a design issue - consider using CompoSingleton from the start.");
            }
            else
            {
                Debug.LogWarning(
                    $"[SingletonRegistry] ADOPT: {newSource} is replacing {oldSource} for '{type.Name}'. " +
                    $"Old: '{oldEntry.DebugSource}', New: '{newEntry.DebugSource}'");
            }

            // 기존 인스턴스 파괴 (Component면 컴포넌트만 파괴, GameObject 전체 파괴 금지)
            if (oldEntry.Instance is Component comp)
            {
                UnityEngine.Object.Destroy(comp);
            }
            else if (oldEntry.Instance is UnityEngine.Object unityObj)
            {
                UnityEngine.Object.Destroy(unityObj);
            }
            // 순수 C# 객체는 파괴 불가, 참조만 교체
        }

        /// <summary>
        /// 인스턴스 등록 해제. 현재 등록된 인스턴스와 같을 때만 해제.
        /// </summary>
        public static void Unregister<T>(T instance)
        {
            if (instance == null)
                return;

            var type = typeof(T);

            if (_entries.TryGetValue(type, out var entry) && ReferenceEquals(entry.Instance, instance))
            {
                _entries.Remove(type);
            }
        }

        /// <summary>
        /// 인스턴스 조회. 없으면 예외.
        /// </summary>
        public static T Get<T>()
        {
            var type = typeof(T);

            if (!_entries.TryGetValue(type, out var entry))
            {
                throw new InvalidOperationException(
                    $"[SingletonRegistry] No instance registered for type '{type.Name}'. " +
                    "Ensure the singleton is instantiated before access.");
            }

            return (T)entry.Instance;
        }

        /// <summary>
        /// 인스턴스 조회. 없으면 false.
        /// </summary>
        public static bool TryGet<T>(out T instance)
        {
            var type = typeof(T);

            if (_entries.TryGetValue(type, out var entry))
            {
                instance = (T)entry.Instance;
                return true;
            }

            instance = default;
            return false;
        }

        /// <summary>
        /// 인스턴스와 소스 정보 조회 (내부용).
        /// </summary>
        internal static bool TryGetWithSource<T>(out T instance, out SingletonSource source)
        {
            var type = typeof(T);

            if (_entries.TryGetValue(type, out var entry))
            {
                instance = (T)entry.Instance;
                source = entry.Source;
                return true;
            }

            instance = default;
            source = default;
            return false;
        }

        /// <summary>
        /// 모든 등록 클리어 (테스트/리셋용).
        /// </summary>
        public static void Clear()
        {
            _entries.Clear();
        }
    }
}
