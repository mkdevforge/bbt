namespace Bbt.Core.Diff;

public static class PullRequestDiffStatsCalculator
{
    public static PullRequestDiffStats Calculate(IEnumerable<DiffFile> files)
    {
        var filesChanged = 0;
        var linesAdded = 0;
        var linesRemoved = 0;

        foreach (var file in files)
        {
            filesChanged++;

            foreach (var hunk in file.Hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    switch (line.Type)
                    {
                        case DiffLineType.Add:
                            linesAdded++;
                            break;
                        case DiffLineType.Del:
                            linesRemoved++;
                            break;
                    }
                }
            }
        }

        return new PullRequestDiffStats(filesChanged, linesAdded, linesRemoved);
    }
}
