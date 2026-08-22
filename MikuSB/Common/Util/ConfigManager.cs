using MikuSB.Configuration;
using MikuSB.Internationalization;
using Newtonsoft.Json;
using MikuSB.Util.Extensions;

namespace MikuSB.Util;

public static class ConfigManager
{
    public static readonly Logger Logger = new("ConfigManager");
    public static ConfigContainer Config { get; private set; } = new();
    private static readonly string ConfigFilePath = Config.Path.ConfigPath + "/Config.json";
    public static HotfixContainer Hotfix { get; private set; } = new();
    private static readonly string HotfixFilePath = Config.Path.ConfigPath + "/Hotfix.json";

    public static void LoadConfig()
    {
        LoadConfigData();
        //LoadHotfixData();
    }

    public static void SaveConfig()
    {
        SaveData(Config, ConfigFilePath);
    }

    private static void LoadConfigData()
    {
        var file = new FileInfo(ConfigFilePath);
        if (!file.Exists)
        {
            Config = new()
            {
                ServerOption =
                {
                    Language = Extensions.Extensions.GetCurrentLanguage()
                }
            };

            Logger.Info("Current Language is " + Config.ServerOption.Language);
            SaveData(Config, ConfigFilePath);
        }

        using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            var json = reader.ReadToEnd();
            Config = JsonConvert.DeserializeObject<ConfigContainer>(json)!;
        }

        Config.Loader.Arguments = NormalizeLoaderArguments(Config.Loader.Arguments);
        SaveData(Config, ConfigFilePath);
    }

    private static string[] NormalizeLoaderArguments(string[]? arguments)
    {
        var result = new List<string>(arguments ?? []);
        var userDataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Client_User_Data"));
        Directory.CreateDirectory(userDataDirectory);

        var userDirArgument = $"-userdir={userDataDirectory}";
        var existingIndex = result.FindIndex(x => x.StartsWith("-userdir=", StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            result[existingIndex] = userDirArgument;
        else
            result.Add(userDirArgument);

        return result.ToArray();
    }

    private static void LoadHotfixData()
    {
        var file = new FileInfo(HotfixFilePath);

        // Generate all necessary versions
        var verList = Extensions.Extensions.GetSupportVersions();

        Logger.Info(I18NManager.Translate("Server.ServerInfo.CurrentVersion",
            verList.Aggregate((current, next) => $"{current}, {next}")));

        if (!file.Exists)
        {
            Hotfix = new HotfixContainer();
            SaveData(Hotfix, HotfixFilePath);
            file.Refresh();
        }

        using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            var json = reader.ReadToEnd();
            Hotfix = JsonConvert.DeserializeObject<HotfixContainer>(json)!;
        }

        foreach (var version in verList)
            if (!Hotfix.Hotfixes.TryGetValue(version, out var _))
                Hotfix.Hotfixes[version] = new();

        SaveData(Hotfix, HotfixFilePath);
    }

    private static void SaveData(object data, string path)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }

    public static void InitDirectories()
    {
        foreach (var property in Config.Path.GetType().GetProperties())
        {
            var dir = property.GetValue(Config.Path)?.ToString();

            if (!string.IsNullOrEmpty(dir))
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
        }
    }
}