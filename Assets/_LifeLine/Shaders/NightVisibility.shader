Shader "LifeLine/UI/Night Visibility"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DarknessColor ("Night Color", Color) = (0.006,0.014,0.025,0.98)
        _FocusUV ("Focus in Viewport", Vector) = (0.5,0.5,0,0)
        _ViewportPixels ("Viewport Pixel Size", Vector) = (960,1080,0,0)
        _MaskRect ("Image Local Rect", Vector) = (-480,-540,960,1080)
        _RadiusPixels ("Visible Radius (pixels)", Float) = 60
        _FeatherPixels ("Soft Edge (pixels)", Float) = 20
        _Opacity ("Opacity", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "NightVisibility"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd, _Color, _DarknessColor;
            float4 _FocusUV, _ViewportPixels, _MaskRect, _ClipRect;
            float _RadiusPixels, _FeatherPixels, _Opacity;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                // UGUI batching transforms vertex positions into Canvas space. The dedicated
                // sprite-less Image keeps stable 0..1 UVs across batching and split viewports.
                float2 viewportUV = input.texcoord;
                float distancePixels = length((viewportUV - _FocusUV.xy) * max(_ViewportPixels.xy, 1.0));
                float radius = max(1.0, _RadiusPixels);
                float feather = max(0.01, min(radius, _FeatherPixels));
                float coverage = smoothstep(radius - feather, radius, distancePixels);
                fixed alpha = coverage * saturate(_Opacity) * _DarknessColor.a * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    alpha *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(alpha - 0.001);
                #endif
                return fixed4(_DarknessColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
