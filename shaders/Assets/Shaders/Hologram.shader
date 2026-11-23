Shader "FarFutureWeaponry/Hologram"
{
    // Far Future Weaponry: a hologram of a craft. Translucent cyan light with an outline, scanlines crawling up it, a
    // flicker, and now and then a glitch: a band of it torn sideways. Added.
    Properties
    {
        _Color ("Colour", Color) = (0.35,0.8,1,1)
        _HoloState ("Strength 0-1, seed, glitch 0-1, -", Vector) = (1,0,0.3,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Noise.cginc"
            float4 _Color, _HoloState;
            struct v2f { float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL)
            {
                v2f o;
                float3 w=mul(unity_ObjectToWorld,vertex).xyz;
                // A glitch: now and then a band of the hologram is torn sideways for a moment.
                float slot=floor(_Time.y*7+_HoloState.y);
                float on=step(1-0.25*_HoloState.z,FfwHash(float3(slot,_HoloState.y,1)));
                float bandY=FfwHash(float3(slot,2,_HoloState.y))*20-10;
                float inBand=step(abs(w.y-mul(unity_ObjectToWorld,float4(0,0,0,1)).y-bandY),1.5);
                w.x+=on*inBand*(FfwHash(float3(slot,3,0))*2-1)*1.5;
                o.pos=mul(UNITY_MATRIX_VP,float4(w,1));
                o.w=w;
                o.n=UnityObjectToWorldNormal(normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 v=normalize(_WorldSpaceCameraPos-i.w);
                float rim=1-abs(dot(normalize(i.n),v));
                float scan=0.65+0.35*sin(i.w.y*6-_Time.y*8);
                float flicker=0.85+0.15*sin(_Time.y*37+_HoloState.y)*sin(_Time.y*13);
                float k=(0.18+0.9*rim*rim)*scan*flicker*_HoloState.x;
                return float4(max(_Color.rgb*k,0),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
