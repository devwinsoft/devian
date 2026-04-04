using System;
using System.Security.Cryptography;
using System.Text;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;

namespace Devian
{
    /// <summary>
    /// .dvn 파일 인코딩/디코딩 파이프라인.
    /// v2: HMAC-SHA256 무결성 검증 포함.
    /// SSOT: skills/devian-unity/50-mobile-system/25-recovery-system/03-ssot/SKILL.md
    /// </summary>
    internal static class RecoveryCodec
    {
        private const int DVN_VERSION_V1 = 0x01;
        private const int DVN_VERSION_V2 = 0x02;
        private const string HMAC_SEPARATOR = "\n";
        private const int HMAC_HEX_LENGTH = 64; // SHA-256 → 32 bytes → 64 hex chars

        // APP_SECRET: C#/TS 양쪽 동일 값. §E HMAC Integrity 참조.
        private const string APP_SECRET = "dvn-k8x2pQ7mW4rJ9sN1vF6bY3hT0cL5";

        /// <summary>
        /// 평문 JSON → .dvn 파일 내용 (v2: version header + obfuscated signed payload).
        /// json 내부 account.socialUserId를 추출하여 HMAC 키에 사용한다.
        /// </summary>
        internal static string Encode(string json)
        {
            var socialUserId = ExtractSocialUserId(json);
            var hmacHex = ComputeHmac(json, socialUserId);
            var signedPayload = json + HMAC_SEPARATOR + hmacHex;
            var obfuscated = ComplexUtil.Encrypt_Base64(signedPayload);
            return (char)DVN_VERSION_V2 + obfuscated;
        }

        /// <summary>
        /// .dvn 파일 내용 → 평문 JSON.
        /// v2: HMAC 무결성 검증 포함.
        /// v1: 하위호환 (HMAC 없이 디코딩).
        /// 실패 시 GameResult 에러 반환.
        /// </summary>
        internal static GameResult<string> Decode(string dvnContent)
        {
            if (string.IsNullOrEmpty(dvnContent) || dvnContent.Length < 2)
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_DECODE_FAILED,
                    "Invalid .dvn file: too short");
            }

            int version = dvnContent[0];

            switch (version)
            {
                case DVN_VERSION_V1:
                    return DecodeV1(dvnContent);
                case DVN_VERSION_V2:
                    return DecodeV2(dvnContent);
                default:
                    return GameResult<string>.Failure(
                        GAME_ERROR_TYPE.RECOVERY_UNSUPPORTED_VERSION,
                        $"Unsupported .dvn version: {version}");
            }
        }

        // ──────────────────────────────────────
        // v1 하위호환: HMAC 없이 디코딩
        // ──────────────────────────────────────
        private static GameResult<string> DecodeV1(string dvnContent)
        {
            try
            {
                var payload = dvnContent.Substring(1);
                var json = ComplexUtil.Decrypt_Base64(payload);
                return GameResult<string>.Success(json);
            }
            catch (Exception ex)
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_DECODE_FAILED,
                    $"DVN v1 decode failed: {ex.Message}");
            }
        }

        // ──────────────────────────────────────
        // v2: HMAC 검증 포함
        // ──────────────────────────────────────
        private static GameResult<string> DecodeV2(string dvnContent)
        {
            string signedPayload;
            try
            {
                var obfuscated = dvnContent.Substring(1);
                signedPayload = ComplexUtil.Decrypt_Base64(obfuscated);
            }
            catch (Exception ex)
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_DECODE_FAILED,
                    $"DVN v2 decode failed: {ex.Message}");
            }

            // signedPayload = json + "\n" + hmac_hex_64
            // 분리: 마지막 65번째 문자가 "\n", 이후 64자가 hmac
            if (signedPayload.Length < HMAC_HEX_LENGTH + HMAC_SEPARATOR.Length + 1)
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_DECODE_FAILED,
                    "DVN v2 signed payload too short");
            }

            var separatorIndex = signedPayload.Length - HMAC_HEX_LENGTH - HMAC_SEPARATOR.Length;
            var separator = signedPayload.Substring(separatorIndex, HMAC_SEPARATOR.Length);
            if (separator != HMAC_SEPARATOR)
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_HMAC_FAILED,
                    "DVN v2 HMAC separator not found");
            }

            var json = signedPayload.Substring(0, separatorIndex);
            var hmacHex = signedPayload.Substring(separatorIndex + HMAC_SEPARATOR.Length);

            // HMAC 검증
            var socialUserId = ExtractSocialUserId(json);
            var expectedHmac = ComputeHmac(json, socialUserId);
            if (!string.Equals(hmacHex, expectedHmac, StringComparison.Ordinal))
            {
                return GameResult<string>.Failure(
                    GAME_ERROR_TYPE.RECOVERY_HMAC_FAILED,
                    "DVN v2 HMAC verification failed");
            }

            return GameResult<string>.Success(json);
        }

        // ──────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────

        private static string ComputeHmac(string json, string socialUserId)
        {
            var keyString = APP_SECRET + (socialUserId ?? string.Empty);
            var keyBytes = Encoding.UTF8.GetBytes(keyString);
            var dataBytes = Encoding.UTF8.GetBytes(json);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static string ExtractSocialUserId(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                return obj["account"]?.Value<string>("socialUserId");
            }
            catch
            {
                return null;
            }
        }
    }
}
