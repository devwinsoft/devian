using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Devian.Tools.Generators
{
    /// <summary>
    /// 빌드 후 검사: 하위 네임스페이스 및 Devarc 잔재 감지.
    /// 위반 발견 시 상세 정보 출력 후 예외 발생.
    /// </summary>
    public static class NamespaceGuard
    {
        private static readonly Regex SubNamespacePattern = new Regex(
            @"^\s*namespace\s+(Devian\.Common\.|Devian\.Ws\.)",
            RegexOptions.Multiline | RegexOptions.Compiled);
        
        private static readonly Regex DevarcPattern = new Regex(
            @"Devarc",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        /// <summary>
        /// 지정된 디렉토리의 모든 *.g.cs 파일을 검사.
        /// 위반 발견 시 예외 발생.
        /// </summary>
        public static void ValidateGeneratedFiles(string rootDir)
        {
            if (!Directory.Exists(rootDir))
            {
                Console.WriteLine(string.Format("[NamespaceGuard] Directory not found: {0}", rootDir));
                return;
            }
            
            var violations = new List<Violation>();
            var files = Directory.GetFiles(rootDir, "*.g.cs", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                ValidateFile(file, violations);
            }
            
            if (violations.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== NAMESPACE GUARD VIOLATIONS ===");
                Console.WriteLine();
                
                foreach (var v in violations)
                {
                    Console.WriteLine(string.Format("[{0}] {1}", v.Type, v.FilePath));
                    Console.WriteLine(string.Format("  Line {0}: {1}", v.LineNumber, v.LineContent.Trim()));
                    Console.WriteLine();
                }
                
                Console.WriteLine(string.Format("Total: {0} violation(s)", violations.Count));
                throw new InvalidOperationException(
                    string.Format("NamespaceGuard: {0} violation(s) found. See above for details.", violations.Count));
            }
            
            Console.WriteLine(string.Format("[NamespaceGuard] Validated {0} file(s), no violations.", files.Length));
        }
        
        private static void ValidateFile(string filePath, List<Violation> violations)
        {
            var lines = File.ReadAllLines(filePath);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNum = i + 1;
                
                // 하위 네임스페이스 검사
                if (SubNamespacePattern.IsMatch(line))
                {
                    violations.Add(new Violation
                    {
                        Type = "SUB_NAMESPACE",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LineContent = line
                    });
                }
                
                // Devarc 잔재 검사
                if (DevarcPattern.IsMatch(line))
                {
                    violations.Add(new Violation
                    {
                        Type = "DEVARC_LEGACY",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LineContent = line
                    });
                }
            }
        }
        
        private class Violation
        {
            public string Type { get; set; }
            public string FilePath { get; set; }
            public int LineNumber { get; set; }
            public string LineContent { get; set; }
        }
    }
}
