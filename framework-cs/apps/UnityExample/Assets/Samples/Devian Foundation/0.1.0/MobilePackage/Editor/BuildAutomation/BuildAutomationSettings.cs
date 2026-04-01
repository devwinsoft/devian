using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 모바일 빌드 자동화 파이프라인의 공통 설정.
    /// ScriptableObject로 프로젝트별 설정을 영속 저장한다.
    /// Assets > Create > Devian > Build Automation Settings 로 생성.
    /// CustomEditor(BuildAutomationSettingsEditor)가 Inspector를 그리므로
    /// [Header]는 사용하지 않는다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildAutomationSettings",
        menuName = "Devian/Build Automation Settings")]
    public class BuildAutomationSettings : ScriptableObject
    {
        // ── General ──
        [Tooltip("빌드 산출물 루트 경로 (프로젝트 루트 기준 상대경로)")]
        public string buildOutputDir = "Builds";

        // ── Android ──
        [Tooltip("ARM64 외 ARMv7 추가 포함 여부")]
        public bool includeARMv7 = false;

        [Tooltip("Android keystore 경로 (비워두면 debug keystore 사용)")]
        public string keystorePath = "";

        [Tooltip("Firebase Android App ID (예: 1:123456789:android:abc123)")]
        public string firebaseAndroidAppId = "";

        // ── iOS ──
        [Tooltip("Firebase iOS App ID (예: 1:123456789:ios:abc123)")]
        public string firebaseIOSAppId = "";

        // ── Release ──
        [Tooltip("릴리즈 데이터 저장소 루트 경로 (절대경로 또는 프로젝트 루트 기준 상대경로). 비워두면 프로젝트 루트 사용")]
        public string releaseRepoRoot = "";

        [Tooltip("AOS 버전 JSON 경로 (releaseRepoRoot 기준 상대경로)")]
        public string versionJsonPathAOS = "release/version_aos.json";

        [Tooltip("iOS 버전 JSON 경로 (releaseRepoRoot 기준 상대경로)")]
        public string versionJsonPathIOS = "release/version_ios.json";

        // ── CLI Paths ──
        [Tooltip("Firebase CLI 경로 (비워두면 자동 탐색). 터미널에서 'which firebase'로 확인 가능")]
        public string firebaseCLIPath = "";

        // ── Addressables ──
        [Tooltip("번들 빌드에서 제외할 Addressable Group 이름 목록")]
        public List<string> excludedAddressableGroups = new List<string>();

        // ── Pipeline Options ──
        [Tooltip("Development Build (디버그 모드). 프로파일러 연결, 스크립트 디버깅 허용")]
        public bool developmentBuild = false;
    }
}
