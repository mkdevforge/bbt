using System.Text.Json;
using System.Text.Json.Nodes;
using Bbt.Core.IO;
using Bbt.Core.Json;

namespace Bbt.Infrastructure;

public sealed class OutputWriter
{
    private readonly ProcessRunner _processRunner;

    public OutputWriter(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task WriteJsonAsync<T>(T model, BbtSettings settings, CancellationToken cancellationToken = default)
    {
        var node = JsonFieldSelector.SerializeToNode(model, BbtJson.OutputSerializerOptions);

        var fields = JsonFieldSelector.ParseFieldsCsv(settings.Fields);
        if (fields.Count > 0)
        {
            node = JsonFieldSelector.Apply(node, fields);
        }

        var json = node.ToJsonString(BbtJson.OutputSerializerOptions);

        if (!string.IsNullOrWhiteSpace(settings.Jq))
        {
            json = await RunJqAsync(settings.Jq!, json, cancellationToken);
        }

        Console.Out.WriteLine(json);
    }

    private async Task<string> RunJqAsync(string expression, string inputJson, CancellationToken cancellationToken)
    {
        if (!_processRunner.IsOnPath("jq"))
        {
            throw new InvalidOperationException("`jq` was not found on PATH. Install jq or omit --jq.");
        }

        var result = await _processRunner.RunAsync("jq", [expression], stdin: inputJson, cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            var err = string.IsNullOrWhiteSpace(result.Stderr) ? "jq failed." : result.Stderr.Trim();
            throw new InvalidOperationException(err);
        }

        return result.Stdout.TrimEnd();
    }

    public static void WriteQuietLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Console.Out.WriteLine(line);
        }
    }

    public static void WriteQuiet(string value)
    {
        Console.Out.WriteLine(value);
    }

    public static void WriteHuman(string value)
    {
        Console.Out.WriteLine(value);
    }
}

