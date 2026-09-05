# AcuLume

> **Adaptive, output-aware image sharpening for photographers — local, deterministic, scriptable.**

**Document type:** Implementation requirements / agent specification  
**Target:** Coding agent (Codex / Claude Code / equivalent)  
**Status:** Initial implementation specification  
**Date:** 2026-09-05

---

## 1. Agent directive

Implement **AcuLume** as a local command-line image sharpening tool.

The tool must not depend on Photoshop, Lightroom, a cloud service, an external API, a browser, or a GUI. All image processing must happen locally.

The primary goal is not to clone one existing Photoshop filter. The goal is to build a technically clean, testable, high-quality sharpening pipeline inspired by proven photographic sharpening concepts and then tune it empirically.

The implementation must support two main use cases:

1. **Large/full-resolution photographs** — e.g. 24–60 MP camera images.
2. **Web output** — especially images resized to around **1800 px** on the long edge.

The algorithm must be **output-size aware**. A full-resolution master and a 1800 px web image must not simply receive the same sharpening parameters.

Implementation priorities:

1. image quality;
2. artifact/halo control;
3. deterministic output;
4. correct color and metadata handling;
5. memory efficiency for large photographs;
6. CLI usability and automation;
7. performance.

Do not prematurely optimize at the expense of image quality or testability.

---

# 2. Project name

## AcuLume

Meaning:

- **Acu** → *acutance*: perceived sharpness created by local edge contrast.
- **Lume** → *luminance*: the sharpening pipeline should primarily operate on luminance/detail information instead of blindly sharpening RGB channels independently.

Suggested executable:

```text
aculume
```

Suggested repository name:

```text
aculume
```

Suggested namespaces:

```text
AcuLume.Core
AcuLume.Cli
AcuLume.Tests
AcuLume.Benchmarks
```

A web/GitHub-oriented name check performed on 2026-09-05 did not reveal a widely used image-sharpening application called AcuLume. This is only a practical naming check, **not a legal trademark clearance**.

---

# 3. Problem statement

Simple sharpening typically applies one filter with parameters such as:

```text
amount
radius
threshold
```

That is insufficient for the desired quality.

A high-quality photographic sharpening system should distinguish between:

- true detail and noise;
- fine and medium spatial frequencies;
- bright and dark sharpening halos;
- ordinary texture and very strong edges;
- full-resolution/capture sharpening and final output sharpening;
- sharpening before resize and sharpening after resize.

AcuLume should therefore implement a configurable multi-stage pipeline.

---

# 4. Research background

The following techniques are **inspirations**, not requirements to reproduce proprietary algorithms pixel-for-pixel.

## 4.1 Photoshop Lighten/Darken Smart Sharpen workflow

A previously used Photoshop action applied approximately:

```text
Smart Sharpen
Amount: 500 %
Radius: 0.2 px
Remove: Lens Blur

Darken blend:
Opacity: 80 %

Lighten blend:
Opacity: 50 %
```

The useful conceptual insight is **asymmetric sharpening**:

- negative/dark sharpening components can be stronger;
- positive/light sharpening components can be weaker;
- reducing bright halos often produces a more natural-looking result.

AcuLume must therefore support independent dark and light detail strength.

Conceptually:

```text
detail = sharpened - original

if detail < 0:
    detail *= darkAmount
else:
    detail *= lightAmount
```

The Photoshop action itself must **not** be treated as the complete AcuLume algorithm.

---

## 4.2 Web Sharpener research

Andreas Resch's Web Sharpener is useful research because it combines ideas such as:

- resizing in multiple stages;
- sharpening during/after resizing;
- separate detail/basic sharpening;
- Lighten/Darken treatment;
- edge masking;
- output-oriented sharpening.

AcuLume must **not** assume that staged resize is automatically superior.

Instead implement:

```text
Direct high-quality resize
```

as the initial default and later add:

```text
Staged resize
```

as an optional experimental strategy so both can be compared objectively and visually.

---

## 4.3 General photographic principle

Sharpening should be considered in stages:

```text
input
  ↓
capture / restoration sharpening
  ↓
master image
  ↓
resize / output preparation
  ↓
output sharpening
  ↓
encoded output
```

AcuLume should make these stages explicit in the architecture.

---

# 5. Non-goals for the first implementation

Do **not** implement the following in Phase 1 unless necessary for the core architecture:

- GUI;
- cloud processing;
- AI/ML sharpening;
- neural super-resolution;
- RAW demosaicing;
- Lightroom catalog integration;
- Photoshop plugin;
- mobile app;
- REST service;
- Docker service;
- automatic semantic detection of faces/sky/animals;
- GPU/CUDA processing;
- exact reverse engineering of Photoshop Smart Sharpen;
- exact reverse engineering of Adobe Lens Blur;
- full photo editor functionality.

These may be considered later.

---

# 6. Technology choice

## 6.1 Runtime

Use:

```text
C# 14
.NET 10 LTS
```

Target framework:

```xml
<TargetFramework>net10.0</TargetFramework>
```

At the time this specification was created, .NET 10 is an active LTS release supported until November 2028.

---

## 6.2 Image engine

Use:

```text
NetVips
libvips
```

Initial package baseline:

```text
NetVips 3.2.x
NetVips.Native / platform packages based on libvips 8.18.x
```

