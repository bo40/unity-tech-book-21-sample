// 弾・自機・ボスを、StructuredBuffer に入った円として 1 回の描画命令でまとめて描く。
// 1 個あたり 6 頂点（四角形 2 枚）を頂点シェーダーで組み立て、フラグメントで円にくり抜く
Shader "PolyglotBossRush/Circle"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // C# の Bullet、Rust の danmaku::Bullet と同じ並び（24 バイト）
            struct Bullet
            {
                float2 position;
                float2 velocity;
                float accel;
                uint rgba;
            };

            StructuredBuffer<Bullet> _Circles;
            float _Radius;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
                float4 color : COLOR;
            };

            static const float2 Corners[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(-1, 1),
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Bullet circle = _Circles[instanceID];
                float2 corner = Corners[vertexID];
                Varyings output;
                output.positionCS = TransformWorldToHClip(float3(circle.position + corner * _Radius, 0));
                output.local = corner;
                uint c = circle.rgba;
                output.color = float4((c >> 24) & 255, (c >> 16) & 255, (c >> 8) & 255, c & 255) / 255.0;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float distance = length(input.local);
                clip(1 - distance);
                // 中心は白く光らせ、縁はなめらかに消す
                float3 color = lerp(input.color.rgb, float3(1, 1, 1), smoothstep(0.55, 0.0, distance));
                return half4(color, input.color.a * smoothstep(1.0, 0.8, distance));
            }
            ENDHLSL
        }
    }
}
