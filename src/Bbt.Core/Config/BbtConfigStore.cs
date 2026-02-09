using System.Text.Json;

namespace Bbt.Core.Config;

public sealed class BbtConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string ConfigFilePath { get; }

    public BbtConfigStore(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? BbtPaths.GetConfigFilePath();
    }

    public async Task<BbtConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new BbtConfig();
        }

        var json = await File.ReadAllTextAsync(ConfigFilePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BbtConfig();
        }

        var config = JsonSerializer.Deserialize<BbtConfig>(json, SerializerOptions);
        return config ?? new BbtConfig();
    }

    public async Task SaveAsync(BbtConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        var tempPath = $"{ConfigFilePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        if (File.Exists(ConfigFilePath))
        {
            File.Replace(tempPath, ConfigFilePath, null);
        }
        else
        {
            File.Move(tempPath, ConfigFilePath);
        }
    }
}