Do not hard-code this document's exact patch versions forever. During implementation use the latest stable compatible patch versions and lock them in dependency management.

Why libvips/NetVips:

- efficient processing of very large images;
- demand-driven processing model;
- low memory overhead compared with naïve full-frame intermediate buffers;
- high-quality resize operations;
- convolution/blur/arithmetic operations;
- broad image-format support;
- ICC/color-management support depending on build;
- cross-platform;
- suitable for batch CLI processing.

Prefer composing image operations through NetVips instead of copying full images into C# arrays for each step.

---

## 6.3 CLI

Use:

```text
System.CommandLine 2.x stable
```

Do not use a 3.x preview dependency for the initial stable implementation unless there is a specific documented requirement that cannot be solved with 2.x.

---

## 6.4 Configuration

Use:

```text
System.Text.Json
```

Presets must be serializable JSON.

---

## 6.5 Testing

Use:

```text
xUnit
```

Also create tooling/tests for:

- golden/reference outputs;
- numerical comparisons;
- optional SSIM/PSNR reporting;
- benchmark timing;
- memory observations.

Do not use PSNR/SSIM as the sole image-quality criterion. Sharpening intentionally modifies pixels, so visual comparison remains essential.

---

# 7. Solution structure

Create:

```text
aculume/
│
├── AcuLume.slnx
│
├── README.md
├── LICENSE
├── THIRD_PARTY_NOTICES.md
├── Directory.Build.props
├── Directory.Packages.props
│
├── src/
│   ├── AcuLume.Core/
│   │   ├── AcuLume.Core.csproj
│   │   │
│   │   ├── Imaging/
│   │   │   ├── ImageLoader.cs
│   │   │   ├── ImageWriter.cs
│   │   │   ├── ImageMetadata.cs
│   │   │   ├── ColorManagement.cs
│   │   │   └── ResizeEngine.cs
│   │   │
│   │   ├── Sharpening/
│   │   │   ├── SharpenPipeline.cs
│   │   │   ├── CaptureSharpenStage.cs
│   │   │   ├── OutputSharpenStage.cs
│   │   │   ├── FrequencyBandExtractor.cs
│   │   │   ├── AsymmetricDetailMixer.cs
│   │   │   ├── EdgeMaskBuilder.cs
│   │   │   ├── NoiseProtection.cs
│   │   │   └── HaloLimiter.cs
│   │   │
│   │   ├── Configuration/
│   │   │   ├── SharpenPreset.cs
│   │   │   ├── PresetLoader.cs
│   │   │   └── PresetValidator.cs
│   │   │
│   │   └── Diagnostics/
│   │       ├── ProcessingReport.cs
│   │       └── DebugImageWriter.cs
│   │
│   └── AcuLume.Cli/
│       ├── AcuLume.Cli.csproj
│       ├── Program.cs
│       ├── Commands/
│       │   ├── SharpenCommand.cs
│       │   ├── BatchCommand.cs
│       │   ├── PresetCommand.cs
│       │   └── InfoCommand.cs
│       └── Console/
│           └── ProgressReporter.cs
│
├── tests/
│   ├── AcuLume.Core.Tests/
│   ├── AcuLume.Cli.Tests/
│   └── TestImages/
│
├── benchmarks/
│   └── AcuLume.Benchmarks/
│
├── presets/
│   ├── full-natural.json
│   ├── web-1800-natural.json
│   ├── web-1800-crisp.json
│   └── neutral.json
│
└── docs/
    ├── ALGORITHM.md
    ├── COLOR_MANAGEMENT.md
    ├── BENCHMARKING.md
    └── RESEARCH.md
```

The exact file split may change as implementation evolves, but **Core and CLI must remain separate**.

---

# 8. Architectural requirements

## 8.1 Core must not depend on CLI

`AcuLume.Core` must expose reusable APIs.

Example direction:

```csharp
public sealed class AcuLumeProcessor
{
    public ProcessingResult Process(
        string inputPath,
        string outputPath,
        ProcessingOptions options,
        CancellationToken cancellationToken = default);
}
```

The CLI is only an adapter around the core.

This enables later integration into:

- GUI;
- n8n;
- folder watcher;
- REST service;
- another .NET program;
- Windows Explorer context command.

---

## 8.2 Processing must be deterministic

For identical:

- input bytes;
- preset;
- AcuLume version;
- libvips version;
- platform where implementation behavior is defined equivalent;

the output processing must be deterministic within the limitations of the encoder.

Avoid random adaptive behavior.

---

# 9. Image-processing precision

Internally, sharpening calculations should use floating-point data where practical.

Preferred conceptual pipeline:

```text
encoded image
   ↓
decode
   ↓
ICC / color interpretation
   ↓
working representation
   ↓
float processing
   ↓
luminance/detail processing
   ↓
recombine color
   ↓
output color conversion
   ↓
encode
```

Avoid repeated 8-bit quantization between intermediate stages.

---

# 10. Color-management requirements

Color management is part of image quality and must not be ignored.

## Phase 1 behavior

At minimum:

1. detect embedded ICC profile if available;
2. preserve embedded ICC metadata when output color space is unchanged;
3. provide an explicit output mode for web-oriented sRGB;
4. avoid silently interpreting arbitrary RGB values as sRGB if metadata says otherwise;
5. document behavior when no ICC profile exists.

Preferred web behavior:

