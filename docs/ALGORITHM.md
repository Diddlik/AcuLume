# AcuLume algorithm notes

Tracks the working-space math actually implemented, per spec section 14 and 61. Updated as bands/stages are added.

## Working color space (Tasks 4-5)

Sharpening operates on the **L channel of a CIE Lab conversion** of the working image:

1. Convert sRGB → Lab (`Image.Colourspace(Enums.Interpretation.Lab)`) once per call.
2. Extract L (band 0); a/b (bands 1-2) are left untouched for the whole pipeline.
3. For each enabled band (fine, medium — spec section 15):
   - High-pass: `highPass = L - GaussianBlur(L, radius)` (radius = Gaussian sigma in px), with
     `min-ampl` 0.005 and float precision — see "Sub-pixel radii" below.
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

### Sub-pixel radii

libvips truncates the Gaussian where it drops below `min-ampl` and, at integer precision, rounds the
remaining off-centre weights. With the defaults (`min-ampl` 0.2, integer precision) every sigma below
~0.6 px collapses to a 1×1 mask, so the blur is an identity, the high-pass is exactly zero, and the
band contributes nothing — silently, at any amount. The fine band exists precisely to work at
sub-pixel radii, so `FrequencyBandExtractor` cuts the kernel at `min-ampl` 0.005 and convolves in
float. Below `BandSharpenOptions.MinimumRadius` (0.35 px) the kernel is still not reliably distinct
from an identity blur, so that is rejected rather than silently ignored.

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

## Presets and comparison outputs (Task 9)

Presets (`AcuLume.Core.Configuration.SharpenPreset`/`PresetLoader`/`PresetValidator`) are
versioned JSON, embedded into `AcuLume.Core` from the repo's top-level `presets/` directory and
also loadable from an arbitrary file path. The schema is deliberately narrower than the spec's
illustrative full example: it omits `captureSharpen` since that stage isn't implemented yet
(Phase 4) — adding an unused, speculative field now would just be dead schema to maintain.
`noiseProtection`/`edgeProtection`/`haloProtection` are single amount knobs in presets; the more
granular threshold/softness/window controls remain CLI-override-only for now.

`aculume compare` (`AcuLume.Core.Comparison.ComparisonSetBuilder`) generates the variants spec
section 43 asks for: `original` (byte-identical copy), `resize-only`, a naive per-channel
`BaselineUnsharpMask` (spec section 44 — deliberately *not* luminance-aware, to make the
pipeline's benefit visible by contrast), and the two built-in presets. `--crop label=x,y,w,h`
(repeatable) re-extracts the same fixed region from every variant via `CropGenerator` — these are
caller-supplied geometric coordinates (e.g. picked once by eye around a strong edge in a specific
test photo), not automatic content detection, since a robust detector for "shadow noise" or
"highlight detail" regions would be over-engineering for Phase 1.

## Calibration (Phase 2, first pass)

Measured on real photographs at a 1800 px long edge, as mean absolute luminance gradient relative to
the resize-only variant ("acutance"), with the 99.9th percentile of the difference from resize-only
as a halo proxy:

| variant | acutance | halo p99.9 |
|---|---|---|
| resize-only | 1.00x | 0 |
| naive USM baseline | 1.15-1.21x | 0.043-0.082 |
| `web-1800-natural` | 1.12-1.18x | 0.038-0.068 |
| `web-1800-crisp` | 1.20-1.28x | 0.050-0.093 |

`web-1800-natural` therefore lands at roughly the naive baseline's acutance while overshooting
noticeably less — which is the entire claim the pipeline has to make. The amounts that produce this
are ~5x the ones the presets shipped with before the sub-pixel radius fix, when only the medium band
was actually running. `full-natural` was calibrated separately, at full resolution against the unsharpened image rather
than against a downscale: its amounts were doubled to reach 1.11x with a 0.020 halo proxy — the
lowest overshoot of any candidate — which keeps it "gentle" as a print/archival preset while being
clearly better than no sharpening at 100%.

Verified across both sample photographs. On fine regular texture (a piqué knit) `web-1800-natural`
resolves slightly more than the naive baseline (1.41x vs 1.38x) at a lower overshoot, which is the
fine band doing its job; `web-1800-crisp` renders the same weave as a hard grid, which is one reason
it stays flagged experimental.

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Capture sharpening, output-size-aware radius scaling, batch command, debug image export.

This completes spec Tasks 1-9 (the full Phase 1 output-sharpening pipeline, presets, and
comparison tooling). Remaining work is Phase 2 (empirical calibration against a real photo
validation set) and later phases (resize research, advanced restoration, distribution).
