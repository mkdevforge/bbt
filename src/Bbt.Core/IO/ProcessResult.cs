namespace Bbt.Core.IO;

public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