```text
input ICC
   ↓
working conversion as required
   ↓
processing
   ↓
convert to sRGB
   ↓
embed sRGB profile
```

Do not strip color profiles by accident.

---

# 11. Metadata requirements

By default preserve useful photographic metadata where technically safe:

- EXIF;
- capture date;
- camera model;
- lens information;
- orientation after normalization;
- copyright/artist;
- ICC profile.

Provide:

```text
--strip-metadata
```

to intentionally remove optional metadata.

GPS preservation should initially follow the general metadata policy, but document it clearly because users may intentionally want metadata removed for web export.

---

# 12. Orientation

EXIF orientation must be handled correctly.

Output pixels should be physically oriented correctly.

Avoid producing a visually rotated image because EXIF orientation was dropped after decode.

---

# 13. Core sharpening philosophy

AcuLume's main algorithm is:

## Adaptive Multi-Frequency Output-Aware Sharpening

High-level design:

```text
INPUT
  │
  ▼
Decode + orientation + ICC
  │
  ▼
Float working image
  │
  ▼
Optional Capture Sharpen
  │
  ▼
Optional Resize
  │
  ▼
Luminance/detail extraction
  │
  ├──────── Fine Frequency
  │
  ├──────── Medium Frequency
  │
  └──────── Optional Coarse Frequency (future/default off)
  │
  ▼
Noise protection
  │
  ▼
Edge protection
  │
  ▼
Halo limiting
  │
  ▼
Dark / Light asymmetric weighting
  │
  ▼
Recombine with original color
  │
  ▼
Output ICC / quantization
  │
  ▼
Encode
```

---

# 14. Luminance-oriented sharpening

Avoid naïvely sharpening each RGB channel independently.

The initial implementation should construct a luminance/detail representation.

The exact working color model should be selected based on what can be implemented safely with NetVips.

Possible implementations include:

```text
linear RGB → luminance
```

or a well-defined luminance channel from a suitable color space.

Important requirement:

**Sharpening must not introduce obvious chromatic edge halos.**

Keep original chroma as stable as practical.

Document the final chosen working-space math in `docs/ALGORITHM.md`.

---

# 15. Frequency-band sharpening

Implement at least two useful detail scales:

## 15.1 Fine band

Purpose:

- hair;
- grass;
- feather detail;
- fabric;
- small texture;
- fine architectural texture;
- output crispness.

Initial radius ranges:

### Full resolution

```text
0.45 – 0.75 px
```

### Web around 1800 px

```text
0.25 – 0.45 px
```

These are starting ranges for experimentation, not hard algorithmic limits.

---

## 15.2 Medium band

Purpose:

- local structural clarity;
- leaves;
- eyes;
- small building edges;
- rock texture;
- moderately sized detail.

Initial radius ranges:

### Full resolution

```text
1.0 – 1.8 px
```

### Web around 1800 px

```text
0.7 – 1.2 px
```

The medium component should generally be weaker than the fine component.

---

## 15.3 Optional coarse band

Do not make this active by default in Phase 1.

Potential later use:

```text
2 – 4 px
```

This is closer to local contrast/microcontrast than conventional sharpening.

Keep architecture extensible so a third band can be added without redesigning the pipeline.

---

# 16. Initial frequency extraction approach

Start with a robust Gaussian/high-pass style decomposition.

Conceptually:

```text
blurFine   = GaussianBlur(luma, fineRadius)
fineDetail = luma - blurFine
```

A more isolated band may use difference of scales:

```text
fineBand =
    GaussianBlur(luma, r1)
    -
    GaussianBlur(luma, r2)
```

and similarly for medium frequencies.

The first implementation should favor predictable behavior and testability over mathematical complexity.

Document the exact decomposition chosen.

---

# 17. Asymmetric Light/Darken sharpening

This is a **required distinguishing feature**.

For detail value `d`:

```text
if d < 0:
    weighted = d * darkAmount
else:
    weighted = d * lightAmount
```

Initial default relationship:

```text
darkAmount > lightAmount
```

Starting reference inspired by successful prior workflow:

```text
darkAmount  = 0.80
lightAmount = 0.50
```

However, these values must be tunable independently per frequency band if necessary.

Preferred configuration model:

```json
{
  "fine": {
    "darkAmount": 0.80,
    "lightAmount": 0.50
  },
  "medium": {
    "darkAmount": 0.35,
    "lightAmount": 0.20
  }
}
```

---

# 18. Noise protection

Sharpening must not blindly amplify small random variations.

Implement an initial noise/detail threshold mechanism.

Baseline concept:

```text
abs(detail) <= threshold
    → suppress or strongly reduce sharpening

abs(detail) > threshold
    → allow sharpening
```

Do **not** implement a harsh binary cutoff if it causes visible discontinuities.

Prefer a smooth transition such as:

```text
smoothstep
sigmoid-like curve
soft threshold
```

Example conceptual function:

```text
weight = smoothstep(noiseLow, noiseHigh, abs(detail))
```

Then:

```text
detail *= weight
```

Future versions may estimate local noise adaptively.

Phase 1 may use configurable thresholds.

---

# 19. Edge protection

Strong edges such as:

```text
dark building | bright sky
tree trunk    | bright sky
black text    | white background
```

are common locations for ugly sharpening halos.

Implement an edge-strength map using a robust gradient operator, for example:

```text
Scharr
```

