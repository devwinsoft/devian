// SSOT: skills/devian-unity/20-domain-common-system/14-base-application/SKILL.md

#nullable enable

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Bootstrap 프리팹용 추상 베이스.
    /// 개발자는 모듈 밖(asmdef)에서 BaseApplication 파생 클래스를 선언하고,
    /// 그 컴포넌트를 Bootstrap prefab에 붙여서 사용한다.
    ///
    /// Bootstrap/BootProc/씬 구성은 개발자가 처리한다.
    /// 프레임워크는 Bootstrap prefab을 자동 instantiate하거나 BootProc를 자동 호출하지 않는다.
    /// BootProc 실행 시점은 SceneTransManager가 아니라 씬/앱 쪽에서 명시적으로 관리한다.
    /// </summary>
    public abstract class BaseApplication : MonoBehaviour
    {
        private static BaseApplication _instance;
        private static bool _booted;
        private static bool _loaded;
        private bool _isForeground = true;

        [SerializeField] private VersionNumber _appVersion;

        /// <summary>
        /// Inspector에 설정된 앱 버전.
        /// </summary>
        public VersionNumber AppVersion => _appVersion;

        /// <summary>
        /// Unity Application이 종료(Quit) 중인지 여부.
        /// Application.quitting 이벤트로 설정되며, 인스턴스 없이도 동작한다.
        /// Singleton 등 외부 시스템이 앱 종료 상태를 판단할 때 사용한다.
        /// </summary>
        public static bool IsApplicationQuitting { get; private set; }

        /// <summary>
        /// 에디터 종료/플레이 종료/씬 종료 정리 단계 여부.
        /// 정리 경로에서 싱글톤/매니저 접근을 스킵하는 데 사용한다.
        /// </summary>
        public static bool IsShuttingDown { get; private set; }

        /// <summary>
        /// Unity Awake. Validates/bootstrap-specific wiring for components already attached to Bootstrap.
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null) _instance = this;
            _isForeground = Application.isFocused;
            ensureRequiredComponents();
        }

        /// <summary>
        /// Validates bootstrap requirements.
        /// CompoSingleton은 런타임 AddComponent로 생성하지 말고 prefab/scene에 미리 부착해야 한다.
        /// </summary>
        protected virtual void ensureRequiredComponents()
        {
        }

        /// <summary>
        /// Bootstrap 인스턴스 참조.
        /// </summary>
        public static BaseApplication Instance => _instance;

        /// <summary>
        /// 개발자가 구현할 부트 프로세스.
        /// </summary>
        protected abstract Task onBootAsync();

        /// <summary>
        /// BootProc를 실행한다. 1회만 실행된다.
        /// 실패해도 재시도하지 않으며, 예외는 상위로 전파된다.
        /// </summary>
        public async Task BootProc()
        {
            if (_booted)
                return;
            _booted = true;

            await onBootAsync();
        }

        /// <summary>
        /// 앱 리소스 로딩을 실행한다. 1회만 실행된다.
        /// 실패해도 재시도하지 않으며, 예외는 상위로 전파된다.
        /// </summary>
        public async Task LoadAsync(SystemLanguage language, Action<float>? onProgress = null)
        {
            if (_loaded)
            {
                onProgress?.Invoke(1f);
                return;
            }
            _loaded = true;

            await onLoadAsync(language, onProgress);
            await onLoadCompletedAsync();
        }

        /// <summary>
        /// 앱 리소스 로딩 로직. 파생 클래스가 override하여 번들 다운로드, 테이블 로드 등을 구현한다.
        /// </summary>
        protected virtual Task onLoadAsync(SystemLanguage language, Action<float>? onProgress = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 리소스 로딩 완료 후 후처리. 파생 클래스가 override하여 매니저 초기화 등을 구현한다.
        /// </summary>
        protected virtual Task onLoadCompletedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 앱이 foreground로 진입할 때 호출되는 semantic hook.
        /// Unity raw callback을 override하지 말고 이 메서드를 사용한다.
        /// </summary>
        protected virtual void OnEnterForeground()
        {
        }

        /// <summary>
        /// 앱이 background로 전환될 때 호출되는 semantic hook.
        /// Unity raw callback을 override하지 말고 이 메서드를 사용한다.
        /// </summary>
        protected virtual void OnEnterBackground()
        {
        }

        protected void OnApplicationPause(bool paused)
        {
            updateForegroundState(!paused);
        }

        protected void OnApplicationFocus(bool focused)
        {
            updateForegroundState(focused);
        }

        /// <summary>
        /// 도메인 리로드 시 상태 리셋.
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            IsShuttingDown = true;
        }

        protected virtual void OnDestroy()
        {
            IsShuttingDown = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _booted = false;
            _loaded = false;
            IsApplicationQuitting = false;
            IsShuttingDown = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SubscribeQuitting()
        {
            Application.quitting += onQuitting;
        }

        private static void onQuitting()
        {
            IsApplicationQuitting = true;
        }

        private void updateForegroundState(bool isForeground)
        {
            if (_isForeground == isForeground)
                return;

            _isForeground = isForeground;

            if (isForeground)
                OnEnterForeground();
            else
                OnEnterBackground();
        }
    }
}
