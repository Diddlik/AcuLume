# AcuLume – Future Enhancements

> Topaz-inspired concepts for a later AcuLume generation.
>
> This document is intentionally separate from the initial implementation requirements. The current implementation should continue unchanged; the ideas here are for later research and improvement.

**Status:** Future enhancement backlog  
**Scope:** Post-MVP / post-initial implementation  
**Project:** AcuLume

---

## 1. Purpose

The initial AcuLume pipeline already has a clear direction:

```text
decode
resize
fine detail
medium detail
noise protection
edge protection
halo limiter
light/dark split
encode
```

That implementation should be completed first.

This document captures advanced ideas inspired by modern photographic sharpening tools such as Topaz. The objective is not to reproduce proprietary Topaz algorithms or models. Instead, AcuLume should adopt useful concepts while staying:

```text
local
deterministic
scriptable
transparent
inspectable
output-aware
```

---

## 2. Core future idea: Detail Confidence

The most important future improvement is to stop asking only:

> How strong is this detail?

Instead ask:

> How confident are we that this signal is real image detail and should be sharpened?

Conceptually:

```text
Detail Confidence =
    Texture Confidence
  × Noise Confidence
  × Edge Safety
  × Frequency Weight
```

Range:

```text
0.0 = do not sharpen
1.0 = trustworthy detail
```

Then:

```text
weightedDetail = rawDetail * detailConfidence
```

This should eventually replace simple fixed thresholds.

---

## 3. Future pipeline

```text
                    IMAGE ANALYSIS
                         │
         ┌───────────────┼────────────────┐
         │               │                │
     Noise Map       Texture Map      Edge Map
         │               │                │
         └───────────────┬────────────────┘
                         │
                 Blur Classification
                         │
                         ▼
                  Fine Frequency
                         +
                 Medium Frequency
                         │
                         ▼
                 Detail Confidence
                         │
          ┌──────────────┴─────────────┐
          │                            │
      Dark Detail                 Light Detail
          │                            │
          └──────────────┬─────────────┘
                         │
                   Halo Limiter
                         │
                         ▼
               Original Recovery
                         │
                         ▼
                       OUTPUT
```

---

## 4. Noise-aware sharpening

The current implementation may use a soft threshold. A future version should estimate local noise.

Instead of:

```text
abs(detail) < threshold
→ suppress
```

use:

```text
SNR =
    abs(detail)
    /
    (localNoise + epsilon)
```

Interpretation:

```text
low SNR
→ likely noise
→ little or no sharpening

high SNR
→ likely real structure
→ sharpening allowed
```

Possible inputs:

- local variance;
- high-frequency residual;
- luminance;
- shadow level;
- local texture consistency.

Future target:

```text
noiseMap(x, y)
```

rather than one global threshold.

---

## 5. Texture confidence

AcuLume should eventually distinguish:

```text
real texture
random sensor noise
flat area
strong single edge
```

### Real texture

Examples:

- hair;
- fur;
- feathers;
- grass;
- leaves;
- fabric;
- stone;
- wood.

These should receive high detail confidence.

### Random noise

Examples:

- high-ISO shadow noise;
- chroma noise;
- JPEG artifacts.

These should receive low confidence.

### Flat areas

Examples:

- sky;
- smooth walls;
- skin regions;
- bokeh.

These usually need little sharpening.

### Strong edges

Examples:

- building against sky;
- tree against sky;
- black object against white background.

These contain structure but have high halo risk.

---

## 6. Texture coherence

Potential future methods:

```text
structure tensor
local entropy
local autocorrelation
oriented gradients
Laplacian statistics
multi-scale variance
```

Do not implement all of them at once.

The objective is to determine whether high-frequency energy is spatially coherent enough to be considered real texture.

---

## 7. Blur-type awareness

Different causes of softness should not use the same correction.

Possible classes:

```text
Sharp / normal
General softness
Lens / defocus blur
Motion blur
Strong focus error
```

---

## 8. General softness

Use normal AcuLume sharpening:

```text
fine + medium detail
noise protection
edge protection
halo limiting
light/dark weighting
```

No deconvolution required.

---