or:

```text
Sobel
```

Prefer Scharr if practical.

Concept:

```text
edgeStrength = gradient(blurredLuma)
```

Map it to a protection mask.

The user must be able to control:

```text
edge protection amount
edge threshold
edge softness
edge detection blur
```

Do not completely suppress real detail around all edges. The mask must attenuate extreme edges progressively.

---

# 20. Halo limiter

Edge protection and halo limiting are related but should remain conceptually separate.

The halo limiter should restrict excessive overshoot/undershoot generated by sharpening.

The first implementation may use a local contrast-aware cap.

Possible conceptual strategy:

```text
localRange = localMax - localMin

maxAllowedSharpenDelta =
    haloLimit * localRange
```

Then clamp sharpening detail contribution:

```text
-detailLimit <= contribution <= +detailLimit
```

Ideally support separate limits for:

```text
dark halo
light halo
```

because bright halos are often visually more objectionable.

Do not silently clip the final image as the primary halo-management technique.

---

# 21. Capture sharpening

Capture sharpening means light restoration before final output sizing.

It must be:

- subtle;
- optional;
- conservative;
- suitable for large images.

Initial command behavior should allow:

```text
--capture-sharpen off
--capture-sharpen low
--capture-sharpen normal
```

Do **not** attempt aggressive deconvolution in Phase 1.

Start with a conservative fine-frequency restoration approach.

Later experimentation may add:

```text
Wiener deconvolution
Richardson-Lucy
PSF-based restoration
```

as separate engines.

---

# 22. Resize strategy

AcuLume must treat resizing as a first-class stage.

## 22.1 Default strategy

Start with:

```text
single high-quality resize to final output dimensions
```

Use a high-quality photographic resampling kernel available in libvips.

Initial default candidate:

```text
Lanczos3 / Lanczos
```

The exact libvips kernel name must be validated in implementation.

---

## 22.2 Linear-light resize experiment

Implement architecture so resize can be tested in:

```text
encoded/gamma-oriented RGB
```

versus:

```text
linear-light RGB
```

Do not assume either wins for every photographic output.

Expose as a preset/config option when stable:

```text
resize.linearLight
```

Initial default can remain the implementation that produces the most reliable color/edge behavior after test images are evaluated.

---

## 22.3 Staged resize experiment

Later add optional:

```text
resize.strategy = staged
```

with configurable reduction factor, initially around:

```text
1.66
```

Example:

```text
6000
 ↓
3614
 ↓
2177
 ↓
1800
```

Allow optional light intermediate sharpening.

This exists for objective A/B comparison with Web-Sharpener-style concepts.

It is **not Phase 1 default behavior**.

---

# 23. Output sharpening

Final output sharpening must happen **after the final resize**.

Example:

```text
6000 × 4000 original
       ↓
resize
       ↓
1800 × 1200
       ↓
WEB OUTPUT SHARPENING
```

This is mandatory for `web` presets.

Do not sharpen at 6000 px using exactly the same radii and then simply downscale.

---

# 24. Initial presets

The values below are **starting points** and must be considered experimental.

## 24.1 `full-natural`

Use for full-resolution output.

```json
{
  "name": "full-natural",
  "captureSharpen": {
    "enabled": true,
    "amount": 0.35
  },
  "resize": {
    "enabled": false
  },
  "outputSharpen": {
    "fine": {
      "radius": 0.60,
      "amount": 0.50,
      "darkAmount": 0.80,
      "lightAmount": 0.60
    },
    "medium": {
      "radius": 1.40,
      "amount": 0.15,
      "darkAmount": 0.75,
      "lightAmount": 0.55
    },
    "noiseProtection": 0.50,
    "edgeProtection": 0.40,
    "haloProtection": 0.50
  }
}
```

---

## 24.2 `web-1800-natural`

```json
{
  "name": "web-1800-natural",
  "resize": {
    "enabled": true,
    "longEdge": 1800,
    "strategy": "single",
    "kernel": "lanczos3"
  },
  "outputSharpen": {
    "fine": {
      "radius": 0.35,
      "amount": 1.00,
      "darkAmount": 0.80,
      "lightAmount": 0.50
    },
    "medium": {
      "radius": 0.90,
      "amount": 0.30,
      "darkAmount": 0.80,
      "lightAmount": 0.50
    },
    "noiseProtection": 0.45,
    "edgeProtection": 0.65,
    "haloProtection": 0.70
  }
}
```

---

## 24.3 `web-1800-crisp`

More aggressive but still halo-controlled.

Do not implement by merely multiplying all strengths uniformly.

Favor:

- slightly stronger fine detail;
- moderate medium detail;
- stronger halo protection;
- still weaker light halos.

The agent should create the initial preset using reasonable values and mark it experimental.

---

# 25. Output-size awareness

Long-term, AcuLume should scale radii/strength intelligently based on final output dimensions.

Phase 1 may rely on presets.

However, architecture must support:

```text
final width
final height
long edge
scale factor
```

as algorithm context.

Do not bury `1800` as a hard-coded magic number inside the sharpening implementation.

---

# 26. CLI design

Executable:

```bash
aculume
```

CLI should be usable interactively and from scripts.

---

## 26.1 Single image

```bash
aculume sharpen input.jpg
```

Default:

```text
preset: full-natural
output: input.aculume.jpg
```

