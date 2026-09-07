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
illustrative full example. `captureSharpen` is an optional object, so files written before it
existed still validate unchanged. `noiseProtection`/`edgeProtection`/`haloProtection` are single
amount knobs in presets; the more granular threshold/softness/window controls remain
CLI-override-only, as calibration knobs rather than everyday controls.

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

### Noise protection strength

`NoiseProtection` blends between passing detail untouched and applying the soft threshold in full:
`weight = 1 - amount * (1 - smoothstep(...))`. The amount is therefore a ceiling on how much can ever
be removed — at 0.45 more than half of below-threshold detail passes no matter where the threshold
sits. That residue was small while the band amounts were around 1.0; after the calibration above
multiplied them by five, it was five times larger, and it showed as noise in skin.

Raising the *threshold* does not help, and measuring says so: at a matched face detail of 1.13x,
thresholds of 2.5, 4 and 6 all left skin noise at 1.12x against the resize-only variant. Neither does
moving weight to the medium band (1.21x) or widening the fine radius (1.16x) — both make it worse.
Only the amount helps: 0.8 gives 1.06x and 1.0 gives 1.02x.

So every preset now runs noise protection at 1.0, with band amounts raised to restore the whole-frame
acutance each preset had before (`web-1800-natural` x1.2, `web-1800-crisp` x1.3, `full-natural`
x1.45). Measured over 24 photographs the global result is unchanged — acutance 1.205x against 1.205x,
fine-band boost 1.285x against 1.286x, overshoot per unit of gain 5% higher — while on faces skin
noise falls from 1.114x to 1.007x. Face micro-detail drops from 1.109x to 1.056x, which is the honest
cost: at an 1800 px long edge much of what sits at that scale in skin is grain rather than texture.

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

## Measured against Photoshop

Twelve photographs were exported from Photoshop at an 1800 px long edge and compared with
`web-1800-natural`. Both were measured against the same neutral reference — the ideal
spectrum-truncated downscale of the original, computed in linear light — so neither tool is
compared against the other's idea of correct.

| | AcuLume | Photoshop |
|---|---|---|
| acutance | 1.076 | 1.094 |
| overshoot per unit of gain | 0.0164 | 0.0163 |
| noise floor | 1.070 | 0.884 |
| clipping increase | +0.006% | 0.000% |

Photoshop sharpens slightly more (8 of 12 photographs) and, more consistently, delivers a noise floor
*below* the ideal downscale while ours sits above it (11 of 12). Halo behaviour is indistinguishable:
0.0164 against 0.0163 per unit of sharpening. The overshoot control this pipeline is built around is
therefore not an advantage over Photoshop — it is parity.

Splitting our own pipeline by stage explains most of the noise gap, and it is not the sharpener:

| stage | noise floor | acutance |
|---|---|---|
| our resize only | 1.041 | 0.887 |
| our resize + sharpen | 1.070 | 1.076 |
| Photoshop | 0.884 | 1.094 |

The Lanczos downscale alone already sits 4% above the ideal reference, and the sharpening adds only
2.7% on top of that. Photoshop lands 12% *below* the reference, which no resize kernel does by
itself — they are denoising as part of the export. So the gap is mostly a stage AcuLume does not have
at all, rather than a sharpener that behaves worse.

The other half of that table is worth as much: our resize-only output is 11% *softer* than the ideal
downscale, and the sharpening spends most of its budget recovering that loss before adding anything.
On two of the twelve photographs it does not fully recover it — the final result is still marginally
softer than a perfect downscale would have been unsharpened.

A caveat on scope: the accompanying full-resolution Photoshop set could not be used. Those files
differ from the originals by 0.6-0.8 levels out of 255 with an acutance ratio of 0.99-1.02, which is
a JPEG re-encode rather than a sharpening pass, so nothing can be concluded from them.

## Noise reduction (scope extension)

The Photoshop comparison showed the gap was a stage AcuLume did not have, so `DenoiseStage` adds
one, between the resize and the sharpening: the noise that matters is the noise the sharpener is
about to amplify. Off by default — the specification asks only that sharpening not amplify noise.

Thresholds are multiples of the sigma `NoiseEstimator` measures from the image, never absolute
constants. Three engines, all composed from libvips operations so the thread pool is used and no
managed per-pixel loop exists:

- **`GuidedFilter`** — a local linear model built from box means; cost independent of radius.
- **`MultiScaleShrinkage`** — soft thresholding across three detail octaves.
- **`NonLocalMeans`** — accumulated over a fixed set of shifts rather than a per-pixel patch search,
  which is what makes it expressible here at all.

### Results (12 photographs, neutral FFT reference)

| config | acutance | noise floor | overshoot per gain | 1800 px | 18 MP |
|---|---|---|---|---|---|
| no denoise | 1.076 | 1.070 | 40.3 | 0.54 s | 1.9 s |
| `GuidedFilter` | 1.053 | 0.919 | 55.9 | 0.85 s | 1.3 s |
| `MultiScaleShrinkage` | 0.940 | 0.786 | — | 0.89 s | 2.3 s |
| `NonLocalMeans` | 1.050 | 0.896 | 59.9 | 4.6 s | 39.7 s |
| Photoshop | 1.094 | 0.884 | 30.7 | | |

