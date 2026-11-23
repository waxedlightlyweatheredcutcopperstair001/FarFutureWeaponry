Shader "FarFutureWeaponry/ShieldOverlay"
{
    // Far Future Weaponry: the force field, a thin electric-blue layer over the craft's parts. A steady outline (brighter
    // where the surface turns away from the eye) with a slow crawling shimmer; as it comes up, a bright band sweeps out over
    // the hull from its middle (_Wave); each blocked hit lights a bright spot that fades (_Hit0..3: position, age).
    Properties
    {
        _Color ("Colour", Color) = (0.3,0.65,1,1)
        _Wave ("Sweep origin xyz, radius (m; <0 none)", Vector) = (0,0,0,-1)
        _ShieldState ("Level 0-1, time, -, -", Vector) = (1,0,0,0)
        _Hit0 ("Hit 0: xyz, age (s; <0 none)", Vector) = (0,0,0,-1)
        _Hit1 ("Hit 1", Vector) = (0,0,0,-1)
        _Hit2 ("Hit 2", Vector) = (0,0,0,-1)
        _Hit3 ("Hit 3", Vector) = (0,0,0,-1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            Offset -1, -1
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Noise.cginc"
            float4 _Color, _Wave, _ShieldState, _Hit0, _Hit1, _Hit2, _Hit3;
            struct v2f { float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(vertex);
                o.w=mul(unity_ObjectToWorld,vertex).xyz;
                o.n=UnityObjectToWorldNormal(normal);
                return o;
            }
            float Hit(float4 h,float3 w)
            {
                if(h.w<0||h.w>0.6) return 0;
                float d=distance(w,h.xyz);
                return exp(-d*d/4)*(1-h.w/0.6)*3;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 v=FfwNormalize(_WorldSpaceCameraPos-i.w,float3(0,1,0));
                float rim=1-abs(dot(FfwNormalize(i.n,v),v));
                float shimmer=0.7+0.6*FfwNoise(i.w*0.9+_ShieldState.y*float3(0.3,0.8,0.2));
                float k=(0.08+0.9*rim*rim*rim)*shimmer*_ShieldState.x;
                if(_Wave.w>=0)
                {
                    float x=distance(i.w,_Wave.xyz)-_Wave.w;
                    k+=1.5*exp(-x*x/0.8);
                }
                k+=Hit(_Hit0,i.w)+Hit(_Hit1,i.w)+Hit(_Hit2,i.w)+Hit(_Hit3,i.w);
                return float4(max(_Color.rgb*k,0),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
