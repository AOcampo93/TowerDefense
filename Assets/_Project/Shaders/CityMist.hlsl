// Low mist between the buildings: banks about as high as the small buildings that drift with the wind, covering part
// of the city and leaving other parts clear. Shared by the city shaders. The values are globals set by CityGenerator;
// before the city exists they are zero and there is no mist.
#ifndef ALIEN_INVADERS_CITY_MIST
#define ALIEN_INVADERS_CITY_MIST

TEXTURE2D(_AI_MistNoise);
SAMPLER(sampler_AI_MistNoise);
// World to city space (the city leans and drops with the tower, the mist goes with it).
float4x4 _AI_CityWorldToLocal;
// x = street height in city space, y = height of the mist top over the street, z = strength, w = wind (m/s).
float4 _AI_Mist;
half4 _AI_MistColor;

float3 CityPosition(float3 positionWS)
{
    return mul(_AI_CityWorldToLocal, float4(positionWS, 1.0)).xyz;
}

// How much mist covers this point, from 0 (clear) to 1.
half CityMist(float3 cityPosition)
{
    float above = max(0.0, cityPosition.y - _AI_Mist.x);
    // A low layer with a soft top.
    half layer = 1.0 - smoothstep(_AI_Mist.y * 0.4, max(_AI_Mist.y, 1.0), above);
    // Banks of mist: two noise layers drifting at different speeds, so the banks change shape as they travel.
    float2 drift = float2(0.8, 0.6) * _AI_Mist.w * _Time.y;
    half big = SAMPLE_TEXTURE2D(_AI_MistNoise, sampler_AI_MistNoise, (cityPosition.xz - drift) / 220.0).r;
    half small = SAMPLE_TEXTURE2D(_AI_MistNoise, sampler_AI_MistNoise, (cityPosition.xz - drift * 0.5) / 140.0 + 0.37).r;
    half banks = smoothstep(0.42, 0.62, big * 0.65 + small * 0.35);
    return saturate(_AI_Mist.z * layer * banks);
}

half3 ApplyCityMist(half3 color, float3 cityPosition)
{
    return lerp(color, _AI_MistColor.rgb, CityMist(cityPosition));
}

#endif
