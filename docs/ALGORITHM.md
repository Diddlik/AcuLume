# AcuLume algorithm notes

Tracks the working-space math actually implemented, per spec section 14 and 61. Updated as bands/stages are added.

## Working color space (fine band, Task 4)

Sharpening operates on the **L channel of a CIE Lab conversion** of the working image:

1. Convert sRGB → Lab (`Image.Colourspace(Enums.Interpretation.Lab)`).
2. Extract L (band 0); a/b (bands 1-2) are left untouched.
3. High-pass: `highPass = L - GaussianBlur(L, radius)` (radius = Gaussian sigma in px).
4. Asymmetric split: `weighted = highPass < 0 ? highPass * darkAmount : highPass * lightAmount`.
5. Scale: `contribution = weighted * amount`.
6. Recombine: `L' = L + contribution`, rejoin with the untouched a/b, convert back to the original interpretation.

Leaving chroma (a/b) untouched is what prevents chromatic edge halos (spec section 14) — color
never changes, only lightness.

Implementation: `AcuLume.Core.Sharpening.FineOutputSharpenStage`, `FrequencyBandExtractor`,
`AsymmetricDetailMixer`.

## Not yet implemented

- Medium frequency band (Task 5).
- Edge protection mask (Task 6).
- Soft noise protection (Task 7).
- Halo limiter (Task 8).
- Capture sharpening, presets, output-size-aware radius scaling.
