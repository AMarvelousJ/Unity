Shader "Shiploader/HarborWater"
{
    Properties { _DeepColor("Deep water",Color)=(0.025,0.15,0.22,1) _SkyColor("Sky reflection",Color)=(0.42,0.66,0.78,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;half4 _SkyColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half fog:TEXCOORD1; };
            V vert(A i) {V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p) {float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
            float wave(float2 p,float t) {return noise(p*float2(.6,2.1)+float2(t*.1,0))*.5+noise(p*float2(1.3,4.1)+float2(-t*.14,t*.07))*.22+noise(p*.13+t*.02)*.3;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y;
                float c=wave(p,t);
                float fade=1/(1+length(fwidth(p))*.8);
                float3 n=normalize(float3((c-wave(p+float2(.15,0),t))*.85*fade,1,(c-wave(p+float2(0,.15),t))*.85*fade));
                float3 view=normalize(GetCameraPositionWS()-i.world);
                float fresnel=.13+.68*pow(1-saturate(dot(n,view)),4);
                Light sun=GetMainLight();float3 h=normalize(view+sun.direction);
                // Bounded broad glint prevents a grazing camera from producing an HDR whiteout.
                float glint=pow(saturate(dot(n,h)),96)*.08;
                half3 color=lerp(_DeepColor.rgb,_SkyColor.rgb,fresnel)+min(sun.color,half3(1,1,1))*glint;
                color+=(c-.5)*.035;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
