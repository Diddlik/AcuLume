using System.CommandLine;
using AcuLume.Core;
using AcuLume.Core.Configuration;
using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;

namespace AcuLume.Cli.Commands;

/// <summary>
/// Resize baseline (spec Task 3) plus the full output-sharpening pipeline: fine + medium bands
/// with dark/light asymmetric split (spec Tasks 4-5), edge protection (spec Task 6), noise
/// protection (spec Task 7), a halo limiter (spec Task 8), and presets (spec Task 9).
/// CLI flags always take precedence over the preset's values (spec section 26.7).
/// </summary>
public static class SharpenCommand
{
    public static Command Create()
    {
        var inputArgument = new Argument<FileInfo>("input");
        var presetOption = new Option<string?>("--preset");
        var longEdgeOption = new Option<int?>("--long-edge");
        var outputOption = new Option<FileInfo?>("--output", "-o");
        var qualityOption = new Option<int?>("--quality");
        var allowUpscaleOption = new Option<bool>("--allow-upscale");
        var resizeStrategyOption = new Option<ResizeStrategy?>("--resize-strategy");
        var resizeSpaceOption = new Option<ResizeSpace?>("--resize-space");
        var fineRadiusOption = new Option<double?>("--fine-radius");
        var fineAmountOption = new Option<double?>("--fine-amount");
        var mediumRadiusOption = new Option<double?>("--medium-radius");
        var mediumAmountOption = new Option<double?>("--medium-amount");
        var darkenOption = new Option<double?>("--darken");
        var lightenOption = new Option<double?>("--lighten");
        var edgeProtectionOption = new Option<double?>("--edge-protection");
        var edgeThresholdOption = new Option<double?>("--edge-threshold");
        var edgeSoftnessOption = new Option<double?>("--edge-softness");
        var edgeBlurOption = new Option<double?>("--edge-blur");
        var noiseProtectionOption = new Option<double?>("--noise-protection");
        var noiseThresholdOption = new Option<double?>("--noise-threshold");
        var noiseSoftnessOption = new Option<double?>("--noise-softness");
        var haloProtectionOption = new Option<double?>("--halo-protection");
        var haloWindowOption = new Option<double?>("--halo-window");
        var haloDarkLimitOption = new Option<double?>("--halo-dark-limit");
        var haloLightLimitOption = new Option<double?>("--halo-light-limit");

        var command = new Command("sharpen", "Resize and sharpen an image.");
        command.Add(inputArgument);
        command.Add(presetOption);
        command.Add(longEdgeOption);
        command.Add(outputOption);
        command.Add(qualityOption);
        command.Add(allowUpscaleOption);
        command.Add(resizeStrategyOption);
        command.Add(resizeSpaceOption);
        command.Add(fineRadiusOption);
        command.Add(fineAmountOption);
        command.Add(mediumRadiusOption);
        command.Add(mediumAmountOption);
        command.Add(darkenOption);
        command.Add(lightenOption);
        command.Add(edgeProtectionOption);
        command.Add(edgeThresholdOption);
        command.Add(edgeSoftnessOption);
        command.Add(edgeBlurOption);
        command.Add(noiseProtectionOption);
        command.Add(noiseThresholdOption);
        command.Add(noiseSoftnessOption);
        command.Add(haloProtectionOption);
        command.Add(haloWindowOption);
        command.Add(haloDarkLimitOption);
        command.Add(haloLightLimitOption);

        command.SetAction(parseResult =>
        {
            var input = parseResult.GetValue(inputArgument)!;

            if (!input.Exists)
            {
                Console.Error.WriteLine($"Input not found: {input.FullName}");
                return (int)ExitCode.InputNotFound;
            }

            ProcessingOptions baseOptions;
            var presetName = parseResult.GetValue(presetOption);
            if (presetName is not null)
            {
                try
                {
                    baseOptions = PresetLoader.ToProcessingOptions(PresetLoader.Load(presetName));
                }
                catch (Exception ex) when (ex is FileNotFoundException or PresetValidationException)
                {
                    Console.Error.WriteLine($"ERROR invalid preset '{presetName}': {ex.Message}");
                    return (int)ExitCode.InvalidPreset;
                }
            }
            else
            {
                baseOptions = new ProcessingOptions();
            }

            var longEdge = parseResult.GetValue(longEdgeOption) ?? baseOptions.LongEdge;
            var quality = parseResult.GetValue(qualityOption) ?? baseOptions.Quality;
            var allowUpscale = parseResult.GetValue(allowUpscaleOption) || baseOptions.AllowUpscale;
            var resizeStrategy = parseResult.GetValue(resizeStrategyOption) ?? baseOptions.ResizeStrategy;
            var resizeSpace = parseResult.GetValue(resizeSpaceOption) ?? baseOptions.ResizeSpace;

            var baseFine = baseOptions.OutputSharpen.Fine;
            var baseMedium = baseOptions.OutputSharpen.Medium;
            var baseEdge = baseOptions.OutputSharpen.EdgeProtection;
            var baseNoise = baseOptions.OutputSharpen.NoiseProtection;
            var baseHalo = baseOptions.OutputSharpen.HaloLimiter;
            var darken = parseResult.GetValue(darkenOption);
            var lighten = parseResult.GetValue(lightenOption);

            var options = baseOptions with
            {
                LongEdge = longEdge,
                AllowUpscale = allowUpscale,
                ResizeStrategy = resizeStrategy,
                ResizeSpace = resizeSpace,
                Quality = quality,
                OutputSharpen = new OutputSharpenOptions
                {
                    Fine = baseFine with
                    {
                        Radius = parseResult.GetValue(fineRadiusOption) ?? baseFine.Radius,
                        Amount = parseResult.GetValue(fineAmountOption) ?? baseFine.Amount,
                        DarkAmount = darken ?? baseFine.DarkAmount,
                        LightAmount = lighten ?? baseFine.LightAmount,
                    },
                    Medium = baseMedium with
                    {
                        Radius = parseResult.GetValue(mediumRadiusOption) ?? baseMedium.Radius,
                        Amount = parseResult.GetValue(mediumAmountOption) ?? baseMedium.Amount,
                        DarkAmount = darken ?? baseMedium.DarkAmount,
                        LightAmount = lighten ?? baseMedium.LightAmount,
                    },
                    EdgeProtection = baseEdge with
                    {
                        Amount = parseResult.GetValue(edgeProtectionOption) ?? baseEdge.Amount,
                        Threshold = parseResult.GetValue(edgeThresholdOption) ?? baseEdge.Threshold,
                        Softness = parseResult.GetValue(edgeSoftnessOption) ?? baseEdge.Softness,
                        DetectionBlur = parseResult.GetValue(edgeBlurOption) ?? baseEdge.DetectionBlur,
                    },
                    NoiseProtection = baseNoise with
                    {
                        Amount = parseResult.GetValue(noiseProtectionOption) ?? baseNoise.Amount,
                        Threshold = parseResult.GetValue(noiseThresholdOption) ?? baseNoise.Threshold,
                        Softness = parseResult.GetValue(noiseSoftnessOption) ?? baseNoise.Softness,
                    },
                    HaloLimiter = baseHalo with
                    {
                        Amount = parseResult.GetValue(haloProtectionOption) ?? baseHalo.Amount,
                        WindowRadius = parseResult.GetValue(haloWindowOption) ?? baseHalo.WindowRadius,
                        DarkLimit = parseResult.GetValue(haloDarkLimitOption) ?? baseHalo.DarkLimit,
                        LightLimit = parseResult.GetValue(haloLightLimitOption) ?? baseHalo.LightLimit,
                    },
                },
            };

            try
            {
                if (options.LongEdge is <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(options.LongEdge), "--long-edge must be positive.");
                }

                if (options.Quality is < 1 or > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(options.Quality), "--quality must be between 1 and 100.");
                }

                options.OutputSharpen.Fine.Validate();
                options.OutputSharpen.Medium.Validate();
                options.OutputSharpen.EdgeProtection.Validate();
                options.OutputSharpen.NoiseProtection.Validate();
                options.OutputSharpen.HaloLimiter.Validate();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.Error.WriteLine($"Invalid option: {ex.Message}");
                return (int)ExitCode.InvalidOptions;
            }

            var outputPath = parseResult.GetValue(outputOption)?.FullName ?? DefaultOutputPath(input);

            var processor = new AcuLumeProcessor();
            try
            {
                var result = processor.Process(input.FullName, outputPath, options);

                Console.WriteLine($"Input      {result.InputPath}");
                Console.WriteLine($"Size       {result.InputWidth} x {result.InputHeight}");
                Console.WriteLine($"Output     {result.OutputPath}");
                Console.WriteLine($"Resize     {result.InputWidth} x {result.InputHeight} -> {result.OutputWidth} x {result.OutputHeight}");
                Console.WriteLine($"Time       {result.Elapsed.TotalSeconds:0.00} s");

                return (int)ExitCode.Success;
            }
            catch (AcuLumeProcessingException ex) when (ex.Stage == ProcessingStage.Validate)
            {
                Console.Error.WriteLine($"ERROR {input.Name}: {ex.Message}");
                return (int)ExitCode.OutputConflict;
            }
            catch (AcuLumeProcessingException ex)
            {
                Console.Error.WriteLine($"ERROR {input.Name}: {ex.Stage.ToString().ToLowerInvariant()} failed: {ex.Message}");
                return (int)ExitCode.GeneralError;
            }
        });

        return command;
    }

    private static string DefaultOutputPath(FileInfo input)
    {
        var directory = input.DirectoryName ?? ".";
        var stem = Path.GetFileNameWithoutExtension(input.Name);
        return Path.Combine(directory, $"{stem}.aculume{input.Extension}");
    }
}
