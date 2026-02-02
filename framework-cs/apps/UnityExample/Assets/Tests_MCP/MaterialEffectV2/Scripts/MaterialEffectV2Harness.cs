using System.Collections;
using UnityEngine;

namespace Devian.TestsMcp
{
    public sealed class MaterialEffectV2Harness : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MaterialEffectController _controller;

        [Header("Effects (MaterialSetMaterialEffectAsset)")]
        [SerializeField] private MaterialEffectAsset _effectA; // red
        [SerializeField] private MaterialEffectAsset _effectB; // green

        [Header("Timing")]
        [SerializeField] private float _stepSeconds = 1.0f;

        private int _hA;
        private int _hB;

        private IEnumerator Start()
        {
            if (_controller == null)
            {
                Debug.LogError("[MaterialEffectV2Harness] Controller is null.");
                yield break;
            }

            // Step0: default baseline
            Debug.Log($"[MaterialEffectV2Harness] Step0 default handle={_controller._GetCurrentAppliedHandle()}");
            yield return new WaitForSeconds(_stepSeconds);

            // Step1: apply A
            if (_effectA != null)
            {
                _hA = _controller._AddEffect(_effectA);
                Debug.Log($"[MaterialEffectV2Harness] Step1 add A handle={_hA}, current={_controller._GetCurrentAppliedHandle()}");
            }
            else Debug.LogWarning("[MaterialEffectV2Harness] effectA null");
            yield return new WaitForSeconds(_stepSeconds);

            // Step2: apply B (priority should be higher on asset or manually set; if same priority, later wins)
            if (_effectB != null)
            {
                _hB = _controller._AddEffect(_effectB);
                Debug.Log($"[MaterialEffectV2Harness] Step2 add B handle={_hB}, current={_controller._GetCurrentAppliedHandle()}");
            }
            else Debug.LogWarning("[MaterialEffectV2Harness] effectB null");
            yield return new WaitForSeconds(_stepSeconds);

            // Step3: remove B -> A
            if (_hB != 0)
            {
                _controller._RemoveEffect(_hB);
                Debug.Log($"[MaterialEffectV2Harness] Step3 remove B, current={_controller._GetCurrentAppliedHandle()}");
            }
            yield return new WaitForSeconds(_stepSeconds);

            // Step4: remove A -> default
            if (_hA != 0)
            {
                _controller._RemoveEffect(_hA);
                Debug.Log($"[MaterialEffectV2Harness] Step4 remove A, current={_controller._GetCurrentAppliedHandle()}");
            }
        }
    }
}
