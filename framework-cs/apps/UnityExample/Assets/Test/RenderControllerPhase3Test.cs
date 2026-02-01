using System;
using System.Collections;
using UnityEngine;

namespace Devian.Test
{
    /// <summary>
    /// RenderController Phase 1~3 회귀 테스트.
    /// Boot/Addressables에 의존하지 않고 씬 로컬 참조만 사용한다.
    /// </summary>
    public sealed class RenderControllerPhase3Test : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField] private RenderController _controller;

        [Header("Effect Assets (Scene Local References)")]
        [Tooltip("Initial default effect (priority 0, NoOp recommended)")]
        [SerializeField] private RenderEffectAsset _default1;

        [Tooltip("Replacement default effect for _SetDefault test")]
        [SerializeField] private RenderEffectAsset _default2;

        [Tooltip("Low priority effect (e.g. priority=1)")]
        [SerializeField] private RenderEffectAsset _effectA;

        [Tooltip("High priority effect (e.g. priority=10)")]
        [SerializeField] private RenderEffectAsset _effectB;

        [Tooltip("Same priority as B (priority=10) for tie-breaker test")]
        [SerializeField] private RenderEffectAsset _effectC;

        [Header("Test Settings")]
        [SerializeField] private float _stepDelay = 0.1f;

        private bool _testFailed = false;
        private string _failureMessage = null;

        private void Start()
        {
            StartCoroutine(RunAllTests());
        }

        private IEnumerator RunAllTests()
        {
            Debug.Log("[RenderControllerPhase3Test] Starting tests...");

            // Wait one frame for Awake to complete
            yield return null;

            // T1: Default immediate apply
            yield return Test_T1_DefaultImmediateApply();
            if (_testFailed) yield break;

            // T4: Child driver option (just verify no error if searchDriverInChildren is used)
            yield return Test_T4_ChildDriverOption();
            if (_testFailed) yield break;

            // T2: Priority + tie-breaker (latest wins)
            yield return Test_T2_PriorityAndTieBreaker();
            if (_testFailed) yield break;

            // T3: _SetDefault runtime replacement
            yield return Test_T3_SetDefaultRuntimeReplacement();
            if (_testFailed) yield break;

            Debug.Log("[RenderControllerPhase3Test] ===== ALL TESTS PASSED =====");
        }

        private void Fail(string message)
        {
            _testFailed = true;
            _failureMessage = message;
            Debug.LogError($"[RenderControllerPhase3Test] TEST FAILED: {message}");
            throw new Exception(message);
        }

        /// <summary>
        /// T1: Play 시작 직후 default가 즉시 적용되어야 함
        /// </summary>
        private IEnumerator Test_T1_DefaultImmediateApply()
        {
            Debug.Log("[T1] Testing default immediate apply...");

            int handle = _controller._GetCurrentAppliedHandle();
            string effectName = _controller._GetCurrentAppliedEffectName();

            // handle 0 = default
            if (handle != 0)
            {
                Fail($"[T1] Expected handle=0 (default), got handle={handle}");
                yield break;
            }

            if (!effectName.StartsWith("default"))
            {
                Fail($"[T1] Expected effect name to start with 'default', got '{effectName}'");
                yield break;
            }

            Debug.Log($"[T1] PASS - Default applied: handle={handle}, name={effectName}");
            yield return new WaitForSeconds(_stepDelay);
        }

        /// <summary>
        /// T4: Child driver option 확인 (에러 로그 없이 동작해야 함)
        /// </summary>
        private IEnumerator Test_T4_ChildDriverOption()
        {
            Debug.Log("[T4] Verifying child driver option...");

            // 현재 컨트롤러가 정상 동작하면 driver가 성공적으로 resolve됨
            // searchDriverInChildren=true 상태에서 driver가 child에 있어도 동작해야 함
            int handle = _controller._GetCurrentAppliedHandle();

            if (handle == -1)
            {
                Fail("[T4] Driver not found or invalid. Check searchDriverInChildren option.");
                yield break;
            }

            Debug.Log("[T4] PASS - Driver resolved successfully");
            yield return new WaitForSeconds(_stepDelay);
        }

        /// <summary>
        /// T2: priority 규칙 + 동점 시 최신 승리
        /// </summary>
        private IEnumerator Test_T2_PriorityAndTieBreaker()
        {
            Debug.Log("[T2] Testing priority and tie-breaker rules...");

            // Step 1: Add A (priority=1) -> A 적용
            int handleA = _controller._AddEffect(_effectA);
            if (handleA == 0)
            {
                Fail("[T2] Failed to add effect A");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            if (!AssertCurrentEffect(handleA, _effectA.name, "After adding A"))
                yield break;

            // Step 2: Add B (priority=10) -> B 적용 (priority 높음)
            int handleB = _controller._AddEffect(_effectB);
            if (handleB == 0)
            {
                Fail("[T2] Failed to add effect B");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            if (!AssertCurrentEffect(handleB, _effectB.name, "After adding B"))
                yield break;

            // Step 3: Add C (priority=10, 동점) -> C 적용 (최신이므로)
            int handleC = _controller._AddEffect(_effectC);
            if (handleC == 0)
            {
                Fail("[T2] Failed to add effect C");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            if (!AssertCurrentEffect(handleC, _effectC.name, "After adding C (tie-breaker: latest wins)"))
                yield break;

            // Step 4: Remove C -> B 적용
            bool removedC = _controller._RemoveEffect(handleC);
            if (!removedC)
            {
                Fail("[T2] Failed to remove effect C");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            if (!AssertCurrentEffect(handleB, _effectB.name, "After removing C"))
                yield break;

            // Step 5: Remove B -> A 적용
            bool removedB = _controller._RemoveEffect(handleB);
            if (!removedB)
            {
                Fail("[T2] Failed to remove effect B");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            if (!AssertCurrentEffect(handleA, _effectA.name, "After removing B"))
                yield break;

            // Step 6: Remove A -> default 적용
            bool removedA = _controller._RemoveEffect(handleA);
            if (!removedA)
            {
                Fail("[T2] Failed to remove effect A");
                yield break;
            }

            yield return new WaitForSeconds(_stepDelay);

            int finalHandle = _controller._GetCurrentAppliedHandle();
            if (finalHandle != 0)
            {
                Fail($"[T2] After removing all effects, expected default (handle=0), got handle={finalHandle}");
                yield break;
            }

            Debug.Log("[T2] PASS - Priority and tie-breaker rules work correctly");
        }

        /// <summary>
        /// T3: _SetDefault() 런타임 교체
        /// </summary>
        private IEnumerator Test_T3_SetDefaultRuntimeReplacement()
        {
            Debug.Log("[T3] Testing _SetDefault runtime replacement...");

            // 효과 스택이 비어있는 상태 확인
            int currentHandle = _controller._GetCurrentAppliedHandle();
            if (currentHandle != 0)
            {
                Fail($"[T3] Expected default (handle=0) before SetDefault test, got handle={currentHandle}");
                yield break;
            }

            // _SetDefault(default2) 호출
            _controller._SetDefault(_default2);

            yield return new WaitForSeconds(_stepDelay);

            // 새 default가 즉시 적용됐는지 확인
            int newHandle = _controller._GetCurrentAppliedHandle();
            string effectName = _controller._GetCurrentAppliedEffectName();

            if (newHandle != 0)
            {
                Fail($"[T3] Expected default (handle=0) after SetDefault, got handle={newHandle}");
                yield break;
            }

            if (!effectName.Contains(_default2.name))
            {
                Fail($"[T3] Expected effect name to contain '{_default2.name}', got '{effectName}'");
                yield break;
            }

            Debug.Log($"[T3] PASS - _SetDefault applied immediately: {effectName}");

            // 원래 default로 복원 (다음 테스트를 위해)
            _controller._SetDefault(_default1);

            yield return new WaitForSeconds(_stepDelay);
        }

        private bool AssertCurrentEffect(int expectedHandle, string expectedAssetName, string context)
        {
            int actualHandle = _controller._GetCurrentAppliedHandle();
            string actualName = _controller._GetCurrentAppliedEffectName();

            if (actualHandle != expectedHandle)
            {
                Fail($"[T2] {context}: Expected handle={expectedHandle}, got handle={actualHandle}");
                return false;
            }

            if (!actualName.Contains(expectedAssetName))
            {
                Fail($"[T2] {context}: Expected name to contain '{expectedAssetName}', got '{actualName}'");
                return false;
            }

            Debug.Log($"[T2] {context}: handle={actualHandle}, name={actualName} - OK");
            return true;
        }
    }
}
