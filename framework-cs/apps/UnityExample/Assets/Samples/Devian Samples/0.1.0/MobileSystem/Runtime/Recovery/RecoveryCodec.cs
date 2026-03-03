using System;
using Devian.Domain.Common;

namespace Devian
{
    /// <summary>
    /// .dvn 파일 인코딩/디코딩 파이프라인.
    /// ComplexUtil 난독화를 사용한다.
    /// </summary>
    internal static class RecoveryCodec
    {
        private const int DVN_VERSION = 0x01;

        /// <summary>
        /// 평문 JSON → .dvn 파일 내용 (version header + obfuscated payload)
        /// </summary>
        internal static string Encode(string json)
        {
            var obfuscated = ComplexUtil.Encrypt_Base64(json);
            return (char)DVN_VERSION + obfuscated;
        }

        /// <summary>
        /// .dvn 파일 내용 → 평문 JSON.
        /// 실패 시 CommonResult 에러 반환.
        /// </summary>
        internal static CommonResult<string> Decode(string dvnContent)
        {
            if (string.IsNullOrEmpty(dvnContent) || dvnContent.Length < 2)
            {
                return CommonResult<string>.Failure(
                    CommonErrorType.RECOVERY_DECODE_FAILED,
                    "Invalid .dvn file: too short");
            }

            int version = dvnContent[0];
            if (version != DVN_VERSION)
            {
                return CommonResult<string>.Failure(
                    CommonErrorType.RECOVERY_UNSUPPORTED_VERSION,
                    $"Unsupported .dvn version: {version}");
            }

            try
            {
                var payload = dvnContent.Substring(1);
                var json = ComplexUtil.Decrypt_Base64(payload);
                return CommonResult<string>.Success(json);
            }
            catch (Exception ex)
            {
                return CommonResult<string>.Failure(
                    CommonErrorType.RECOVERY_DECODE_FAILED,
                    $"DVN decode failed: {ex.Message}");
            }
        }
    }
}
