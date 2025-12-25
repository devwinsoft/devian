using System;
using System.Collections.Generic;

namespace Devian.Tools.Generators
{
    /// <summary>
    /// 도메인 → 네임스페이스 고정 매핑.
    /// 하위 네임스페이스는 절대 생성하지 않는다.
    /// </summary>
    public static class DomainNamespace
    {
        private static readonly Dictionary<string, string> DomainToNamespace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "common", "Devian.Common" },
            { "ws", "Devian.Ws" }
        };
        
        /// <summary>
        /// 도메인 이름으로 C# 네임스페이스를 반환한다.
        /// 등록되지 않은 도메인은 "Devian.{PascalCase(domain)}" 형태로 반환.
        /// </summary>
        public static string GetCSharpNamespace(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be null or empty", nameof(domain));
            
            if (DomainToNamespace.TryGetValue(domain, out var ns))
                return ns;
            
            // 등록되지 않은 도메인: Devian.{PascalCase}
            return "Devian." + TypeMapper.ToPascalCase(domain);
        }
        
        /// <summary>
        /// 네임스페이스가 하위 네임스페이스(점 2개 이상)인지 검사.
        /// 하위 네임스페이스면 예외 발생.
        /// </summary>
        public static void ValidateNoSubNamespace(string ns)
        {
            if (string.IsNullOrWhiteSpace(ns))
                throw new ArgumentException("Namespace cannot be null or empty", nameof(ns));
            
            // "Devian.Common"은 허용, "Devian.Common.Data"는 금지
            var dotCount = 0;
            foreach (var c in ns)
            {
                if (c == '.') dotCount++;
            }
            
            if (dotCount > 1)
            {
                throw new InvalidOperationException(
                    string.Format("Sub-namespace is forbidden: '{0}'. Only single-level domain namespaces are allowed (e.g., Devian.Common, Devian.Ws).", ns));
            }
        }
        
        /// <summary>
        /// "Devarc" 문자열이 포함되어 있는지 검사.
        /// 포함되어 있으면 예외 발생.
        /// </summary>
        public static void ValidateNoDevarc(string content, string filePath)
        {
            if (content.IndexOf("Devarc", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    string.Format("Legacy 'Devarc' branding found in '{0}'. Use 'Devian' instead.", filePath));
            }
        }
    }
}
