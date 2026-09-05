# AcuLume algorithm notes

Tracks the working-space math actually implemented, per spec section 14 and 61. Updated as bands/stages are added.

## Working color space (Tasks 4-5)

Sharpening operates on the **L channel of a CIE Lab conversion** of the working image:

1. Convert sRGB → Lab (`Image.Colourspace(Enums.Interpretation.Lab)`) once per call.
2. Extract L (band 0); a/b (bands 1-2) are left untouched for the whole pipeline.
3. For each enabled band (fine, medium — spec section 15):
   - High-pass: `highPass = L - GaussianBlur(L, radius)` (radius = Gaussian sigma in px).
   - Asymmetric split: `weighted = highPass < 0 ? highPass * darkAmount : highPass * lightAmount`.
   - Scale: `contribution = weighted * amount`.
4. Sum every band's `contribution` into one total, then `L' = L + total` (added once, not per band).
5. Recombine `L'` with the untouched a/b, convert back to the original interpretation.

Both bands are simple independent high-pass extractions at their own radius (not a difference-of-Gaussians
isolated band) — chosen per spec section 16 for predictability/testability over spectral purity in Phase 1.
Medium is generally weaker than fine (spec 15.2) purely via preset `amount`, not architecturally.

Leaving chroma (a/b) untouched is what prevents chromatic edge halos (spec section 14) — color
never changes, only lightness.

Implementation: `AcuLume.Core.Sharpening.OutputSharpenStage`, `BandSharpenOptions`,
`FrequencyBandExtractor`, `AsymmetricDetailMixer`.

## Edge protection (Task 6)

Applied once, after summing band contributions, before adding the total back to L:

1. Denoise: `denoised = GaussianBlur(L, detectionBlur)`.
2. Gradient: Scharr `gx`, `gy` via 3x3 convolution (preferred over Sobel per spec section 19 for
   better rotational symmetry); `magnitude = sqrt(gx^2 + gy^2)`.
3. Mask: `mask = smoothstep(magnitude, threshold - softness/2, threshold + softness/2)` — 0 in
   flat areas, ramping smoothly to 1 on strong edges. See `SoftThreshold.Smoothstep` (also reused
   by noise protection later).
4. Attenuate: `contribution *= (1 - mask * edgeProtectionAmount)`. `edgeProtectionAmount = 0`
   disables the stage entirely; at `1.0` a fully-saturated edge gets the contribution zeroed, but
   the smoothstep ramp means it is never an abrupt cutoff (spec: "must attenuate progressively,
   never completely suppress detail around all edges" — full suppression only happens exactly at
   the mask's asymptotic maximum on very strong edges, by design, at `amount = 1.0`).

Implementation: `AcuLume.Core.Sharpening.EdgeMaskBuilder`, `SoftThreshold`.

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Soft noise protection (Task 7).
- Halo limiter (Task 8).
- Capture sharpening, presets, output-size-aware radius scaling.
