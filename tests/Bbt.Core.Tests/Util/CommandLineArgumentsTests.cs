using Bbt.Core.Util;

namespace Bbt.Core.Tests.Util;

public sealed class CommandLineArgumentsTests
{
    [Fact]
    public void TrimLeadingWhitespace_TrimsOnlyTheStartOfEachArgument()
    {
        var result = CommandLineArguments.TrimLeadingWhitespace(["pr", "create", "--body", "\n\tline one\n  line two ", "--title", "a b"]);

        Assert.Equal(["pr", "create", "--body", "line one\n  line two ", "--title", "a b"], result);
    }

    [Fact]
    public void TrimLeadingWhitespace_WhitespaceOnlyBecomesEmpty()
    {
        var result = CommandLineArguments.TrimLeadingWhitespace(["pr", "view", " \t "]);

        Assert.Equal(["pr", "view", ""], result);
    }
}