Exact output naming may be refined, but source files must never be overwritten by default.

---

## 26.2 Web image

```bash
aculume sharpen input.jpg --preset web-1800-natural
```

or:

```bash
aculume sharpen input.jpg --long-edge 1800 --preset web-natural
```

The second syntax can be introduced if presets become size-independent.

---

## 26.3 Explicit output

```bash
aculume sharpen input.jpg \
  --preset web-1800-natural \
  --output output.jpg
```

---

## 26.4 Output quality

```bash
aculume sharpen input.jpg \
  --preset web-1800-natural \
  --quality 90
```

---

## 26.5 Format conversion

Example:

```bash
aculume sharpen input.tif \
  --preset web-1800-natural \
  --format avif \
  --quality 85 \
  --output output.avif
```

Only expose formats verified to work correctly with the packaged libvips build.

---

## 26.6 Batch processing

Required:

```bash
aculume batch ./photos \
  --output ./out \
  --preset web-1800-natural
```

Options:

```text
--recursive
--pattern
--parallel
--overwrite
--skip-existing
```

`--overwrite` must be explicit.

---

## 26.7 Manual parameter overrides

Required for research/tuning:

```bash
aculume sharpen input.jpg \
  --preset web-1800-natural \
  --fine-radius 0.35 \
  --fine-amount 1.10 \
  --medium-radius 0.90 \
  --medium-amount 0.30 \
  --darken 0.80 \
  --lighten 0.50 \
  --edge-protection 0.65 \
  --halo-protection 0.70 \
  --noise-protection 0.45
```

CLI overrides take precedence over preset values.

Do not require recompilation for algorithm tuning.

---

# 27. CLI commands

Initial command set:

```text
aculume sharpen
aculume batch
aculume info
aculume preset list
aculume preset show
aculume preset validate
aculume version
```

Example:

```bash
aculume info image.jpg
```

Should report useful fields:

```text
Path
Dimensions
Format
Bit depth / pixel format if known
ICC profile
EXIF orientation
Alpha
Metadata summary
```

---

# 28. CLI exit codes

Use stable exit codes suitable for automation.

Initial proposal:

```text
0  success
1  general processing error
2  invalid command/options
3  input not found / inaccessible
4  unsupported format
5  invalid preset
6  output conflict
7  cancelled
```

Document these in README.

---

# 29. Logging and console behavior

Default console output should be concise.

Example:

```text
AcuLume 0.1.0

Input      DSC_1234.jpg
Size       6048 × 4024
Preset     web-1800-natural
Output     DSC_1234.aculume.jpg
Resize     6048 × 4024 → 1800 × 1198
Time       0.84 s
```

Support:

```text
--quiet
--verbose
```

Do not write noisy debug logs by default.

---

# 30. Diagnostic image outputs

For algorithm development implement optional debug exports.

Example:

```bash
aculume sharpen input.jpg \
  --preset web-1800-natural \
  --debug-dir ./debug
```

Possible debug files:

```text
01-luminance.tif
02-fine-detail.tif
03-medium-detail.tif
04-edge-mask.tif
05-noise-mask.tif
06-halo-mask.tif
07-before-output-sharpen.tif
08-final.tif
```

These are extremely important for understanding why an image looks wrong.

Debug mode may be slower and use more storage.

---

# 31. Preset schema

Create strongly typed records/classes.

Example direction:

```csharp
public sealed record SharpenPreset
{
    public required string Name { get; init; }
    public CaptureSharpenOptions CaptureSharpen { get; init; } = new();
    public ResizeOptions Resize { get; init; } = new();
    public OutputSharpenOptions OutputSharpen { get; init; } = new();
}
```

Validation must reject:

- negative radii;
- impossible dimensions;
- invalid strengths;
- unsupported resize strategy;
- invalid output quality;
- nonsensical thresholds.

Do not silently clamp malformed preset files unless documented explicitly.

---

# 32. Preset file example

```json
{
  "version": 1,
  "name": "web-1800-natural",
  "resize": {
    "enabled": true,
    "longEdge": 1800,
    "allowUpscale": false,
    "strategy": "single",
    "kernel": "lanczos3",
    "linearLight": false
  },
  "captureSharpen": {
    "enabled": false
  },
  "outputSharpen": {
    "enabled": true,
    "fine": {
      "radius": 0.35,
      "amount": 1.0,
      "darkAmount": 0.8,
      "lightAmount": 0.5
    },
    "medium": {
      "radius": 0.9,
      "amount": 0.3,
      "darkAmount": 0.8,
      "lightAmount": 0.5
    },
    "noise": {
      "enabled": true,
      "amount": 0.45
    },
    "edges": {
      "enabled": true,
      "amount": 0.65
    },
    "halos": {
      "enabled": true,
      "amount": 0.70
    }
  },
  "output": {
    "colorSpace": "sRGB",
    "quality": 90,
    "preserveMetadata": true
  }
}
```

Schema versioning is required from the beginning.

---

# 33. Supported inputs

Phase 1 must prioritize:

```text
JPEG
PNG
TIFF
```

If packaged libvips reliably supports:

```text
WebP
AVIF
HEIC/HEIF
```

they may also be exposed, but do not claim support without integration tests.

RAW camera files are not part of Phase 1.

Users should feed AcuLume already-demosaiced TIFF/JPEG/etc. initially.

---

