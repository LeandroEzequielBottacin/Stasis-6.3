#ifndef STASIS_ELECTRIC_INCLUDED
#define STASIS_ELECTRIC_INCLUDED

// Shared electricity model for the stasis outline.
//
// Used by both S_StasisOutline.shader (the inverted-hull fallback) and
// S_StasisOutlineScreen.shader (the screen-space renderer feature), so the two paths
// can't drift apart visually.
//
// Everything is driven from an OBJECT-SPACE position, which is what keeps the pattern
// stuck to the object instead of swimming when the camera or the object moves.

float StasisHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float StasisHash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float StasisVNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    return lerp(lerp(lerp(StasisHash31(i + float3(0, 0, 0)), StasisHash31(i + float3(1, 0, 0)), f.x),
                     lerp(StasisHash31(i + float3(0, 1, 0)), StasisHash31(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(StasisHash31(i + float3(0, 0, 1)), StasisHash31(i + float3(1, 0, 1)), f.x),
                     lerp(StasisHash31(i + float3(0, 1, 1)), StasisHash31(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float StasisFbm(float3 p)
{
    float s = 0.0, a = 0.5;
    [unroll] for (int k = 0; k < 3; k++)
    {
        s += a * StasisVNoise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return s / 0.875;
}

// Thin filament along the zero-crossing of the noise field.
//
// Ridged noise (1 - abs(n*2-1)) is the usual trick here, but value-noise fbm barely
// leaves the 0.35..0.65 band, so ridging it saturates to ~1 everywhere and the outline
// reads as a flat solid line. Measuring the distance to the n == 0.5 contour instead
// gives genuinely thin arcs, and 'thinness' controls their width directly.
float StasisFilament(float3 p, float thinness)
{
    float s = StasisFbm(p) - 0.5;
    return saturate(1.0 - abs(s) * thinness);
}

// Irregular mains-like flicker.
float StasisFlicker(float time, float strength, float speed)
{
    float fs = time * speed;
    float f = lerp(StasisHash11(floor(fs)), StasisHash11(floor(fs) + 1.0), smoothstep(0.0, 1.0, frac(fs)));
    return lerp(1.0, 0.55 + f * 0.75, strength);
}

// Layer 1: branching filaments travelling over the surface.
float StasisArcs(float3 posOS, float time, float scale, float speed, float thinness, float falloff, float branching)
{
    float3 ap = posOS * scale + float3(0.0, time * speed, time * speed * 0.31);
    float a1 = pow(StasisFilament(ap, thinness), falloff);

    // A second, finer layer scrolling the other way reads as branching.
    float3 bp = posOS * (scale * 1.87) + float3(time * speed * -0.73, time * speed * 0.44, 11.3);
    float a2 = pow(StasisFilament(bp, thinness * 1.6), falloff);

    return max(a1, a2 * branching);
}

// Layer 2: soft energy field flowing over the surface.
float StasisPlasma(float3 posOS, float time, float scale, float speed)
{
    float3 pp = posOS * scale + float3(time * speed * 0.5, time * speed, -time * speed * 0.3);
    // StasisFbm only spans roughly 0.35..0.65, so stretch it before use or the plasma
    // layer is a flat mid-grey wash.
    float v = saturate((StasisFbm(pp) - 0.38) * 4.0);
    return v * v;
}

#endif // STASIS_ELECTRIC_INCLUDED
