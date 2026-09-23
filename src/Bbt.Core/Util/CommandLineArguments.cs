namespace Bbt.Core.Util;

public static class CommandLineArguments
{
    /// <summary>
    /// Spectre.Cli 0.49.0 hangs forever when an argument starts with whitespace (e.g. `--body "$(cat notes.md)"`
    /// where the file starts with a blank line, or `pr view " "`). Spectre trims values anyway, so trimming the
    /// leading whitespace first does not change any value that parses today. A whitespace-only argument becomes
    /// empty, which Spectre reports as a missing value.
    /// </summary>
    public static string[] TrimLeadingWhitespace(string[] args)
    {
        return args.Select(arg => arg.TrimStart()).ToArray();
    }
}
