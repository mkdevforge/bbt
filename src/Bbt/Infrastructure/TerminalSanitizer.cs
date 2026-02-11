using System.Text;
using Spectre.Console;

namespace Bbt.Infrastructure;

internal static class TerminalSanitizer
{
    public static string EscapeMarkup(string? value)
    {
        return Markup.Escape(Sanitize(value) ?? string.Empty);
    }

    public static string? Sanitize(string? value)
    {
        if (value is null || value.Length == 0)
        {
            return value;
        }

        var needsChanges = false;
        foreach (var ch in value)
        {
            if (ch == '\r' || (char.IsControl(ch) && ch is not ('\n' or '\t')))
            {
                needsChanges = true;
                break;
            }
        }

        if (!needsChanges)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '\r')
            {
                if (i + 1 < value.Length && value[i + 1] == '\n')
                {
                    continue;
                }

                sb.Append('\n');
                continue;
            }

            if (ch is '\n' or '\t')
            {
                sb.Append(ch);
                continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}

