using System.CommandLine;
using AcuLume.Core;

namespace AcuLume.Cli.Commands;

/// <summary>
/// Phase 0 resize-only baseline (spec Task 3). No sharpening yet — that starts once this
/// decode/orientation/resize/encode path is proven trustworthy.
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

        var command = new Command("sharpen", "Resize (and, in later phases, sharpen) an image.");
        command.Add(inputArgument);
        command.Add(longEdgeOption);
        command.Add(outputOption);
        command.Add(qualityOption);
        command.Add(allowUpscaleOption);

        command.SetAction(parseResult =>
        {
            var input = parseResult.GetValue(inputArgument)!;
            var longEdge = parseResult.GetValue(longEdgeOption);
            var outputFile = parseResult.GetValue(outputOption);
            var quality = parseResult.GetValue(qualityOption);
            var allowUpscale = parseResult.GetValue(allowUpscaleOption);

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

            var outputPath = outputFile?.FullName ?? DefaultOutputPath(input);

            var options = new ProcessingOptions
            {
                LongEdge = longEdge,
                AllowUpscale = allowUpscale,
                Quality = quality,
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
