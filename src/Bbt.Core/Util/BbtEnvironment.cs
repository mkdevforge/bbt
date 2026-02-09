using System.Diagnostics.CodeAnalysis;

namespace Bbt.Core.Util;

public static class BbtEnvironment
{
    public static bool TryGetNonEmpty(string name, [NotNullWhen(true)] out string? value)
    {
        value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            return false;
        }

        return true;
    }

    public static string? GetNonEmptyOrNull(string name)
    {
        return TryGetNonEmpty(name, out var value) ? value : null;
    }
}

