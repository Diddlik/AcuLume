using System.CommandLine;
using AcuLume.Cli.Commands;

namespace AcuLume.Cli;

/// <summary>Builds and runs the command tree. Kept separate from Program.cs so it is directly testable.</summary>
public static class CliApp
{
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("AcuLume — local, deterministic, output-aware image sharpening.");
        root.Add(InfoCommand.Create());
        root.Add(SharpenCommand.Create());
        root.Add(PresetCommand.Create());
        root.Add(CompareCommand.Create());
        return root;
    }

    public static int Run(string[] args) => BuildRootCommand().Parse(args).Invoke();
}
