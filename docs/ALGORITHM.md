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

## Noise protection (Task 7)

Applied to each band's raw high-pass detail, before the dark/light split (order matters: the
threshold decision uses the unsplit `abs(detail)`, so it can't come after the split):

```text
weight          = smoothstep(abs(detail), threshold - softness/2, threshold + softness/2)
effectiveWeight = 1 - amount * (1 - weight)   // amount blends "always pass" vs the full curve
detail'         = detail * effectiveWeight
```

`amount = 0` disables it (full detail always passes). Implementation:
`AcuLume.Core.Sharpening.NoiseProtection`, reusing `SoftThreshold.Smoothstep`.

Testing note: a whole-pipeline comparison against the *raw* input is misleading here — the
Lab↔sRGB colourspace round-trip itself has a float rounding error an order of magnitude larger
than the sharpening delta on near-noise-floor detail. Tests instead compare against a
round-trip-only baseline (same conversion, zero sharpening) so the round-trip error cancels out.

## Halo limiter (Task 8)

Applied once, after edge protection, right before the (edge-protected, noise-protected,
band-summed) contribution is added back to L:

1. Local contrast: `localMax`/`localMin` from an order-statistic ("rank") filter over a
   `(2*windowRadius+1)`-square window on the *pre-sharpening* luminance; `localRange = max - min`.
2. Limits: `maxOvershoot = localRange * lightLimit`, `maxUndershoot = localRange * darkLimit`
   (separate, because bright halos are usually more objectionable — spec section 20 — so the
   default `lightLimit` is tighter than `darkLimit`).
3. Clamp: `clamped = clamp(contribution, -maxUndershoot, maxOvershoot)`.
4. Blend: `final = contribution * (1 - amount) + clamped * amount`. `amount = 0` disables it.

Because the limits scale with *local* contrast rather than a fixed number, a genuinely
high-contrast edge is allowed a large correction while a flat area's overshoot gets clamped
toward zero — this is what keeps the limiter from being "final-image clipping" (spec: "do not
silently clip the final image as the primary halo-management technique").

Implementation: `AcuLume.Core.Sharpening.HaloLimiter`, `HaloLimiterOptions`, shared
`ImageClamp` helper (also now used by `SoftThreshold`).

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Capture sharpening, presets, output-size-aware radius scaling, batch command, debug image export.

This completes spec Tasks 1-8 (the full Phase 1 output-sharpening pipeline). Remaining work is
Task 9 (initial web preset + comparison outputs) and Phase 2 empirical calibration.
