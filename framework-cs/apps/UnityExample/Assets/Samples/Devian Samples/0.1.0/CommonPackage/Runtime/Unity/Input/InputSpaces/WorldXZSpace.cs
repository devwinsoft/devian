using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Move(x, y) → World(x, 0, y). 탑다운 / 2D 레이아웃용.
    /// </summary>
    public class WorldXZSpace : IInputSpace
    {
        public Vector3 ResolveMove(Vector2 raw)
        {
            return new Vector3(raw.x, 0f, raw.y);
        }
    }
}