## 9. Lens / defocus blur

Lens blur is closer to:

```text
observed = original * PSF
```

than to ordinary Gaussian softness.

Future research:

```text
Wiener deconvolution
Richardson-Lucy
PSF estimation
```

Preferred CLI direction:

```bash
aculume restore image.jpg --blur lens
```

---

## 10. Motion blur

Motion blur is directional.

Potential parameters:

```text
length
angle
strength
```

Future research:

```text
estimate blur angle
estimate blur length
apply directional deconvolution
```

Potential CLI:

```bash
aculume restore image.jpg --blur motion
```

Manual form:

```bash
aculume restore image.jpg   --blur motion   --motion-angle 18   --motion-length 4.2
```

---

## 11. Separate sharpening from restoration

This should become an architectural principle.

### Sharpen

Use for:

```text
slightly soft image
resize output
increase acutance
enhance microdetail
prepare for web/display
```

CLI:

```bash
aculume sharpen image.jpg
```

### Restore

Use for:

```text
real focus error
motion blur
optical softness
deconvolution
```

CLI:

```bash
aculume restore image.jpg
```

Do not solve real focus errors by simply increasing normal sharpening strength.

---

## 12. Automatic image analysis

A future version should analyze:

```text
Noise level
Blur amount
Edge density
Texture density
Resize factor
Shadow noise
Highlight clipping
Local contrast
Motion likelihood
Defocus likelihood
```

Example:

```text
Noise       0.21
Blur        0.34
FineDetail  0.72
EdgeRisk    0.41
Scale       0.298
```

---

## 13. Auto mode

Future CLI:

```bash
aculume sharpen image.jpg --auto
```

The analysis engine should recommend:

```text
fine radius
fine amount
medium radius
medium amount
noise protection
edge protection
halo protection
light detail
dark detail
```

Example:

```text
Fine Amount       0.92
Fine Radius       0.34 px
Medium Amount     0.27
Medium Radius     0.88 px
Noise Protection  0.58
Edge Protection   0.61
Halo Protection   0.73
Dark Detail       0.79
Light Detail      0.47
```

---

## 14. Explainable auto mode

Future:

```bash
aculume sharpen image.jpg --auto --verbose
```

Example output:

```text
Detected moderate fine detail.
Detected mild sensor noise in shadows.
Detected several high-contrast edges.
Final output is 29.8% of original long edge.

Adjustments:
+ increased fine sharpening
+ increased noise protection
+ increased halo protection
- reduced light-side sharpening
```

AcuLume should remain inspectable instead of becoming a black box.

---

## 15. Original Detail Recovery

After denoise/protection, some images may become too clean.

Future feature:

```text
result =
    processed * (1 - recoveryMask)
  + original  * recoveryMask
```

Potential targets:

```text
hair
fur
fine grain
skin texture
microstructure
```

Possible control:

```text
Original Detail Recovery
0 – 100 %
```

---

## 16. Frequency-selective recovery

Prefer recovering selected original frequencies instead of simply blending the entire original image back.

Example:

```text
final =
    processed
  + originalFineDetail * recoveryAmount
```

This avoids reintroducing low-frequency blur.

---

## 17. Recovery confidence

Recovery should also be adaptive.

```text
high texture confidence
→ allow recovery

high noise probability
→ suppress recovery

flat area
→ no recovery
```

Conceptually:

```text
recoveryMask =
    textureConfidence
  × noiseSafety
```

---

## 18. Subject-aware sharpening

Later AcuLume may optionally distinguish:

```text
subject
background
sky
portrait
skin
hair
animal fur
bokeh
```

The first adaptive generation should not require AI.

Later, optional local segmentation could be added.

---

## 19. Local-only AI option

If segmentation is introduced:

```text
ONNX Runtime
local model
no cloud
```

Requirements:

```text
no upload
no telemetry
model packaged locally
optional feature
```

Possible masks:

```text
subjectMask
skyMask
skinMask
```

---

## 20. Example subject-aware behavior

### Portrait

```text
eyes/hair:
100% sharpening

skin:
30%

background:
20%

bokeh:
0%
```

### Wildlife

