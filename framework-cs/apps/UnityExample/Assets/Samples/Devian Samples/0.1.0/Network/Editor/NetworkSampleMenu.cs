#nullable enable
using UnityEngine;
using UnityEditor;

namespace Devian
{
    /// <summary>
    /// Editor menu for Network sample.
    /// GameNetManager handles Stub/Proxy internally - extend via partial class.
    /// </summary>
    public static class NetworkSampleMenu
    {
        private const string CreateMenuPath = "Devian/Samples/Network/Create GameNetManager";
        private const string HelpMenuPath = "Devian/Samples/Network/How to Use";

        [MenuItem(CreateMenuPath)]
        public static void CreateGameNetManager()
        {
            var go = new GameObject("GameNetManager");
            go.AddComponent<GameNetManager>();
            Selection.activeGameObject = go;
            Debug.Log("[NetworkSampleMenu] Created GameNetManager. Use Connect(url) to connect.");
        }

        [MenuItem(HelpMenuPath)]
        public static void ShowUsageInfo()
        {
            Debug.Log(@"[NetworkSampleMenu] GameNetManager Usage:

1. Add GameNetManager component to a GameObject (or use menu)
2. Call Connect(url) to establish connection
3. Use Proxy to send messages
4. Extend via partial class for custom handling

Basic Usage:
  var manager = GetComponent<GameNetManager>();
  manager.Connect(""ws://localhost:8080"");
  manager.Proxy.SendPing(new C2Game.Ping { Timestamp = Time.time });

Extending (partial class):
  // In a separate file: Game2CStub.Partial.cs
  public partial class Game2CStub
  {
      partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message)
      {
          // Handle Pong message
      }
  }
");
        }
    }
}
