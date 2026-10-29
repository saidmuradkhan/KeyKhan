using System.Text.Json;
using System.Text.Json.Serialization;

namespace PassIn.App.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
        Current = Load(path);
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}
