using UnityEngine;
using UnityEngine.InputSystem;

namespace Devian
{
    /// <summary>
    /// InputManager의 프로젝트 단위 설정을 담는 ScriptableObject.
    /// InputManager는 Resources.Load로 고정 경로에서 로드한다.
    /// </summary>
    [CreateAssetMenu(fileName = "InputSettings", menuName = "Devian/MobilePackage/Input Settings")]
    public class InputSettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/InputSettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/InputSettings.asset";

        [Header("InputActionAsset")]
        [SerializeField] private InputActionAsset _asset;

        [Header("Action Map Names")]
        [SerializeField] private string _gameplayMapName = "Player";
        [SerializeField] private string _uiMapName = "UI";

        [Header("Move / Look Action Keys (Map/Action)")]
        [SerializeField] private string _moveKey = "Player/Move";
        [SerializeField] private string _lookKey = "Player/Look";

        [Header("Button Action Keys (Map/Action)")]
        [SerializeField] private string[] _expectedButtonKeys;

        public InputActionAsset Asset => _asset;
        public string GameplayMapName => _gameplayMapName;
        public string UIMapName => _uiMapName;
        public string MoveKey => _moveKey;
        public string LookKey => _lookKey;
        public string[] ExpectedButtonKeys => _expectedButtonKeys;
    }
}
