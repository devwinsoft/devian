#nullable enable
using System;
using UnityEngine;
using Devian.Network.Sample;

namespace Devian.Sample
{
    /// <summary>
    /// Smoke test that verifies sample protocol types are accessible.
    /// This script demonstrates how to reference generated protocol code.
    /// 
    /// The sample protocol (Devian.Network.Sample) is located in:
    /// Runtime/Generated.Sample/Devian.Network.Sample/
    /// </summary>
    public class SampleProtocolSmokeTest : MonoBehaviour
    {
        [ContextMenu("Run Smoke Test")]
        public void RunSmokeTest()
        {
            Debug.Log("[SampleProtocolSmokeTest] Starting smoke test...");

            try
            {
                // 1. Create a Ping message
                var ping = new C2Sample.Ping
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = "Hello from sample!"
                };
                Debug.Log($"[SampleProtocolSmokeTest] Created Ping: timestamp={ping.Timestamp}, payload={ping.Payload}");

                // 2. Create an Echo message
                var echo = new C2Sample.Echo
                {
                    Message = "Test echo message"
                };
                Debug.Log($"[SampleProtocolSmokeTest] Created Echo: message={echo.Message}");

                // 3. Create a Pong message (server response)
                var pong = new Sample2C.Pong
                {
                    Timestamp = ping.Timestamp,
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                Debug.Log($"[SampleProtocolSmokeTest] Created Pong: timestamp={pong.Timestamp}, serverTime={pong.ServerTime}");

                // 4. Create an EchoReply message
                var echoReply = new Sample2C.EchoReply
                {
                    Message = echo.Message,
                    EchoedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                Debug.Log($"[SampleProtocolSmokeTest] Created EchoReply: message={echoReply.Message}, echoedAt={echoReply.EchoedAt}");

                Debug.Log("[SampleProtocolSmokeTest] ✓ Smoke test PASSED - all sample protocol types are accessible!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SampleProtocolSmokeTest] ✗ Smoke test FAILED: {ex}");
            }
        }

        private void Start()
        {
            // Auto-run smoke test on start (optional)
            RunSmokeTest();
        }
    }
}
