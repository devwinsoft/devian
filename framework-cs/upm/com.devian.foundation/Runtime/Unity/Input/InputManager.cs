using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Devian
{
    /// <summary>
    /// InputActionAsset 기반 입력 수집/정규화/발행 관리자.
    /// CompoSingleton — Bootstrap에 기본 포함. IInputManager는 OnEnable/OnDisable에서 별도 Register.
    /// Update에서 등록된 BaseInputController를 Priority 순으로 직접 호출한다.
    /// </summary>
    public sealed class InputManager : CompoSingleton<InputManager>, IInputManager
    {
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

        [Header("Options")]
        [SerializeField] private bool _outputEnabled = true;

        // Runtime
        private InputActionMap _gameplayMap;
        private InputActionMap _uiMap;
        private InputAction _moveAction;
        private InputAction _lookAction;

        private Dictionary<string, int> _buttonMap;
        private InputAction[] _buttonActions;
        private string[] _buttonKeys;

        private InputContext _context;

        // Controller registry
        private readonly List<BaseInputController> _controllers = new();
        private bool _controllersDirty;

        // ---- IInputManager ----

        public InputActionAsset Asset => _asset;
        public InputContext Context => _context;
        public System.Collections.Generic.IReadOnlyList<string> ButtonKeys => _buttonKeys ?? System.Array.Empty<string>();

        public int GetButtonIndex(string key)
        {
            if (_buttonMap != null && _buttonMap.TryGetValue(key, out int index))
                return index;
            return -1;
        }

        public void SetContext(InputContext context)
        {
            _context = context;

            if (_gameplayMap != null && _uiMap != null)
            {
                if (context == InputContext.Game)
                {
                    _gameplayMap.Enable();
                    _uiMap.Disable();
                }
                else
                {
                    _gameplayMap.Disable();
                    _uiMap.Enable();
                }
            }
        }

        // ---- Controller Registry ----

        public void RegisterController(BaseInputController controller)
        {
            if (controller == null) return;
            if (_controllers.Contains(controller)) return;
            _controllers.Add(controller);
            _controllersDirty = true;
        }

        public void UnregisterController(BaseInputController controller)
        {
            if (controller == null) return;
            if (_controllers.Remove(controller))
                _controllersDirty = true;
        }

        // ---- Lifecycle ----

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            if (_asset == null)
            {
                Debug.LogError("[InputManager] InputActionAsset is not assigned.");
                return;
            }

            _gameplayMap = _asset.FindActionMap(_gameplayMapName);
            _uiMap = _asset.FindActionMap(_uiMapName);

            // Resolve move / look actions
            _moveAction = InputButtonMapBuilder.TryFindActionByKey(_asset, _moveKey);
            _lookAction = InputButtonMapBuilder.TryFindActionByKey(_asset, _lookKey);

            // Build button map
            _rebuildButtonMapInternal();

            // Enable initial context
            SetContext(_context);

            Singleton.Register<IInputManager>(this, SingletonSource.Compo, "InputManager.OnEnable");
        }

        private void OnDisable()
        {
            _gameplayMap?.Disable();
            _uiMap?.Disable();

            Singleton.Unregister<IInputManager>(this);
        }

        private void Update()
        {
            if (!_outputEnabled) return;

            // NOTE:
            // Action이 null이면 "초기화 안 됨"으로 간주하고 축 값을 Invalid(NaN)로 보낸다.
            // BaseInputController에서 NaN 축은 콜백을 절대 발생시키지 않는다.
            Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : new Vector2(float.NaN, float.NaN);
            Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : new Vector2(float.NaN, float.NaN);

            ulong bits = 0UL;
            if (_buttonActions != null)
            {
                for (int i = 0; i < _buttonActions.Length; i++)
                {
                    var action = _buttonActions[i];
                    if (action != null && action.IsPressed())
                    {
                        bits |= (1UL << i);
                    }
                }
            }

            var frame = new InputFrame(move, look, bits, _context, Time.time);

            // Sort controllers by Priority (descending) when dirty
            if (_controllersDirty)
            {
                _controllers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _controllersDirty = false;
            }

            // Dispatch to all registered controllers
            for (int i = 0; i < _controllers.Count; i++)
            {
                var c = _controllers[i];
                if (c == null) continue;
                c.__Consume(frame);
            }
        }

        // ---- Public (Editor-safe) ----

        /// <summary>
        /// _expectedButtonKeys 기준으로 내부 버튼 맵을 재빌드한다.
        /// move/look 액션도 재-resolve 한다.
        /// Edit 모드에서도 NRE 없이 안전하게 호출 가능.
        /// ActionMap enable/disable 등 플레이 모드 전용 동작은 건드리지 않는다.
        /// </summary>
        public void RebuildButtonMap()
        {
            if (_asset == null)
            {
                _buttonMap = new Dictionary<string, int>();
                _buttonActions = null;
                _buttonKeys = null;
                _moveAction = null;
                _lookAction = null;
                return;
            }

            // Re-resolve move / look actions
            _moveAction = InputButtonMapBuilder.TryFindActionByKey(_asset, _moveKey);
            _lookAction = InputButtonMapBuilder.TryFindActionByKey(_asset, _lookKey);

            // Rebuild button map
            _rebuildButtonMapInternal();
        }

        // ---- Private ----

        private void _rebuildButtonMapInternal()
        {
            if (_expectedButtonKeys == null || _expectedButtonKeys.Length == 0)
            {
                _buttonMap = new Dictionary<string, int>();
                _buttonActions = null;
                _buttonKeys = null;
                return;
            }

            _buttonMap = InputButtonMapBuilder.Build(_asset, _expectedButtonKeys);

            // Build ordered action array + key array matching indices
            int maxIndex = -1;
            foreach (var kvp in _buttonMap)
            {
                if (kvp.Value > maxIndex) maxIndex = kvp.Value;
            }

            if (maxIndex >= 0)
            {
                _buttonActions = new InputAction[maxIndex + 1];
                _buttonKeys = new string[maxIndex + 1];

                foreach (var kvp in _buttonMap)
                {
                    _buttonActions[kvp.Value] = InputButtonMapBuilder.TryFindActionByKey(_asset, kvp.Key);
                    _buttonKeys[kvp.Value] = kvp.Key;
                }
            }
            else
            {
                _buttonActions = null;
                _buttonKeys = null;
            }
        }
    }
}
