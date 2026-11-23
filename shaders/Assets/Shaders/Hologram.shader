Shader "FarFutureWeaponry/Hologram"
{
    // Far Future Weaponry: a hologram of a craft. The craft's own colours (its texture and paint, lit by the sun),
    // tinted a little blue and semi-transparent, with a brighter blue rim, faint scanlines crawling up it, a flicker,
    // and now and then a glitch: a band of it torn sideways. A depth pre-pass keeps it reading as one solid,
    // see-through shape rather than a jumble of every inner face.
    Properties
    {
        _MainTex ("Craft texture", 2D) = "white" {}
        _BaseColor ("Craft paint", Color) = (1,1,1,1)
        _Color ("Hologram tint", Color) = (0.55,0.8,1,1)
        _Opacity ("Opacity", Range(0,1)) = 0.55
        _HoloState ("Strength 0-1, seed, glitch 0-1, -", Vector) = (1,0,0.3,0)
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Noise.cginc"
    float4 _HoloState;
    // A glitch: now and then a band of the hologram is torn sideways for a moment.
    float3 Glitch(float3 w)
    {
        float slot=floor(_Time.y*7+_HoloState.y);
        float on=step(1-0.25*_HoloState.z,FfwHash(float3(slot,_HoloState.y,1)));
        float bandY=FfwHash(float3(slot,2,_HoloState.y))*20-10;
        float inBand=step(abs(w.y-mul(unity_ObjectToWorld,float4(0,0,0,1)).y-bandY),1.5);
        w.x+=on*inBand*(FfwHash(float3(slot,3,0))*2-1)*1.5;
        return w;
    }
    ENDCG
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            // Depth only: the nearest surface wins, so the hologram is see-through once, not layer upon layer.
            ZWrite On ColorMask 0 Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 vertex:POSITION):SV_POSITION
            {
                float3 w=Glitch(mul(unity_ObjectToWorld,vertex).xyz);
                return mul(UNITY_MATRIX_VP,float4(w,1));
            }
            float4 frag(float4 pos:SV_POSITION):SV_Target { return 0; }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off ZTest LEqual Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityLightingCommon.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            float4 _BaseColor, _Color; float _Opacity;
            struct v2f { float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL, float2 uv:TEXCOORD0)
            {
                v2f o;
                float3 w=Glitch(mul(unity_ObjectToWorld,vertex).xyz);
                o.pos=mul(UNITY_MATRIX_VP,float4(w,1));
                o.w=w;
                o.n=UnityObjectToWorldNormal(normal);
                o.uv=TRANSFORM_TEX(uv,_MainTex);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=FfwNormalize(i.n,float3(0,1,0));
                float3 v=FfwNormalize(_WorldSpaceCameraPos-i.w,float3(0,1,0));
                float rim=1-abs(dot(n,v));
                // The craft's own colour, lit softly (so its shape reads), then pulled a little towards the tint.
                float3 albedo=tex2D(_MainTex,i.uv).rgb*_BaseColor.rgb;
                float3 light=0.45+0.55*saturate(dot(n,FfwNormalize(_WorldSpaceLightPos0.xyz,float3(0,1,0))))*_LightColor0.rgb;
                float3 c=albedo*light;
                float lum=dot(c,float3(0.3,0.59,0.11));
                c=lerp(c,lum*_Color.rgb*1.3+_Color.rgb*0.08,0.3);
                float scan=0.88+0.12*sin(i.w.y*6-_Time.y*8);
                float flicker=0.9+0.1*sin(_Time.y*37+_HoloState.y)*sin(_Time.y*13);
                c=c*scan+_Color.rgb*pow(rim,3)*0.6;
                float a=saturate(_Opacity+0.3*pow(rim,3))*flicker*_HoloState.x;
                return float4(saturate(c),saturate(a));
            }
            ENDCG
        }
    }
    Fallback Off
}
