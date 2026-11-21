Shader "LifeLine/Art/Pixel Ocean Backdrop"
{
    Properties
    {
        _Sky ("Surface sky", 2D) = "white" {}
        _WaterlineSky ("Sky above the miniature boat", 2D) = "white" {}
        _CityCoast ("Flooded city silhouette", 2D) = "black" {}
        _CityCoastMapping ("Coast horizon, height and availability", Vector) = (.226,8,0,0)
        _Shallows ("Shallows", 2D) = "white" {}
        _City ("Submerged city", 2D) = "white" {}
        _Deep ("Deep water", 2D) = "black" {}
        _IsSurface ("Surface map", Float) = 0
        _HorizonY ("Water line", Float) = -1.5
        _PanoramaWidth ("Panorama width", Float) = 55
        _DeckSkyHorizon ("Coast texture waterline UV", Range(0,.5)) = .12
        _Regions ("Shallow / deep transition and floor depth", Vector) = (53,124,8,54)
        _WaterlineSkyMapping ("Sky width, height, speed and texture presence", Vector) = (7.5,2.5,.27,0)
        _UnderwaterColorBlend ("Blue palette blend", Range(0,1)) = .85
        _UnderwaterShadows ("Underwater palette shadows", Color) = (.045,.115,.215,1)
        _UnderwaterHighlights ("Underwater palette highlights", Color) = (.29,.54,.83,1)
        _SceneTint ("Weather tint", Color) = (1,1,1,1)
        _SkyTint ("Sky tint", Color) = (1,1,1,1)
        _Motion ("Elapsed and ship position", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Background" }
        Pass
        {
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Sky); SAMPLER(sampler_Sky);
            TEXTURE2D(_WaterlineSky); SAMPLER(sampler_WaterlineSky);
            TEXTURE2D(_CityCoast); SAMPLER(sampler_CityCoast);
            TEXTURE2D(_Shallows); SAMPLER(sampler_Shallows);
            TEXTURE2D(_City); SAMPLER(sampler_City);
            TEXTURE2D(_Deep); SAMPLER(sampler_Deep);
            CBUFFER_START(UnityPerMaterial)
                float _IsSurface, _HorizonY, _PanoramaWidth, _UnderwaterColorBlend;
                float _DeckSkyHorizon;
                float4 _Regions, _SceneTint, _SkyTint, _Motion;
                float4 _WaterlineSkyMapping, _UnderwaterShadows, _UnderwaterHighlights;
                float4 _CityCoastMapping;
            CBUFFER_END

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 worldXY : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION)
            {
                Varyings result;
                float3 world = TransformObjectToWorld(position);
                result.position = TransformWorldToHClip(world);
                result.worldXY = world.xy;
                return result;
            }

            float Mirror(float coordinate)
            {
                return 1 - abs(frac(coordinate * .5) * 2 - 1);
            }

            half3 CityCoast(half3 sky, float x, float above)
            {
                float y = above / max(1, _CityCoastMapping.y) + _CityCoastMapping.x;
                half4 coast = SAMPLE_TEXTURE2D(_CityCoast, sampler_CityCoast, float2(x, saturate(y)));
                return lerp(sky, coast.rgb * _SceneTint.rgb, coast.a * .84);
            }

            half3 StormRain(half3 sky, float2 world, float time)
            {
                if (_Motion.w < .5) return sky;
                float2 pixel = floor(world * 36) / 36;
                float2 slant = float2(pixel.x + pixel.y * .36, pixel.y + time * 13);
                float column = floor(slant.x * 4.7);
                float phase = frac(sin(column * 41.13) * 237.77);
                float streak = step(frac(slant.x * 4.7), .13) * step(frac(slant.y * .72 + phase), .29);
                return lerp(sky, half3(.25,.43,.67), streak * .46);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 position = input.worldXY;
                float above = position.y - _HorizonY;
                float time = _Motion.x;
                half3 color;
                if (_IsSurface > .5)
                {
                    float2 pixel = floor(float2(position.x, above) * 22) / 22;
                    float wave = sin(pixel.x * .82 + time * 1.6) * .10 + sin(pixel.x * 2.2 - time * 2.0) * .032;
                    if (above >= wave + .036)
                    {
                        float x = Mirror((position.x - _Motion.y * .65 + 20 + time * .09) / _PanoramaWidth);
                        float y = lerp(_DeckSkyHorizon, 1, saturate(above / 15));
                        half3 sky = SAMPLE_TEXTURE2D(_Sky, sampler_Sky, float2(x, y)).rgb * _SkyTint.rgb;
                        if (_Motion.z > 0 && _CityCoastMapping.z > .5)
                        {
                            half3 city = SAMPLE_TEXTURE2D(_WaterlineSky, sampler_WaterlineSky, float2(x, saturate(above / 9))).rgb * _SkyTint.rgb;
                            float coastX = Mirror((position.x - _Motion.y * .65 - 17 + time * .09) / _PanoramaWidth);
                            sky = lerp(sky, CityCoast(city, coastX, above * .62), _Motion.z);
                        }
                        return half4(StormRain(sky, position, time), 1);
                    }
                    float depth = max(0, wave - pixel.y);
                    half3 top = half3(.13, .58, .67);
                    half3 bottom = half3(.026, .26, .35);
                    color = lerp(top, bottom, saturate(depth * .20));
                    float waterBands = frac(pixel.y * 2.1 + sin(pixel.x * .5 + time * 1.1) * .19);
                    color += half3(.046,.073,.075) * (1 - step(.13, waterBands)) * (1 - saturate(depth * .18));
                    if (abs(above - wave) < .055) color = half3(.60,.85,.83);
                }
                else if (above >= 0)
                {
                    float2 metres = max(_WaterlineSkyMapping.xy, 1);
                    float x = Mirror((position.x - time * _WaterlineSkyMapping.z) / metres.x + .32);
                    float y = saturate(above / metres.y);
                    color = _WaterlineSkyMapping.w > .5
                        ? SAMPLE_TEXTURE2D(_WaterlineSky, sampler_WaterlineSky, float2(x, y)).rgb
                        : lerp(half3(.31,.46,.72), half3(.096,.20,.42), y);
                    if (y > .9 && _WaterlineSkyMapping.w > .5)
                    {
                        half3 zenith = SAMPLE_TEXTURE2D(_WaterlineSky, sampler_WaterlineSky, float2(.5, 1)).rgb;
                        color = lerp(color, zenith, smoothstep(.9, 1, y));
                    }
                    color *= _SkyTint.rgb;
                    if (_Motion.z > 0 && _CityCoastMapping.z > .5)
                    {
                        float coastX = Mirror((position.x - _Motion.y * .65 - 17 + time * .09) / _PanoramaWidth);
                        color = lerp(color, CityCoast(color, coastX, above), _Motion.z);
                    }
                    return half4(StormRain(color, position, time), 1);
                }
                else
                {
                    float depth = -above;
                    float x = Mirror((position.x * .83 + sin(time * .15) * .75 + 12) / _PanoramaWidth);
                    float y = 1 - saturate(depth / max(1, _Regions.w));
                    float2 uv = float2(x, y);
                    half3 shallow = SAMPLE_TEXTURE2D(_Shallows, sampler_Shallows, uv).rgb;
                    half3 city = SAMPLE_TEXTURE2D(_City, sampler_City, uv).rgb;
                    half3 deep = SAMPLE_TEXTURE2D(_Deep, sampler_Deep, uv).rgb;
                    float blend = max(1, _Regions.z);
                    float cityWeight = smoothstep(_Regions.x - blend, _Regions.x + blend, position.x);
                    float deepWeight = smoothstep(_Regions.y - blend, _Regions.y + blend, position.x);
                    color = lerp(lerp(shallow, city, cityWeight), deep, deepWeight);
                    half light = dot(color, half3(.2126,.7152,.0722));
                    half3 oceanBlue = lerp(_UnderwaterShadows.rgb, _UnderwaterHighlights.rgb, saturate(light * 1.7));
                    color = lerp(color, oceanBlue, _UnderwaterColorBlend);
                    float abyss = smoothstep(12, max(13, _Regions.w), depth);
                    color = lerp(color, _UnderwaterShadows.rgb, abyss * (.44 + .14 * deepWeight));
                    if (depth < .16)
                    {
                        float pixelX = floor(position.x * 24) / 24;
                        float waveDepth = .05 + sin(pixelX * 1.4 - time * 1.9) * .035;
                        float foam = 1 - step(.024, abs(depth - waveDepth));
                        color = lerp(color, half3(.36,.60,.82), foam * .58);
                    }
                }
                return half4(color * _SceneTint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
