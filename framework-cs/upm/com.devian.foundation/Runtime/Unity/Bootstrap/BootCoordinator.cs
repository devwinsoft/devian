// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#nullable enable

using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 부팅 오케스트레이터.
    /// Bootstrap Root(DDOL)에 붙어서 1회 실행되며, 부팅 완료를 IsBooted로 신호한다.
    /// </summary>
    public sealed class BootCoordinator : MonoBehaviour
    {
        [SerializeField] private bool _autoStart = true;

        public bool IsBooted { get; private set; }
        public Exception? BootError { get; private set; }

        public event Action? Booted;

        private bool _isBooting;

        private void Awake()
        {
            // 중복 부팅 방지
            if (IsBooted || _isBooting)
                return;

            if (_autoStart)
                StartBoot();
        }

        public void StartBoot()
        {
            if (IsBooted || _isBooting)
                return;

            _isBooting = true;
            StartCoroutine(_BootRoutine());
        }

        public IEnumerator WaitUntilBooted()
        {
            while (!IsBooted && BootError == null)
                yield return null;

            if (BootError != null)
                throw BootError;
        }

        private IEnumerator _BootRoutine()
        {
            // 같은 루트(자식 포함)에 붙어있는 부팅 스텝들을 Order 기준으로 실행
            var steps = GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IDevianBootStep>()
                .OrderBy(s => s.Order)
                .ToArray();

            foreach (var step in steps)
            {
                IEnumerator? routine = null;
                try
                {
                    routine = step.Boot();
                }
                catch (Exception ex)
                {
                    BootError = ex;
                    Debug.LogException(ex);
                    yield break;
                }

                if (routine != null)
                {
                    while (true)
                    {
                        bool moveNext;
                        try
                        {
                            moveNext = routine.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            BootError = ex;
                            Debug.LogException(ex);
                            yield break;
                        }

                        if (!moveNext)
                            break;

                        yield return routine.Current;
                    }
                }
            }

            IsBooted = true;
            _isBooting = false;
            Booted?.Invoke();
        }
    }
}
