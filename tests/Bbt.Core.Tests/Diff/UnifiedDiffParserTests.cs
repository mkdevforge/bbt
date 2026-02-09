using Bbt.Core.Diff;
using Xunit;

namespace Bbt.Core.Tests.Diff;

public sealed class UnifiedDiffParserTests
{
    [Fact]
    public void Parse_ProducesFilesHunksAndLineNumbers()
    {
        var diff = """
diff --git a/foo.txt b/foo.txt
index e69de29..4b825dc 100644
--- a/foo.txt
+++ b/foo.txt
@@ -0,0 +1,3 @@
+line1
+line2
+line3
diff --git a/src/old.cs b/src/new.cs
similarity index 88%
rename from src/old.cs
rename to src/new.cs
--- a/src/old.cs
+++ b/src/new.cs
@@ -10,2 +10,3 @@
 context
-old
+new
+added
diff --git a/bin.dat b/bin.dat
Binary files a/bin.dat and b/bin.dat differ
""";

        var files = UnifiedDiffParser.Parse(diff);

        Assert.Equal(3, files.Count);

        Assert.Equal("foo.txt", files[0].Path);
        Assert.False(files[0].IsBinary);
        Assert.Single(files[0].Hunks);
        Assert.Equal(3, files[0].Hunks[0].Lines.Count(l => l.Type == DiffLineType.Add));

        Assert.Equal("src/new.cs", files[1].Path);
        Assert.False(files[1].IsBinary);
        Assert.Single(files[1].Hunks);

        var hunk = files[1].Hunks[0];
        Assert.Equal(10, hunk.OldStart);
        Assert.Equal(2, hunk.OldCount);
        Assert.Equal(10, hunk.NewStart);
        Assert.Equal(3, hunk.NewCount);

        var contextLine = hunk.Lines[0];
        Assert.Equal(DiffLineType.Context, contextLine.Type);
        Assert.Equal(10, contextLine.OldLine);
        Assert.Equal(10, contextLine.NewLine);
        Assert.Equal("context", contextLine.Text);

        var deletedLine = hunk.Lines[1];
        Assert.Equal(DiffLineType.Del, deletedLine.Type);
        Assert.Equal(11, deletedLine.OldLine);
        Assert.Null(deletedLine.NewLine);
        Assert.Equal("old", deletedLine.Text);

        var addedLine = hunk.Lines[2];
        Assert.Equal(DiffLineType.Add, addedLine.Type);
        Assert.Null(addedLine.OldLine);
        Assert.Equal(11, addedLine.NewLine);
        Assert.Equal("new", addedLine.Text);

        Assert.Equal("bin.dat", files[2].Path);
        Assert.True(files[2].IsBinary);
        Assert.Empty(files[2].Hunks);
    }

    [Fact]
    public void Parse_DoesNotAddPhantomLine_WhenDiffEndsWithTrailingNewline()
    {
        var diff =
            "diff --git a/foo.txt b/foo.txt\n" +
            "--- a/foo.txt\n" +
            "+++ b/foo.txt\n" +
            "@@ -1,1 +1,1 @@\n" +
            "-old\n" +
            "+new\n";

        var files = UnifiedDiffParser.Parse(diff);

        Assert.Single(files);
        Assert.Single(files[0].Hunks);
        Assert.Equal(2, files[0].Hunks[0].Lines.Count);

        Assert.Equal(DiffLineType.Del, files[0].Hunks[0].Lines[0].Type);
        Assert.Equal(1, files[0].Hunks[0].Lines[0].OldLine);
        Assert.Null(files[0].Hunks[0].Lines[0].NewLine);

        Assert.Equal(DiffLineType.Add, files[0].Hunks[0].Lines[1].Type);
        Assert.Null(files[0].Hunks[0].Lines[1].OldLine);
        Assert.Equal(1, files[0].Hunks[0].Lines[1].NewLine);
    }
}
