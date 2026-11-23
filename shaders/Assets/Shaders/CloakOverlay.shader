Shader "FarFutureWeaponry/CloakOverlay"
{
    // Far Future Weaponry: the cloak. Drawn over the craft's hull while the parts' own renderers are hidden: nothing but
    // a faint bluish outline (light added at grazing angles) and, as it cloaks or uncloaks, a band of light rippling over
    // the hull (_Wave). While it sweeps (_CloakState.z), only the cloaked side of the band is drawn: inside it when
    // cloaking (z = 1), outside it when uncloaking (z = -1), so the craft turns invisible (or visible) as the wave passes.
    // No screen copy (GrabPass): with KSP's several cameras, Deferred and Scatterer, a copied screen can come from the
    // wrong camera or the wrong moment and paints stale sky over the view.
    Properties
    {
        _Color ("Outline colour", Color) = (0.45,0.7,1,1)
        _Wave ("Ripple origin xyz, radius (m; <0 none)", Vector) = (0,0,0,-1)
        _CloakState ("Strength, outline strength, sweep (1 cloaking, -1 uncloaking, 0 none), -", Vector) = (1,0.25,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color, _Wave, _CloakState;
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
                float3 v=normalize(_WorldSpaceCameraPos-i.w);
                float rim=1-abs(dot(normalize(i.n),v));
                float band=0;
                if(_Wave.w>=0)
                {
                    float x=distance(i.w,_Wave.xyz)-_Wave.w;
                    band=exp(-x*x/1.5);
                    if(_CloakState.z>0.5&&x>1.5) discard;     // not reached yet: still visible
                    if(_CloakState.z<-0.5&&x<-1.5) discard;   // already uncloaked
                }
                float k=(pow(rim,4)*_CloakState.y+band*0.6)*_CloakState.x;
                return float4(_Color.rgb*k,0);
            }
            ENDCG
        }
    }
    Fallback Off
}