**Non-local means did not earn its cost.** It ties the guided filter — marginally less noise,
marginally less acutance — at five times the price at 1800 px and thirty times at full resolution.
The reason was predictable and was predicted: patch weighting assumes white noise, and after a 3x
downscale the residual noise is spatially correlated, so the mechanism its advantage comes from is
the one the input breaks. The same shape of failure as Richardson-Lucy in Phase 4. BM3D was not
attempted: its block matching is data-dependent grouping that no composition of libvips operations
expresses, so it would need a second native dependency.

Multi-scale shrinkage is too aggressive at these settings — it drops below an ideal downscale.

### Denoising pays for stronger sharpening

Removing grain before the sharpener means there is far less of it to amplify, so raising the band
amounts costs almost nothing on the noise axis:

| config | acutance | noise floor | overshoot per gain |
|---|---|---|---|
| guided filter, amounts x1.0 | 1.053 | 0.919 | 55.9 |
| x1.3 | 1.107 | 0.922 | 30.5 |
| x1.6 | 1.161 | 0.926 | 22.7 |
| x2.0 | 1.230 | 0.931 | 18.3 |
| threshold 2.0, x1.6 | **1.143** | **0.876** | **24.8** |
| Photoshop | 1.094 | 0.884 | 30.7 |

Guided filter at threshold 2.0 with amounts at 1.6x beats Photoshop on all three measures, and looks
better on faces, stonework and brickwork side by side.

It also makes the known near-Nyquist limitation worse: fine-band boost rises from 1.386x to 1.560x
on the striped shirt, and the moire is visibly harder. That trade — better everywhere except on
subjects that already moire — was taken deliberately.

### The shipped presets

All three now run the guided filter at full strength with a 2.0 sigma threshold. The multiplier on
each preset's band amounts was measured separately rather than copied across, because the presets
occupy different regimes: `crisp` was already the strongest and carries the moire risk, and
`full-natural` works without a downscale, where the measured sigma is lower.

| preset | fine | medium | acutance | noise floor | fine boost |
|---|---|---|---|---|---|
| `web-1800-natural` | 9.60 | 2.88 | 1.292 | 0.840 | 1.364x |
| `web-1800-crisp` | 11.40 | 3.40 | 1.394 | 0.844 | 1.495x |
| Photoshop | | | 1.240 | 0.861 | 1.344x |
| `full-natural` (full resolution) | 1.90 | 0.59 | 1.160 | 0.812 | 1.199x |

Measured against the resize-only variant over the same twelve photographs, so the rows compare
directly. `web-1800-natural` is now ahead of Photoshop on both sharpening and noise, at a fine-band
boost only slightly higher.

`web-1800-crisp` needed the larger multiplier for a reason worth recording: at 1.0x it produced
1.307 acutance against the new natural's 1.300 — the two presets would have been indistinguishable
and crisp would have had no purpose. At 1.3x the gap is real again. It keeps its `experimental` flag,
now for a sharper reason than before: its fine-band boost of 1.495x is the highest of the family and
that band is what turns near-Nyquist texture into an artefact.

## Open: the halo limiter barely engages

Building the help illustrations produced a measurement worth acting on. Rendering the preset with
`haloProtection` at 0 and at its calibrated value and taking the strongest 150 px window across seven
photographs, the difference is **0.14 out of 255** — the stage is effectively inert in normal use.

The reason is its limits, not its strength: `DarkLimit` 0.5 and `LightLimit` 0.3 cap overshoot at half
and a third of the local contrast range, and the band contribution never reaches that. Tightening them
to 0.1 and 0.05 makes it visible (4.68/255 on the same crop).

So either the default limits are far too loose to ever bite, or the stage is in the wrong place in the
pipeline to see the overshoot it is meant to cap. Both are testable with the method used for the noise
protection analysis: sweep the limits, measure envelope overshoot and acutance across the corpus, and
find out whether a tighter limiter buys anything the edge protection does not already provide.

## Not yet implemented

- Optional coarse band (spec 15.3 — architecture already supports adding one).
- Output-size-aware radius scaling, batch command (the GUI has one), debug image export.
- Wiener and PSF-estimating deconvolution — see the Phase 4 result above for why.

Spec phases 1-4 are done: the output-sharpening pipeline, presets and comparison tooling; the
calibration above; the resize research; and capture sharpening with a deconvolution engine. Phase 5
(self-contained Windows x64 and Linux x64 builds) has not been started.

One correctness item outlives them: `Colourspace(Lab)` assumes sRGB primaries and transfer, so for an
AdobeRGB or Display-P3 input the luminance the bands operate on is slightly wrong. The error largely
cancels because the round trip makes the same assumption in both directions, but converting into a
defined working space after load and back before write would change exported pixels and require
recalibrating the presets.
