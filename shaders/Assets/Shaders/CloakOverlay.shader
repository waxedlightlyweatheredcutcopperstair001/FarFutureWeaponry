Shader "FarFutureWeaponry/CloakOverlay"
{
    // Far Future Weaponry: the cloak. Drawn over the craft's parts while their own renderers are hidden: what's behind the
    // hull (a copy of the screen) shows through it, bent a little by the hull's shape, with a faint bluish outline; a band of
    // stronger bending and light ripples over the hull as it cloaks or uncloaks (_Wave).
    Properties
    {
        _Color ("Outline colour", Color) = (0.45,0.7,1,1)
        _Wave ("Ripple origin xyz, radius (m; <0 none)", Vector) = (0,0,0,-1)
        _CloakState ("Bend strength, outline strength, -, -", Vector) = (1,0.25,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_FfwCloakGrab" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _FfwCloakGrab;
            float4 _Color, _Wave, _CloakState;
            struct v2f { float4 pos:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float4 grab:TEXCOORD2; float3 vn:TEXCOORD3; };
            v2f vert(float4 vertex:POSITION, float3 normal:NORMAL)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(vertex);
                o.w=mul(unity_ObjectToWorld,vertex).xyz;
                o.n=UnityObjectToWorldNormal(normal);
                o.vn=mul((float3x3)UNITY_MATRIX_V,o.n);
                o.grab=ComputeGrabScreenPos(o.pos);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 v=normalize(_WorldSpaceCameraPos-i.w);
                float rim=1-abs(dot(normalize(i.n),v));
                float band=0;
                if(_Wave.w>=0) { float x=distance(i.w,_Wave.xyz)-_Wave.w; band=exp(-x*x/1.5); }
                float bend=(0.012+0.03*band)*_CloakState.x;
                float2 uv=i.grab.xy/i.grab.w+normalize(i.vn).xy*bend*(0.3+rim);
                float3 c=tex2D(_FfwCloakGrab,uv).rgb;
                c+=_Color.rgb*(pow(rim,4)*_CloakState.y+band*0.8);
                return float4(c,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
