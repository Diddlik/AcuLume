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

## Resize research (Phase 3)

`ResizeEngine` takes two experimental axes (spec section 47), both non-default and CLI-only via
`--resize-strategy` and `--resize-space`:

- **Strategy** — `Single` (one Lanczos 3 step) or `Staged` (repeated halving, then a final step sized
  from the target dimensions so chained rounding cannot drift off the requested long edge).
- **Space** — `Gamma` (resample the encoded values) or `LinearLight` (convert to scRGB, resample,
  convert back), the physically correct averaging.

### Method

Each candidate is compared against an *ideal* downscale of the same photograph: the source luminance
is linearised, its spectrum truncated to the target size by FFT, and the result re-encoded. That is a
brick-wall low-pass, so it is alias-free by construction, and anything a candidate adds is resampling
error. The low-frequency component of that error (Gaussian sigma 3) is what reads as moire. The ground
truth is computed in linear light deliberately: evaluating a linear-light resize against a gamma-space
reference would rig the comparison, as a first pass of this measurement did.

### Results (20 photographs, 1800 px long edge)

| strategy / space | total error | low-frequency error | vs. default |
|---|---|---|---|
| `Single` / `Gamma` (default) | 0.02363 | 0.00835 | — |
| `Staged` / `Gamma` | 0.02388 | 0.00831 | -0.5% |
| `Single` / `LinearLight` | 0.02383 | 0.00820 | -1.8% |
| `Staged` / `LinearLight` | 0.02417 | 0.00820 | -1.8% |

**Neither justifies a default change, so the defaults are unchanged.**

Staged downscaling buys nothing measurable, and the reason is mechanical: libvips' `resize` already
performs an integer block shrink before the kernel reduce for large downscale factors, so the single
step is *already* staged and band-limited internally. An explicit halving chain duplicates it.

Linear light gives a consistent but tiny reduction in low-frequency error, and the difference is not
visible side by side even on the worst moire case in the corpus.

A pre-resize low-pass was simulated as well, since it is the obvious remaining lever: sigma 0.3-1.2
moved the low-frequency error by at most -0.8% while costing up to 36% of the fine-band detail. It
was not implemented.

The honest conclusion is that the downscale is not where this pipeline's remaining quality problems
live.

## Capture sharpening and deconvolution (Phase 4)

`CaptureSharpenStage` sits where the spec's pipeline diagram puts it, between decode and resize, and
works on the L channel like the output stage. It is off by default and exposed as
`--capture-sharpen off|low|normal` (spec section 21), with two engines:

- **`FineRestore`** — the spec's conservative starting point: a fine-frequency high-pass added back at
  a low amount. Reuses `FrequencyBandExtractor` rather than restating the band maths.
- **`RichardsonLucy`** — deconvolution against an assumed Gaussian PSF, iterating
  `f <- f * ((g / (f (*) h)) (*) h)`. The PSF is symmetric so its mirror is itself and the correlation
  is another blur, which makes every iteration expressible as libvips ops with no managed per-pixel
  loop. The result is blended back by the level's amount, since full deconvolution is precisely the
  aggressive restoration the spec rules out.

### Result: neither earns a place in the default path

Measured as acutance and envelope overshoot (same metrics as the Phase 2 calibration), the honest
comparison is not "does capture sharpening do something" but "does it beat simply turning the output
stage up to the same acutance".

At an 1800 px long edge (10 photographs):

| config | acutance | overshoot | overshoot per unit of gain |
|---|---|---|---|
| baseline, no capture | 1.238 | 0.00030 | 0.00128 |
| capture normal, `FineRestore` | 1.329 | 0.00048 | 0.00145 |
| capture normal, `RichardsonLucy` | 1.350 | 0.00051 | 0.00147 |
| output amounts +50% instead | 1.353 | 0.00050 | 0.00142 |

At full resolution, where no downscale throws restored detail away (4 photographs):

| config | acutance | overshoot | overshoot per unit of gain |
|---|---|---|---|
| `full-natural` | 1.199 | 0.00014 | 0.00070 |
| `full-natural` at 1.5x amounts | 1.326 | 0.00024 | 0.00074 |
| `full-natural` + capture `FineRestore` | 1.727 | 0.00068 | 0.00093 |
| `full-natural` + capture `RichardsonLucy` | 1.617 | 0.00064 | 0.00103 |
| `RichardsonLucy` alone, no output sharpening | 1.309 | 0.00028 | 0.00089 |

In both regimes the plain output stage is the most halo-efficient way to reach any given acutance,
and Richardson-Lucy is the least — at roughly nine times the processing cost (3.7 s versus 0.4 s on a
24 MP file).

Two reasons, and they are worth recording because they bound what any deconvolution engine can do
here. For the downscaled case, most of what capture sharpening restores sits above the output's
Nyquist limit and is discarded by the resize; what survives is ordinary added contrast, which the
output stage produces more cheaply and more cleanly. For deconvolution specifically, Richardson-Lucy
only beats a high-pass when the PSF is approximately right — and the input is a demosaiced,
JPEG-compressed, often already-sharpened file whose true PSF is unknown and no longer Gaussian.
Deconvolving that with a guessed kernel is not restoration, it is a nonlinear sharpener with extra
steps.

Wiener deconvolution is therefore not implemented. It is bound by the same unknown PSF that limits
Richardson-Lucy, so a second engine would fail for the identical reason. Deconvolution becomes worth
revisiting with RAW input and a measured or estimated PSF, which is out of scope here.

Both engines stay in the tree, off by default, as the spec's separate experimental restoration
engines — not folded into the stable path.

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Output-size-aware radius scaling, batch command (the GUI has one), debug image export.
- Wiener and PSF-estimating deconvolution — see the Phase 4 result above for why.

This completes spec Tasks 1-9 (the full Phase 1 output-sharpening pipeline, presets, and
comparison tooling). Remaining work is Phase 2 (empirical calibration against a real photo
validation set) and later phases (resize research, advanced restoration, distribution).
