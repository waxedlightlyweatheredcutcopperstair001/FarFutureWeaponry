Shader "FarFutureWeaponry/PhaseLens"
{
    // Far Future Weaponry: the phase-shift missile vanishing and reappearing: a small gravitational lens with strong
    // chromatic aberration (the colours pulled apart, red bent least and blue most, plus a radial colour split), on a
    // camera-facing quad over a copy of the screen (an unnamed GrabPass: taken by whichever camera draws it). Outside the radius the copy is unchanged.
    Properties
    {
        _Lens ("Radius (m), strength, chroma, -", Vector) = (20,1,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { }      // (unnamed: grabbed per draw, by the camera drawing it; a named grab can come from another camera)
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _GrabTexture;
            float4 _Lens;
            struct v2f { float4 pos:SV_POSITION; float2 local:TEXCOORD0; float4 grab:TEXCOORD1; float scale:TEXCOORD2; };
            v2f vert(float4 vertex:POSITION)
            {
                v2f o;
                float3 centre=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
                float3 view=mul(UNITY_MATRIX_V,float4(centre,1)).xyz;
                float dist=max(-view.z,0.5);
                float R=min(_Lens.x,0.9*dist);
                float3 v=view; v.xy+=vertex.xy*R;
                o.pos=mul(UNITY_MATRIX_P,float4(v,1));
                o.local=vertex.xy;
                o.grab=ComputeGrabScreenPos(o.pos);
                o.scale=0.5*R/dist*UNITY_MATRIX_P[1][1];   // the radius on screen, in screen heights
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w;
                float r=length(i.local);
                float w=1-smoothstep(0.55,1,r);
                float2 dir=r>1e-4?i.local/r:0;
                float aspect=_ScreenParams.y/_ScreenParams.x;
                float2 toUv=float2(aspect,1)*i.scale;
                // The lens: pulled towards the centre, strongest at mid-radius.
                float pull=_Lens.y*w*0.35*r*(1-r);
                float2 base=-dir*pull*toUv;
                // Chromatic aberration: the channels bent by different amounts, and split radially.
                float c=_Lens.z*w;
                float2 split=dir*c*0.08*toUv;
                float rC=tex2D(_GrabTexture,uv+base*(1-0.6*c)+split).r;
                float gC=tex2D(_GrabTexture,uv+base).g;
                float bC=tex2D(_GrabTexture,uv+base*(1+0.6*c)-split).b;
                return float4(rC,gC,bC,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