```text
animal:
100%

fur/feathers:
high fine detail

background:
25%

sky:
minimal
```

### Landscape

```text
foreground texture:
high

foliage:
high

sky:
very low

extreme horizon edge:
high halo protection
```

---

## 21. Content-aware presets

Potential future presets:

```text
Natural
Portrait
Wildlife
Landscape
Architecture
High ISO
Web Natural
Web Crisp
```

They should modify multiple aspects:

```text
frequency weights
noise confidence
edge protection
light/dark ratio
original recovery
subject weighting
```

---

## 22. Natural mode

Goal:

```text
increase perceived detail
without brittle texture
without obvious bright halos
without plastic reconstruction
```

Natural mode should prioritize:

```text
texture preservation
lower light-side halos
moderate edge contrast
high noise awareness
original detail recovery
```

---

## 23. High Fidelity mode

Possible future mode:

```text
High Fidelity
```

Principle:

> Do not invent structure that is not present in the image.

This mode should:

```text
avoid aggressive restoration
avoid synthetic texture generation
prefer real recorded detail
limit overshoot
preserve grain
```

This could become a core AcuLume identity.

---

## 24. Detail Confidence debug view

Future diagnostic view:

```text
View:
Detail Confidence
```

Interpretation:

```text
white  = trusted detail
gray   = uncertain
black  = do not sharpen
```

---

## 25. Future diagnostic maps

Possible outputs:

```text
noise-map.tif
texture-map.tif
edge-map.tif
detail-confidence.tif
blur-map.tif
motion-likelihood.tif
defocus-likelihood.tif
recovery-mask.tif
subject-mask.tif
```

---

## 26. Multi-scale confidence

Confidence should eventually be frequency-specific:

```text
fineConfidence(x, y)
mediumConfidence(x, y)
```

Then:

```text
fineBand   *= fineConfidence
mediumBand *= mediumConfidence
```

One region may contain strong medium structure but unreliable fine texture.

---

## 27. Adaptive Light/Dark balance

Current AcuLume may use fixed values such as:

```text
Dark  = 0.80
Light = 0.50
```

Future behavior could adapt locally.

Example:

```text
high contrast edge:
Dark  0.65
Light 0.30

fine texture:
Dark  0.85
Light 0.60
```

Bright halos should be restricted most strongly at dangerous edges.

---

## 28. Local halo risk

Possible model:

```text
haloRisk =
    edgeStrength
  × localContrast
  × sharpeningRadius
```

Then:

```text
lightAmount *= 1 - lightHaloRisk
darkAmount  *= 1 - darkHaloRisk
```

Use separate risk curves for light and dark halos.

---

## 29. Better noise estimation

Candidates:

```text
wavelet residual
median absolute deviation
high-pass robust estimator
block-based local variance
shadow-weighted noise estimator
```

Evaluate both quality and performance.

Noise estimation must remain robust around true edges.

---

## 30. Blur maps

Some images contain localized softness.

Examples:

```text
moving animal
slightly missed focus on face
depth-of-field transition
motion in one region only
```

Future target:

```text
blurMap(x, y)
```

Then apply restoration only where needed.

---

## 31. Restoration confidence

Deconvolution should only run with sufficient confidence.

```text
motionConfidence > threshold
→ directional restoration

defocusConfidence > threshold
→ lens restoration

otherwise
→ standard sharpening
```

If classification is uncertain, fall back to ordinary AcuLume sharpening.

---

## 32. Future CLI design

Potential commands:

```bash
aculume analyze image.jpg
aculume sharpen image.jpg --auto
aculume restore image.jpg --auto
```

Manual examples:

```bash
aculume restore image.jpg --blur lens
```

```bash
aculume restore image.jpg   --blur motion   --motion-angle 15   --motion-length 3.5
```

---

## 33. Analyze command

Future:

```bash
aculume analyze image.jpg
```

Possible output:

```text
Image Analysis

Resolution       6048 × 4024
Noise            Low
Blur             Mild
Motion Blur      Unlikely
Defocus Blur     Possible
Texture Density  High
Edge Risk        Medium

Suggested Mode   Sharpen
Suggested Preset Web Natural
```

