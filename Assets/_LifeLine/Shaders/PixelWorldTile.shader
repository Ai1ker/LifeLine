Shader "LifeLine/Art/Pixel World Tile"
{
    Properties
    {
        [MainTexture] _BaseMap ("Tile", 2D) = "white" {}
        _Tint ("Tile tint", Color) = (1, 1, 1, 1)
        _TileSize ("Tile size in metres", Vector) = (1.3, 1.05, 0, 0)
        _TileRegion ("Texture region XY min / ZW size", Vector) = (.16, .23, .68, .57)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _TileSize;
                float4 _TileRegion;
                float4 _Tint;
            CBUFFER_END

            struct Varyings
            {
                float4 clipPosition : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION)
            {
                Varyings result;
                float3 world = TransformObjectToWorld(position);
                result.clipPosition = TransformWorldToHClip(world);
                result.worldPosition = world.xy;
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Repeat at fixed world scale so a wall or narrow jamb cannot stretch the art.
                float2 tileUV = frac(input.worldPosition / max(_TileSize.xy, .01));
                return half4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _TileRegion.xy + tileUV * _TileRegion.zw).rgb * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
