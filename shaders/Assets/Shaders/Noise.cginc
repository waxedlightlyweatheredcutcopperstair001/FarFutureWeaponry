// Far Future Weaponry: value noise in 3D (no textures), for the overlays.
#ifndef FFW_NOISE
#define FFW_NOISE
float FfwHash(float3 p)
{
    p=frac(p*0.3183099+0.1);
    p*=17.0;
    return frac(p.x*p.y*p.z*(p.x+p.y+p.z));
}
float FfwNoise(float3 x)
{
    float3 i=floor(x), f=frac(x);
    f=f*f*(3-2*f);
    return lerp(lerp(lerp(FfwHash(i+float3(0,0,0)),FfwHash(i+float3(1,0,0)),f.x),
                     lerp(FfwHash(i+float3(0,1,0)),FfwHash(i+float3(1,1,0)),f.x),f.y),
                lerp(lerp(FfwHash(i+float3(0,0,1)),FfwHash(i+float3(1,0,1)),f.x),
                     lerp(FfwHash(i+float3(0,1,1)),FfwHash(i+float3(1,1,1)),f.x),f.y),f.z);
}
float FfwFbm(float3 p)
{
    float a=0.5, s=0;
    for(int k=0;k<4;k++) { s+=a*FfwNoise(p); p=p*2.03+17.1; a*=0.5; }
    return s/0.9375;
}
#endif
