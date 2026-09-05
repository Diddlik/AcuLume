using System.CommandLine;
using System.Globalization;
using AcuLume.Core.Comparison;

namespace AcuLume.Cli.Commands;

/// <summary>
/// Developer comparison command (spec sections 43-44): generates original / resize-only /
/// USM-baseline / AcuLume-natural / AcuLume-crisp variants so the pipeline's benefit over simple
/// sharpening can be judged visually.
/// </summary>
public static class CompareCommand
{
    public static Command Create()
    {
        var inputArgument = new Argument<FileInfo>("input");
        var outputOption = new Option<DirectoryInfo>("--output", "-o") { Required = true };
        var longEdgeOption = new Option<int>("--long-edge") { DefaultValueFactory = _ => 1800 };
        var usmRadiusOption = new Option<double>("--usm-radius") { DefaultValueFactory = _ => 1.0 };
        var usmAmountOption = new Option<double>("--usm-amount") { DefaultValueFactory = _ => 0.5 };
        var qualityOption = new Option<int>("--quality") { DefaultValueFactory = _ => 90 };
        var cropOption = new Option<string[]>("--crop")
        {
            Description = "label=x,y,width,height — repeatable. Applies the same fixed region to every variant.",
        };

        var command = new Command("compare", "Generate side-by-side comparison variants for one image.");
        command.Add(inputArgument);
        command.Add(outputOption);
        command.Add(longEdgeOption);
        command.Add(usmRadiusOption);
        command.Add(usmAmountOption);
        command.Add(qualityOption);
        command.Add(cropOption);

        command.SetAction(parseResult =>
        {
            var input = parseResult.GetValue(inputArgument)!;
            var outputDirectory = parseResult.GetValue(outputOption)!;
            var longEdge = parseResult.GetValue(longEdgeOption);
            var usmRadius = parseResult.GetValue(usmRadiusOption);
            var usmAmount = parseResult.GetValue(usmAmountOption);
            var quality = parseResult.GetValue(qualityOption);
            var cropArgs = parseResult.GetValue(cropOption) ?? [];

            if (!input.Exists)
            {
                Console.Error.WriteLine($"Input not found: {input.FullName}");
                return (int)ExitCode.InputNotFound;
            }

            List<CropSpec> crops;
            try
            {
                crops = cropArgs.Select(ParseCrop).ToList();
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine($"Invalid --crop: {ex.Message}");
                return (int)ExitCode.InvalidOptions;
            }

            var options = new ComparisonSetOptions
            {
                LongEdge = longEdge,
                UsmRadius = usmRadius,
                UsmAmount = usmAmount,
                Quality = quality,
            };

            var variants = ComparisonSetBuilder.Build(input.FullName, outputDirectory.FullName, options);
            foreach (var variant in variants)
            {
                Console.WriteLine($"{variant.Name,-16} {variant.Path}");
            }

            if (crops.Count > 0)
            {
                var cropPaths = CropGenerator.Generate(variants, crops, outputDirectory.FullName);
                Console.WriteLine($"Crops            {cropPaths.Count} files under {Path.Combine(outputDirectory.FullName, "crops")}");
            }

            return (int)ExitCode.Success;
        });

        return command;
    }

    private static CropSpec ParseCrop(string arg)
    {
        var equalsIndex = arg.IndexOf('=');
        if (equalsIndex < 0)
        {
            throw new FormatException($"expected label=x,y,width,height, got '{arg}'");
        }

        var label = arg[..equalsIndex];
        var parts = arg[(equalsIndex + 1)..].Split(',');
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            throw new FormatException($"expected label=x,y,width,height, got '{arg}'");
        }

        var values = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        return new CropSpec(label, values[0], values[1], values[2], values[3]);
    }
}
