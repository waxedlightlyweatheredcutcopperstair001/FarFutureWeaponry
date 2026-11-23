Shader "FarFutureWeaponry/ContourWave"
{
    // Far Future Weaponry: a thin layer of light drawn over a craft's parts (the Kraken missile's red wave; later the force
    // field and the cloak). A band at a distance from an origin (it sweeps over the hull's contours as the radius grows),
    // a fading trail behind it, brighter where the surface turns away from the eye, with a faint crawling texture. Added.
    Properties
    {
        _Wave ("Origin xyz (world), radius (m)", Vector) = (0,0,0,1)
        _WaveShape ("Band width (m), trail length (m), intensity, crawl", Vector) = (0.6,4,1,1)
        _Color ("Colour", Color) = (1,0.1,0.1,1)
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
            float4 _Wave, _WaveShape, _Color;
            struct v2f { float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(vertex);
                o.w=mul(unity_ObjectToWorld,vertex).xyz;
                o.n=UnityObjectToWorldNormal(normal);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float x=distance(i.w,_Wave.xyz)-_Wave.w;
                float w=max(_WaveShape.x,1e-3);
                float band=exp(-(x*x)/(w*w));
                float trail=x<0?0.3*exp(x/max(_WaveShape.y,1e-3)):0;
                float3 v=FfwNormalize(_WorldSpaceCameraPos-i.w,float3(0,1,0));
                float rim=1-abs(dot(FfwNormalize(i.n,v),v));
                float crawl=lerp(1,0.55+0.9*FfwNoise(i.w*1.7+_Time.y*float3(0,2.5,0)),saturate(_WaveShape.w));
                float k=(band*1.6+trail)*(0.5+0.8*rim*rim)*crawl*_WaveShape.z;
                return float4(max(_Color.rgb*k,0),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