# 34. Supported outputs

Phase 1 required:

```text
JPEG
PNG
TIFF
```

Desirable when supported and tested:

```text
WebP
AVIF
```

JPEG is the first-class web output for the initial release.

---

# 35. Alpha channel

If input contains alpha:

- preserve alpha when output format supports it;
- sharpening should not create nonsensical RGB fringes around transparent edges;
- avoid applying sharpening across fully transparent boundaries without consideration.

JPEG output must flatten or reject alpha according to explicit documented behavior.

Do not silently choose a background color without documenting it.

---

# 36. Performance requirements

Target machine class includes modern Windows desktop systems processing 24–60 MP photos.

Requirements:

- avoid unnecessary full-image copies;
- prefer libvips pipeline operations;
- avoid per-pixel managed C# loops for the main image path;
- batch processing should permit controlled parallelism;
- do not spawn unbounded workers;
- cancellation should work between files and, where feasible, inside long-running stages.

Initial goal:

```text
A typical 24 MP JPEG → 1800 px JPEG
should feel interactive on a modern desktop CPU.
```

Do not define a fake hard millisecond requirement before benchmarks exist.

---

# 37. Memory requirements

The implementation must not scale memory use linearly with the number of unnecessary intermediate full-size image copies.

Avoid this pattern:

```text
original full buffer
+ blur 1 full buffer
+ blur 2 full buffer
+ edge map full buffer
+ several managed copies
+ ...
```

Use libvips lazy/demand-driven operations as intended.

Add benchmark documentation showing:

```text
input dimensions
processing preset
wall time
peak process memory if measurable
output dimensions
```

---

# 38. Batch parallelism

Batch mode should initially default conservatively.

Example:

```text
--parallel auto
```

where `auto` chooses a sensible number based on processor count and memory.

Remember libvips itself may use concurrency internally.

Do not multiply:

```text
many parallel images × many internal threads
```

until the machine is oversubscribed.

Make concurrency configurable.

---

# 39. Error handling

Errors must identify:

```text
file
processing stage
reason
```

Batch mode should support continuing after one bad file unless configured otherwise.

Example:

```text
[17/120] ERROR corrupt.jpg: decode failed: ...
[18/120] OK DSC_0182.jpg
```

End with summary:

```text
Processed: 120
Succeeded: 119
Failed: 1
Skipped: 0
```

---

# 40. Source preservation

**Never overwrite originals by default.**

If user explicitly passes:

```text
--overwrite
```

only allow overwriting files in the output target, and handle direct source replacement cautiously.

Preferred design: prevent source-path == output-path unless an even more explicit future option is introduced.

---

# 41. Testing strategy

## 41.1 Unit tests

Test pure math/configuration where possible:

- preset parsing;
- preset validation;
- radius validation;
- light/dark split;
- soft threshold function;
- output-size calculations;
- long-edge resize calculations;
- CLI precedence;
- metadata policy.

---

## 41.2 Synthetic image tests

Generate controlled test images:

### Flat field

Expected:

```text
no added structure
```

### Single black/white edge

Measure:

```text
overshoot
undershoot
halo width
```

### Sinusoidal patterns

Use several spatial frequencies.

### Fine checkerboard

Detect ringing/moiré.

### Noise field

Verify noise protection.

### Colored edge

Verify no strong chromatic halo.

These tests are more useful for algorithm correctness than arbitrary photos alone.

---

# 42. Real photographic validation set

Build a local test set with at least these categories:

```text
1. Landscape with grass/foliage
2. Trees against bright sky
3. Architecture
4. Portrait / skin
5. Hair / fur
6. Night/high-ISO image
7. Snow / bright detail
8. Dark fine detail
9. Water
10. Fine repeating patterns / fabric / roof tiles
```

Prefer 20+ representative images over tuning to one favorite photo.

---

# 43. Comparison outputs

Create a developer comparison command or script that can generate multiple variants.

Example:

```text
original
resize-only
USM baseline
AcuLume natural
AcuLume crisp
optional Photoshop reference
optional staged-resize reference
```

Generate fixed crops automatically.

Recommended crop categories:

```text
strong edge
fine detail
flat area
shadow noise
highlight detail
```

---

# 44. Baseline algorithm

Implement a basic Unsharp Mask baseline for comparison even if it is not the final AcuLume pipeline.

Purpose:

```text
prove that AcuLume's additional complexity produces a visible benefit
```

It may live under an experimental/developer option.

Example:

```text
--engine baseline-usm
--engine adaptive
```

Default:

```text
adaptive
```

---

# 45. Objective metrics

Metrics may include:

```text
PSNR
SSIM
edge overshoot
edge undershoot
noise amplification
processing time
memory
```

But do not optimize solely for PSNR/SSIM.

The final quality target is:

```text
high perceived detail
without obvious sharpening artifacts
```

---

# 46. Quality acceptance criteria

AcuLume should be considered successful when, across the validation set:

1. fine detail is visibly clearer than resize-only output;
2. large bright halos are not obvious at normal viewing size;
3. dark halos remain controlled;
4. sky/skin/noise is not aggressively sharpened;
5. colored edges do not develop obvious RGB fringes;
6. fine patterns do not show substantially worse ringing/moiré than the resize baseline;
7. web output looks crisp at actual display size, not only at 400% zoom;
8. full-resolution images do not look brittle or crunchy at 100%;
9. output has correct orientation and color profile;
10. source file is unchanged.

