using System.CommandLine;
using AcuLume.Core.Imaging;

namespace AcuLume.Cli.Commands;

public static class InfoCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<FileInfo>("image");

        var command = new Command("info", "Show information about an image file.");
        command.Add(pathArgument);

        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(pathArgument)!;

            if (!file.Exists)
            {
                Console.Error.WriteLine($"Input not found: {file.FullName}");
                return (int)ExitCode.InputNotFound;
            }

            ImageMetadata metadata;
            try
            {
                metadata = ImageLoader.ReadMetadata(file.FullName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR {file.Name}: decode failed: {ex.Message}");
                return (int)ExitCode.GeneralError;
            }

            Console.WriteLine($"Path         {metadata.Path}");
            Console.WriteLine($"Dimensions   {metadata.Width} x {metadata.Height}");
            Console.WriteLine($"Bands        {metadata.Bands}");
            Console.WriteLine($"Loader       {metadata.Loader}");
            Console.WriteLine($"Alpha        {metadata.HasAlpha}");
            Console.WriteLine($"Orientation  {metadata.Orientation}");
            Console.WriteLine($"ICC profile  {metadata.HasIccProfile}");

            return (int)ExitCode.Success;
        });

        return command;
    }
}
