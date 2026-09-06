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

## Calibration (Phase 2)

### Metrics

Two numbers, both computed on the luminance of the 1800 px output against the resize-only variant of
the same photograph:

- **Acutance** — mean absolute horizontal/vertical gradient, expressed as a ratio to resize-only.
  How much apparent sharpness the stage added.
- **Overshoot** — mean excursion outside the pre-sharpening *local envelope*: with `hi` and `lo` the
  5x5 local maximum and minimum of the resize-only luminance, overshoot is `mean(max(L - hi, 0))` and
  undershoot `mean(max(lo - L, 0))`. A pixel pushed past what its own neighbourhood contains is a
  halo; extra contrast inside the envelope is not.

The obvious cheap proxy — a high percentile of `sharpened - resize-only` — is not usable here. It
grows with any sharpening at all, so it scores a strong sharpener as haloed and a weak one as clean,
which is the opposite of what the guards are for. An earlier pass of this calibration used it and
drew the wrong conclusion.

### Results

Over 200 randomly sampled photographs from two shoots and two bodies (Canon R6, Fujifilm X-T2), at a
1800 px long edge, against a deliberately strong reference unsharp mask (sigma 0.8, amount 0.8):

| variant | acutance (median) | overshoot | undershoot | overshoot per unit of gain | clipping increase |
|---|---|---|---|---|---|
| `web-1800-natural` | 1.19x | 0.00029 | 0.00052 | 0.00147 | +0.005% |
| `web-1800-crisp` | 1.30x | 0.00051 | 0.00094 | 0.00165 | +0.016% |
| reference USM | 1.36x | 0.00080 | 0.00089 | 0.00213 | +0.113% |

`web-1800-natural` overshoots ~31% less per unit of sharpening than the reference mask, and does so
in 79 of 80 photographs in the envelope-metric subset; it also pushes ~20x fewer pixels into
clipping. Its undershoot exceeding its overshoot is the asymmetric dark/light split (0.80/0.50)
showing up as intended: dark-side detail is carried, bright halos are held back.

The calibration is stable and body-independent: mean acutance 1.192x on the R6 (n=159) and 1.195x on
the X-T2 (n=41) despite the X-Trans sensor, with a p5-p95 spread of 1.14-1.27.

The amounts producing this are ~5x the ones the presets shipped with before the sub-pixel radius fix,
when only the medium band was actually running.

`full-natural` was calibrated separately, at full resolution against the unsharpened image rather than
a downscale: doubling its amounts reaches 1.11x acutance and remains visibly clean at 100%, which
keeps it "gentle" for print and archival exports.

### Known limitation: near-Nyquist texture

Ranking the corpus by how much of its energy sits in the band the fine radius operates on, and
measuring each preset's amplification of exactly that band, gives a clear picture: `web-1800-natural`
boosts it by a median 1.28x and `web-1800-crisp` by 1.44x, rising to 1.48x and a worst case of 1.86x
on the most texture-heavy photographs.

Where that structure is real detail — hair, knitwear, brickwork — this is the fine band doing its job,
and `natural` is a clear improvement over the downscale. Where it is *aliasing*, it is not. A finely
striped shirt at an 1800 px long edge already moirés in the resize-only variant, because the stripe
period lands near the output Nyquist limit; sharpening then amplifies the interference pattern along
with everything else, and `natural` makes visible moiré distinctly worse. `crisp` compounds it.

No stage in the pipeline can distinguish aliasing from detail after the resize — by then they are the
same signal. The fix belongs in Phase 3 (staged downscaling, which suppresses aliasing before the
sharpener ever sees it), not in the sharpening parameters. Until then this is a documented limitation:
on subjects with regular texture near the output resolution, sharpening amplifies moiré.

`web-1800-crisp` keeps its `experimental` flag for this reason. It wins the overshoot-per-sharpening
comparison against the reference mask (78 of 80 photographs), so it is not badly behaved in general —
but it is the preset most likely to turn near-Nyquist texture into an artefact, and it renders hair
with a wiry, etched edge that `natural` does not.

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Capture sharpening, output-size-aware radius scaling, batch command, debug image export.

This completes spec Tasks 1-9 (the full Phase 1 output-sharpening pipeline, presets, and
comparison tooling). Remaining work is Phase 2 (empirical calibration against a real photo
validation set) and later phases (resize research, advanced restoration, distribution).