---

# 47. Phase plan

## Phase 0 — Bootstrap

Create:

- solution/projects;
- package management;
- CLI shell;
- basic load/save;
- `info` command;
- test setup;
- CI-ready build command.

Definition of done:

```bash
dotnet build
dotnet test
aculume info sample.jpg
```

works.

---

## Phase 1 — Minimum viable sharpening pipeline

Implement:

1. image load;
2. EXIF orientation normalization;
3. float-capable processing path;
4. high-quality direct resize;
5. luminance/detail path;
6. fine band;
7. medium band;
8. independent light/dark weights;
9. basic soft noise threshold;
10. basic edge mask;
11. halo limiter v1;
12. recombination;
13. JPEG/PNG/TIFF save;
14. preset support;
15. CLI overrides;
16. debug image export;
17. unit and synthetic tests.

Phase 1 presets:

```text
full-natural
web-1800-natural
web-1800-crisp
```

---

## Phase 2 — Calibration

Build comparison corpus.

Tune:

```text
fine radius
fine amount
medium radius
medium amount
dark/light ratio
noise threshold
edge curve
halo limits
```

Compare against:

```text
resize only
USM
previous Photoshop Lighten/Darken result
Web-Sharpener-style reference where available
```

Do not copy settings blindly; use visual evaluation.

---

## Phase 3 — Resize research

Implement experimental strategies:

```text
single
staged
```

and:

```text
gamma-space resize
linear-light resize
```

Create automatic comparison matrices.

Do not change defaults until results justify it.

---

## Phase 4 — Advanced restoration

Experiment separately with:

```text
Wiener
Richardson-Lucy
small-PSF deconvolution
```

Use as optional capture/restoration sharpening.

Do not mix experimental deconvolution into stable default output sharpening until proven.

---

## Phase 5 — Distribution

Create local releases for:

```text
Windows x64 first
Linux x64 second
```

Goal:

```text
download/unzip
run aculume
```

Native libvips dependencies must be packaged correctly.

A self-contained .NET publish is desirable, but do **not** require NativeAOT in the first release if native dependency loading becomes fragile.

---

# 48. First implementation task for the agent

Start implementation immediately with this order.

## Task 1

Bootstrap repository and solution.

Use:

```text
.NET 10
C# 14
NetVips
System.CommandLine 2.x stable
xUnit
```

Create:

```text
AcuLume.Core
AcuLume.Cli
AcuLume.Core.Tests
AcuLume.Cli.Tests
```

Add central package management.

Enable:

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Use modern analyzers where appropriate without introducing excessive style noise.

---

## Task 2

Implement:

```bash
aculume info <image>
```

This proves:

- native libvips loads;
- image decode works;
- image dimensions can be read;
- metadata can be inspected;
- executable packaging direction is viable.

---

## Task 3

Implement a simple processing path:

```bash
aculume sharpen input.jpg \
  --long-edge 1800 \
  --output output.jpg
```

Initially this may perform only:

```text
decode
orientation
resize
encode
```

No sharpening yet.

Add integration test.

This establishes a trustworthy resize baseline.

---

## Task 4

Add simple luminance high-pass sharpening with:

```text
fine radius
amount
dark amount
light amount
```

Before adding edge/noise complexity, verify the Lighten/Darken split works.

Add synthetic black/white-edge test.

---

## Task 5

Add medium frequency band.

---

## Task 6

Add edge mask.

---

## Task 7

Add soft noise protection.

---

## Task 8

Add halo limiter.

---

## Task 9

Create the initial web preset and comparison outputs.

Only after these steps should algorithm tuning begin.

---

# 49. Coding rules for the agent

1. Keep methods small and testable.
2. Do not place algorithm logic in `Program.cs`.
3. Do not use static global mutable preset state.
4. Do not hard-code paths.
5. Do not hard-code `1800` inside Core algorithm logic.
6. Do not silently ignore ICC/metadata failures.
7. Avoid unnecessary image materialization.
8. Dispose native resources correctly where required by NetVips.
9. Avoid unsafe code unless benchmark data proves it is necessary.
10. Avoid managed per-pixel loops in the normal pipeline.
11. Every public option must be documented.
12. Every preset property must be validated.
13. Experimental algorithms must be marked as experimental.
14. Preserve backwards compatibility of preset schema once releases begin.
15. Never overwrite input by default.

---

# 50. Performance coding rules

Do not optimize based on guesses.

When changing implementation for performance:

1. benchmark before;
2. implement;
3. benchmark after;
4. ensure image output remains correct.

Benchmark at least:

```text
12 MP
24 MP
45–60 MP
```

and:

```text
full-resolution output
1800 px output
```

---

# 51. Debug/research mode

AcuLume is initially both a useful tool and a sharpening research platform.

Therefore make it easy to answer:

```text
What did the fine band contain?
What did the medium band contain?
Where did edge protection activate?
Where did noise protection activate?
Where did the halo limiter clamp?
How much did Lighten differ from Darken?
```

Without visibility into those intermediate stages, tuning becomes guesswork.

---

# 52. Future features

Do not implement now, but preserve architectural room for:

