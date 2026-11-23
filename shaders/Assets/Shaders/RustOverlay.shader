Shader "FarFutureWeaponry/RustOverlay"
{
    // Far Future Weaponry: rust drawn over a part (the Tachyon Gun, the Chronos Rust-Dart). Patches grow from noise in the
    // part's own space (so they stay put on it), their coverage set by the rust level; dull orange-brown with darker
    // pitting, lit by the sun and the sky, blended over the part's own surface.
    Properties
    {
        _Rust ("Rust level", Range(0,1)) = 0
        _Scale ("Part scale xyz, patch size (m)", Vector) = (1,1,1,0.4)
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off ZTest LEqual Cull Back
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "Noise.cginc"
            float _Rust, _Seed;
            float4 _Scale;
            struct v2f { float4 pos:SV_POSITION; float3 p:TEXCOORD0; float3 n:TEXCOORD1; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(vertex);
                o.p=vertex.xyz*_Scale.xyz/max(_Scale.w,0.01)+_Seed;
                o.n=UnityObjectToWorldNormal(normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                if(_Rust<0.002) discard;
                float big=FfwFbm(i.p), fine=FfwNoise(i.p*7.1);
                // Coverage: the noisiest spots first, then spreading. (The noise mostly lies between 0.2 and 0.8: the threshold
                // runs over that range, so the share covered roughly follows the level: a few spots at 0.05, half at 0.5.)
                float t=lerp(0.8,0.18,_Rust);
                float m=smoothstep(t-0.035,t+0.035,big*0.85+fine*0.15);
                if(m<0.01) discard;
                float tone=FfwFbm(i.p*2.7+5.3);
                float3 albedo=lerp(float3(0.22,0.1,0.04),float3(0.6,0.3,0.1),tone);
                albedo*=lerp(0.65,1.0,smoothstep(0.3,0.6,fine));   // pitting
                float3 n=normalize(i.n);
                float ndl=saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
                float3 lit=albedo*(ShadeSH9(float4(n,1))+_LightColor0.rgb*ndl);
                return float4(lit,m);
            }
            ENDCG
        }
    }
    Fallback Off
}
