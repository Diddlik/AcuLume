using System.CommandLine;
using AcuLume.Core.Configuration;

namespace AcuLume.Cli.Commands;

public static class PresetCommand
{
    public static Command Create()
    {
        var command = new Command("preset", "Inspect built-in and custom presets.");
        command.Add(CreateList());
        command.Add(CreateShow());
        command.Add(CreateValidate());
        return command;
    }

    private static Command CreateList()
    {
        var command = new Command("list", "List built-in preset names.");
        command.SetAction(_ =>
        {
            foreach (var name in PresetLoader.BuiltInPresetNames)
            {
                Console.WriteLine(name);
            }

            return (int)ExitCode.Success;
        });
        return command;
    }

    private static Command CreateShow()
    {
        var nameArgument = new Argument<string>("name");
        var command = new Command("show", "Print a preset's resolved settings.");
        command.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            try
            {
                var preset = PresetLoader.Load(name);
                var options = PresetLoader.ToProcessingOptions(preset);

                Console.WriteLine($"Name             {preset.Name}");
                Console.WriteLine($"Experimental     {preset.Experimental}");
                Console.WriteLine($"Long edge        {options.LongEdge?.ToString() ?? "(no resize)"}");
                Console.WriteLine($"Allow upscale    {options.AllowUpscale}");
                Console.WriteLine($"Quality          {options.Quality}");
                Console.WriteLine(
                    $"Fine band        radius={options.OutputSharpen.Fine.Radius} amount={options.OutputSharpen.Fine.Amount} " +
                    $"dark={options.OutputSharpen.Fine.DarkAmount} light={options.OutputSharpen.Fine.LightAmount}");
                Console.WriteLine(
                    $"Medium band      radius={options.OutputSharpen.Medium.Radius} amount={options.OutputSharpen.Medium.Amount} " +
                    $"dark={options.OutputSharpen.Medium.DarkAmount} light={options.OutputSharpen.Medium.LightAmount}");
                Console.WriteLine($"Edge protection  {options.OutputSharpen.EdgeProtection.Amount}");
                Console.WriteLine($"Noise protection {options.OutputSharpen.NoiseProtection.Amount}");
                Console.WriteLine($"Halo protection  {options.OutputSharpen.HaloLimiter.Amount}");
                Console.WriteLine(options.Denoise.IsEnabled
                    ? $"Denoise          {options.Denoise.Engine} amount={options.Denoise.Amount} " +
                      $"threshold={options.Denoise.Threshold}σ radius={options.Denoise.Radius}"
                    : "Denoise          off");

                return (int)ExitCode.Success;
            }
            catch (Exception ex) when (ex is FileNotFoundException or PresetValidationException)
            {
                Console.Error.WriteLine($"ERROR invalid preset '{name}': {ex.Message}");
                return (int)ExitCode.InvalidPreset;
            }
        });
        return command;
    }

    private static Command CreateValidate()
    {
        var nameArgument = new Argument<string>("name");
        var command = new Command("validate", "Validate a built-in preset name or a preset JSON file.");
        command.Add(nameArgument);

        command.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArgument)!;
            try
            {
                PresetLoader.Load(name);
                Console.WriteLine($"OK: '{name}' is a valid preset.");
                return (int)ExitCode.Success;
            }
            catch (Exception ex) when (ex is FileNotFoundException or PresetValidationException)
            {
                Console.Error.WriteLine($"INVALID: {ex.Message}");
                return (int)ExitCode.InvalidPreset;
            }
        });
        return command;
    }
}
