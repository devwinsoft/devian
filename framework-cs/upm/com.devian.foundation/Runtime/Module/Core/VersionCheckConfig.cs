// SSOT: skills/devian/10-module/20-core/15-version-check-config/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// 버전 JSON 파일의 공통 데이터 모델.
    /// Runtime(RemoteDataManager)과 Editor(BuildAutomationWindow) 양쪽에서 사용한다.
    /// </summary>
    [Serializable]
    public sealed class VersionCheckConfig
    {
        public string currentVersion = string.Empty;
        public string minVersion = string.Empty;
        public string update_url = string.Empty;
    }
}