Machine-readable:

```bash
aculume analyze image.jpg --json
```

Useful for:

```text
n8n
scripts
batch pipelines
GUI
```

---

## 34. Batch auto mode

Example:

```bash
aculume batch ./photos   --output ./web   --auto   --long-edge 1800
```

Each image can receive different parameters.

Optional report:

```text
filename
detected noise
detected blur
selected settings
processing time
```

---

## 35. Research architecture

Do not place experimental analysis directly into the production pipeline.

Possible future modules:

```text
AcuLume.Analysis
AcuLume.Restoration
AcuLume.Segmentation
```

Suggested structure:

```text
src/
├── AcuLume.Core/
├── AcuLume.Analysis/
├── AcuLume.Restoration/
└── AcuLume.Cli/
```

---

## 36. Suggested future interfaces

Example:

```csharp
public interface IImageAnalyzer
{
    ImageAnalysis Analyze(
        VipsImage image,
        AnalysisOptions options,
        CancellationToken cancellationToken);
}
```

Possible result:

```csharp
public sealed record ImageAnalysis
{
    public double NoiseLevel { get; init; }
    public double BlurLevel { get; init; }
    public double EdgeDensity { get; init; }
    public double TextureDensity { get; init; }
    public double MotionBlurConfidence { get; init; }
    public double DefocusConfidence { get; init; }
}
```

---

## 37. Recommended implementation order after MVP

Do not implement everything simultaneously.

### Enhancement 1 — Local adaptive noise estimation

Reason:

```text
high quality benefit
low conceptual risk
fits current pipeline
```

### Enhancement 2 — Detail Confidence v1

Use:

```text
noise confidence
+
edge safety
+
frequency strength
```

### Enhancement 3 — Texture coherence

Improve real texture vs. noise distinction.

### Enhancement 4 — Auto parameter selection

```bash
aculume sharpen image.jpg --auto
```

### Enhancement 5 — Original Detail Recovery

### Enhancement 6 — Blur classification

Separate:

```text
standard softness
lens blur
motion blur
```

### Enhancement 7 — Experimental restoration

Start with lens blur, then motion blur.

### Enhancement 8 — Optional subject segmentation

Only after the non-AI adaptive engine is mature.

---

## 38. Avoid becoming a Topaz clone

AcuLume differentiation should remain:

```text
transparent
deterministic
local
scriptable
inspectable
output-aware
photographically conservative
```

Possible guiding phrase:

> **Sharpen only what the image can justify.**

Alternative:

> **Trust detail before amplifying it.**

---

## 39. Future quality goal

Optimize for:

```text
maximum useful perceived detail
+
minimum haloing
+
minimum noise amplification
+
minimum invented texture
```

not simply:

```text
maximum edge contrast
```

---

## 40. Advanced pipeline summary

Eventually:

```text
INPUT
  │
  ▼
Image Analysis
  │
  ├── Noise Map
  ├── Texture Map
  ├── Edge Map
  ├── Blur Analysis
  └── Optional Subject Map
  │
  ▼
Output-size-aware Resize
  │
  ▼
Multi-frequency Detail Extraction
  │
  ├── Fine Detail
  └── Medium Detail
  │
  ▼
Per-band Detail Confidence
  │
  ▼
Adaptive Light / Dark Weighting
  │
  ▼
Halo Limiter
  │
  ▼
Original Detail Recovery
  │
  ▼
Optional Local Restoration
  │
  ▼
OUTPUT
```

---

## 41. Final recommendation

Do **not** interrupt the current implementation.

Complete the deterministic AcuLume sharpening engine first.

Then prioritize:

```text
1. Better local noise estimation
2. Detail Confidence
3. Texture confidence
4. Auto mode
5. Original Detail Recovery
6. Blur classification
7. Lens/motion restoration
8. Optional subject awareness
```

The most important future feature is **Detail Confidence**.

That can evolve AcuLume from:

```text
an advanced sharpening pipeline
```

into:

```text
an adaptive photographic detail-processing engine
```

while preserving the product's core advantages:

```text
local
CLI controllable
deterministic
transparent
non-cloud
non-black-box by default
```
