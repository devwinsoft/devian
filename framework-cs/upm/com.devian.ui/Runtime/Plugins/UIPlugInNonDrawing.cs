using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    /// <summary>
    /// A Graphic that does not draw anything.
    /// Useful for raycast targets or layout purposes without visual rendering.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIPlugInNonDrawing : Graphic
    {
        public override void SetMaterialDirty()
        {
            // No-op: nothing to render
        }

        public override void SetVerticesDirty()
        {
            // No-op: nothing to render
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
