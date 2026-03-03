using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Devian
{
    /// <summary>
    /// Save data의 Export(전송)와 Import(복원)를 오케스트레이션한다.
    /// SaveDataManager의 public API만 사용한다.
    /// </summary>
    public sealed class RecoveryManager : CompoSingleton<RecoveryManager>
    {
        /// <summary>
        /// 현재 save data를 .dvn 파일로 Export하고 OS 공유 시트를 연다.
        /// </summary>
        public async Task<CommonResult<bool>> ExportDvnAsync(CancellationToken ct)
        {
            try
            {
#if UNITY_EDITOR
                Debug.Log("[RecoveryManager] ExportDvnAsync: Editor에서는 .dvn 파일을 생성하지 않습니다.");
                await Task.CompletedTask;
                return CommonResult<bool>.Success(true);
#else
                var prepareResult = await PrepareDvnFileAsync(ct);
                if (prepareResult.IsFailure)
                {
                    return CommonResult<bool>.Failure(prepareResult.Error!);
                }

                var filePath = prepareResult.Value;
                var subject = Application.productName + " Save Data Recovery";
                ShareFile(filePath, subject);
                Debug.Log("[RecoveryManager] ExportDvnAsync: ShareFile called");

                return CommonResult<bool>.Success(true);
#endif
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[RecoveryManager] ExportDvnAsync: cancelled");
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    "Export cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecoveryManager] ExportDvnAsync: failed — {ex}");
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    $"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 save data를 .dvn 파일로 Export하고 이메일 앱을 연다 (수신자 프리셋).
        /// </summary>
        public async Task<CommonResult<bool>> ExportDvnViaEmailAsync(string recipient, CancellationToken ct)
        {
            try
            {
#if UNITY_EDITOR
                Debug.Log($"[RecoveryManager] ExportDvnViaEmailAsync: Editor에서는 .dvn 파일을 생성하지 않습니다. recipient={recipient}");
                await Task.CompletedTask;
                return CommonResult<bool>.Success(true);
#else
                var prepareResult = await PrepareDvnFileAsync(ct);
                if (prepareResult.IsFailure)
                {
                    return CommonResult<bool>.Failure(prepareResult.Error!);
                }

                var filePath = prepareResult.Value;
                var subject = Application.productName + " Save Data Recovery";
                SendEmail(filePath, recipient, subject);
                Debug.Log($"[RecoveryManager] ExportDvnViaEmailAsync: SendEmail called → {recipient}");

                return CommonResult<bool>.Success(true);
#endif
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[RecoveryManager] ExportDvnViaEmailAsync: cancelled");
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    "Export cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecoveryManager] ExportDvnViaEmailAsync: failed — {ex}");
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    $"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// .dvn 파일을 디코딩하여 save data를 복원한다.
        /// </summary>
        public async Task<CommonResult<bool>> ImportDvnAsync(string filePath, CancellationToken ct)
        {
            try
            {
                // 1. .dvn 파일 읽기
                if (!File.Exists(filePath))
                {
                    return CommonResult<bool>.Failure(
                        CommonErrorType.RECOVERY_DECODE_FAILED,
                        $"File not found: {filePath}");
                }

                var content = await Task.Run(() => File.ReadAllText(filePath), ct);
                ct.ThrowIfCancellationRequested();

                // 2. RecoveryCodec.Decode → 평문 JSON
                var decodeResult = RecoveryCodec.Decode(content);
                if (decodeResult.IsFailure)
                {
                    return CommonResult<bool>.Failure(decodeResult.Error!);
                }

                var json = decodeResult.Value;

                // 3. JSON 기본 검증 (파싱 가능 여부)
                try
                {
                    JObject.Parse(json);
                }
                catch (Exception ex)
                {
                    return CommonResult<bool>.Failure(
                        CommonErrorType.RECOVERY_DECODE_FAILED,
                        $"Invalid JSON: {ex.Message}");
                }

                // 4. SaveDataManager로 복원
                var restoreResult = await SaveDataManager.Instance
                    .RestoreFromPlainJsonAsync(json, true, ct);

                if (restoreResult.IsFailure)
                {
                    return CommonResult<bool>.Failure(restoreResult.Error!);
                }

                // 5. 임시 .dvn 파일 삭제
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // 삭제 실패는 무시
                }

                return CommonResult<bool>.Success(true);
            }
            catch (OperationCanceledException)
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    "Import cancelled");
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    $"Import failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 네이티브 레이어에서 UnitySendMessage로 호출됨.
        /// </summary>
        public void OnRecoveryFileReceived(string filePath)
        {
            _ = ImportDvnAsync(filePath, CancellationToken.None);
        }

        // ──────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────

        /// <summary>
        /// ToJson → Encode → 임시 파일 저장. 성공 시 filePath 반환.
        /// ExportDvnAsync / ExportDvnViaEmailAsync 공통.
        /// </summary>
        private async Task<CommonResult<string>> PrepareDvnFileAsync(CancellationToken ct)
        {
            Debug.Log("[RecoveryManager] PrepareDvnFileAsync: start");

            // 1. 평문 JSON 획득
            var json = SaveDataManager.Instance.ToJson();
            ct.ThrowIfCancellationRequested();
            Debug.Log($"[RecoveryManager] PrepareDvnFileAsync: ToJson OK ({json.Length} chars)");

            // 2. RecoveryCodec.Encode → .dvn string
            var dvnContent = RecoveryCodec.Encode(json);
            Debug.Log($"[RecoveryManager] PrepareDvnFileAsync: Encode OK ({dvnContent.Length} chars)");

            // 3. 임시 파일 저장
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"recovery_{timestamp}.dvn";
            var filePath = Path.Combine(Application.temporaryCachePath, fileName);
            await Task.Run(() => File.WriteAllText(filePath, dvnContent), ct);
            Debug.Log($"[RecoveryManager] PrepareDvnFileAsync: File saved → {filePath}");

            return CommonResult<string>.Success(filePath);
        }

        private static void ShareFile(string filePath, string subject)
        {
#if UNITY_IOS && !UNITY_EDITOR
            DevianShare_ShareFile(filePath, subject);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var cls = new AndroidJavaClass("com.devian.share.DevianShare");
            cls.CallStatic("shareFile", activity, filePath, subject);
#else
            Debug.Log($"[RecoveryManager] ShareFile not supported on this platform. path={filePath}");
#endif
        }

        private static void SendEmail(string filePath, string recipient, string subject)
        {
#if UNITY_IOS && !UNITY_EDITOR
            DevianShare_SendEmail(filePath, recipient, subject);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var cls = new AndroidJavaClass("com.devian.share.DevianShare");
            cls.CallStatic("sendEmail", activity, filePath, recipient, subject);
#else
            Debug.Log($"[RecoveryManager] SendEmail not supported on this platform. recipient={recipient}, path={filePath}");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DevianShare_ShareFile(string filePath, string subject);

        [DllImport("__Internal")]
        private static extern void DevianShare_SendEmail(string filePath, string recipient, string subject);
#endif
    }
}