```text
GUI
folder watcher
n8n integration
REST API
Docker
OpenCvSharp experimental engine
GPU compute
RAW support
automatic noise estimation
face/skin protection
sky masking
subject-aware sharpening
print-size-aware output sharpening
DPI/viewing-distance profiles
AVIF/WebP/JXL advanced output
Windows Explorer context menu
side-by-side comparison viewer
```

---

# 53. Potential future OpenCV integration

Do not add OpenCvSharp to the initial dependency graph.

Only introduce it if needed for:

- advanced deconvolution;
- specific edge algorithms;
- local statistics unavailable/awkward in libvips;
- experimental computer-vision masks.

NetVips/libvips remains the primary image engine.

---

# 54. Security/privacy

AcuLume is local-only.

Requirements:

```text
no telemetry by default
no image upload
no external API calls
no cloud dependency
```

If update checking is ever added, it must never upload image metadata or filenames.

---

# 55. Licensing

Before public release:

- choose project license explicitly;
- include third-party notices;
- verify obligations of NetVips, NetVips.Native, libvips and bundled codecs;
- do not assume the wrapper's MIT license is the only relevant license when native binaries/codecs are distributed.

Create and maintain:

```text
THIRD_PARTY_NOTICES.md
```

---

# 56. README minimum content

README should ultimately include:

```text
What AcuLume is
Why output sharpening matters
Installation
Quick start
Full-resolution example
Web-1800 example
Batch example
Preset explanation
Light/Darken concept
Source-preservation warning
Supported formats
Color-management behavior
License
```

Avoid claiming "AI" because the initial engine is not AI based.

---

# 57. Definition of MVP

The MVP is complete when this works reliably:

```bash
aculume sharpen DSC_1234.jpg \
  --preset web-1800-natural \
  --output DSC_1234_web.jpg
```

and produces:

- long edge = 1800 px;
- correct orientation;
- correct colors;
- preserved required metadata;
- visibly improved detail;
- controlled bright halos;
- controlled dark halos;
- no obvious color fringes;
- no excessive sharpening of flat noise;
- unchanged original;
- reproducible output.

Also:

```bash
aculume batch ./photos \
  --output ./web \
  --preset web-1800-natural
```

must successfully process a folder.

---

# 58. Definition of first public-quality release

Before calling AcuLume `1.0`:

- at least 20-image photographic validation set;
- synthetic edge/noise tests;
- documented preset behavior;
- Windows x64 release package;
- batch processing;
- metadata and ICC testing;
- no source overwrite by default;
- comparison against resize-only and USM baseline;
- stable preset schema;
- useful errors/exit codes;
- benchmark report;
- third-party licensing review.

---

# 59. Key design decisions summary

The agent should treat these as current decisions unless implementation evidence justifies changing them.

| Decision | Choice |
|---|---|
| Product | Local CLI image sharpener |
| Name | **AcuLume** |
| Language | C# 14 |
| Runtime | .NET 10 LTS |
| Image engine | NetVips / libvips |
| CLI | System.CommandLine 2.x stable |
| Config | JSON presets |
| Main algorithm | Adaptive multi-frequency sharpening |
| Color strategy | Luminance-oriented sharpening |
| Frequency bands | Fine + Medium initially |
| Halo strategy | Separate bright/dark control + limiter |
| Noise strategy | Soft threshold/protection |
| Edge strategy | Gradient-based adaptive mask |
| Resize default | Single high-quality final resize |
| Staged resize | Experimental later |
| Web target | 1800 px preset first-class |
| Output sharpening | After final resize |
| Large images | Separate full-resolution preset |
| Source behavior | Never overwrite by default |
| Cloud | None |
| GUI | Not Phase 1 |
| AI | Not Phase 1 |
| RAW | Not Phase 1 |

---

# 60. External references

Research references used while defining the project:

- Adobe — sharpening concepts / Smart Sharpen documentation  
  https://helpx.adobe.com/photoshop/desktop/effects-filters/smart-filters/sharpen-controls-with-smart-sharpen.html

- Adobe — edge-mask sharpening concepts  
  https://helpx.adobe.com/photoshop/desktop/effects-filters/smart-filters/sharpen-image-using-edge-mask.html

- Andreas Resch — Web Sharpener  
  https://andreasresch.at/websharpener_en

- libvips  
  https://github.com/libvips/libvips

- NetVips  
  https://github.com/kleisauke/net-vips

- NetVips NuGet  
  https://www.nuget.org/packages/NetVips

- System.CommandLine  
  https://www.nuget.org/packages/System.CommandLine

- .NET support policy  
  https://dotnet.microsoft.com/platform/support/policy

- Cambridge in Colour — image sharpening/acutance background  
  https://www.cambridgeincolour.com/tutorials/image-sharpening.htm

These references are inputs for research and comparison. AcuLume must have its own documented implementation.

---

# 61. Final instruction to the coding agent

Do **not** begin by trying to invent the perfect sharpening formula.

Begin by creating a clean, observable processing pipeline with:

```text
decode
resize baseline
fine detail
light/dark split
medium detail
edge protection
noise protection
halo limiter
encode
```

Make every important parameter configurable.

Make intermediate stages inspectable.

Create synthetic tests early.

Then tune the algorithm against a diverse real-photo validation set.

The objective is not maximum numerical edge contrast.

The objective is:

> **maximum useful perceived detail with minimum visible sharpening artifacts at the actual intended output size.**

That is the core principle of **AcuLume**.
