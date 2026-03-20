using UnityEngine;

namespace Devian
{
    public sealed class UITweenHandle
    {
        private UITweenRunner _runner;
        private Coroutine _coroutine;

        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsCanceled { get; private set; }

        internal static UITweenHandle CreateCanceled()
        {
            return new UITweenHandle
            {
                IsCanceled = true
            };
        }

        internal void Bind(UITweenRunner runner, Coroutine coroutine)
        {
            if (runner == null || coroutine == null)
            {
                IsCanceled = true;
                return;
            }

            if (IsCanceled)
            {
                runner.StopManaged(coroutine);
                return;
            }

            _runner = runner;
            _coroutine = coroutine;
            IsRunning = true;
        }

        public void Cancel()
        {
            if (IsCompleted || IsCanceled)
            {
                return;
            }

            IsCanceled = true;
            IsRunning = false;

            var runner = _runner;
            var coroutine = _coroutine;

            _runner = null;
            _coroutine = null;

            if (runner != null && coroutine != null)
            {
                runner.StopManaged(coroutine);
            }
        }

        internal void Complete()
        {
            if (IsCanceled || IsCompleted)
            {
                return;
            }

            IsRunning = false;
            IsCompleted = true;
            _runner = null;
            _coroutine = null;
        }
    }
}
