using System.CommandLine;
using AcuLume.Core;
using AcuLume.Core.Sharpening;

namespace AcuLume.Cli.Commands;

/// <summary>
/// Resize baseline (spec Task 3) plus fine + medium output sharpening with the dark/light
/// asymmetric split (spec Tasks 4-5). Edge/noise protection and halo limiting are later tasks.
/// </summary>
public static class SharpenCommand
{
    public static Command Create()
    {
        var inputArgument = new Argument<FileInfo>("input");
        var longEdgeOption = new Option<int?>("--long-edge");
        var outputOption = new Option<FileInfo?>("--output", "-o");
        var qualityOption = new Option<int>("--quality") { DefaultValueFactory = _ => 90 };
        var allowUpscaleOption = new Option<bool>("--allow-upscale");
        var fineRadiusOption = new Option<double>("--fine-radius") { DefaultValueFactory = _ => 0.6 };
        var fineAmountOption = new Option<double>("--fine-amount") { DefaultValueFactory = _ => 0.0 };
        var mediumRadiusOption = new Option<double>("--medium-radius") { DefaultValueFactory = _ => 1.4 };
        var mediumAmountOption = new Option<double>("--medium-amount") { DefaultValueFactory = _ => 0.0 };
        var darkenOption = new Option<double>("--darken") { DefaultValueFactory = _ => 0.8 };
        var lightenOption = new Option<double>("--lighten") { DefaultValueFactory = _ => 0.5 };

        var command = new Command("sharpen", "Resize and sharpen an image.");
        command.Add(inputArgument);
        command.Add(longEdgeOption);
        command.Add(outputOption);
        command.Add(qualityOption);
        command.Add(allowUpscaleOption);
        command.Add(fineRadiusOption);
        command.Add(fineAmountOption);
        command.Add(mediumRadiusOption);
        command.Add(mediumAmountOption);
        command.Add(darkenOption);
        command.Add(lightenOption);

        command.SetAction(parseResult =>
        {
            var input = parseResult.GetValue(inputArgument)!;
            var longEdge = parseResult.GetValue(longEdgeOption);
            var outputFile = parseResult.GetValue(outputOption);
            var quality = parseResult.GetValue(qualityOption);
            var allowUpscale = parseResult.GetValue(allowUpscaleOption);
            var fineRadius = parseResult.GetValue(fineRadiusOption);
            var fineAmount = parseResult.GetValue(fineAmountOption);
            var mediumRadius = parseResult.GetValue(mediumRadiusOption);
            var mediumAmount = parseResult.GetValue(mediumAmountOption);
            var darken = parseResult.GetValue(darkenOption);
            var lighten = parseResult.GetValue(lightenOption);

            if (!input.Exists)
            {
                Console.Error.WriteLine($"Input not found: {input.FullName}");
                return (int)ExitCode.InputNotFound;
            }

            if (quality is < 1 or > 100)
            {
                Console.Error.WriteLine("--quality must be between 1 and 100.");
                return (int)ExitCode.InvalidOptions;
            }

            if (fineRadius <= 0 || mediumRadius <= 0 || fineAmount < 0 || mediumAmount < 0 || darken < 0 || lighten < 0)
            {
                Console.Error.WriteLine(
                    "--fine-radius/--medium-radius must be positive; --fine-amount/--medium-amount/--darken/--lighten must not be negative.");
                return (int)ExitCode.InvalidOptions;
            }

            var outputPath = outputFile?.FullName ?? DefaultOutputPath(input);

            var options = new ProcessingOptions
            {
                LongEdge = longEdge,
                AllowUpscale = allowUpscale,
                Quality = quality,
                OutputSharpen = new OutputSharpenOptions
                {
                    Fine = new BandSharpenOptions
                    {
                        Radius = fineRadius,
                        Amount = fineAmount,
                        DarkAmount = darken,
                        LightAmount = lighten,
                    },
                    Medium = new BandSharpenOptions
                    {
                        Radius = mediumRadius,
                        Amount = mediumAmount,
                        DarkAmount = darken,
                        LightAmount = lighten,
                    },
                },
            };

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
