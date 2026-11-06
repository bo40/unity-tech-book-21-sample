using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PolyglotBossRush
{
    // Bullet の配列を GPU に送り、Graphics.RenderPrimitives で一度に描く
    public sealed class CircleRenderer : IDisposable
    {
        static readonly int CirclesId = Shader.PropertyToID("_Circles");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");

        readonly GraphicsBuffer buffer;
        readonly Material material;
        RenderParams renderParams;

        public CircleRenderer(int capacity, float radius)
        {
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 5 + sizeof(uint));
            material = new Material(Resources.Load<Shader>("PolyglotCircle"));
            renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
                matProps = new MaterialPropertyBlock(),
                shadowCastingMode = ShadowCastingMode.Off,
            };
            renderParams.matProps.SetBuffer(CirclesId, buffer);
            renderParams.matProps.SetFloat(RadiusId, radius);
        }

        public void Draw(Bullet[] circles, int count)
        {
            if (count == 0)
                return;
            buffer.SetData(circles, 0, 0, count);
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, count);
        }

        public void Dispose()
        {
            buffer.Dispose();
            UnityEngine.Object.Destroy(material);
        }
    }
}
