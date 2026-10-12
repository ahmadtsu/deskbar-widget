using System.IO;
using System.Text.Json;
using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testRegistrySubKey;

    public ConfigServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"deskbar_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _testRegistrySubKey = $@"Software\DeskbarConfigTests_{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testRegistrySubKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ResolveConfigDirectory_WhenEnvVarSet_TakesPrecedence()
    {
        var customEnvDir = Path.Combine(_tempDirectory, "custom_env_config");
        Environment.SetEnvironmentVariable("DESKBAR_CONFIG_HOME", customEnvDir);

        try
        {
            var resolved = ConfigService.ResolveConfigDirectory();
            Assert.Equal(Path.GetFullPath(customEnvDir), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DESKBAR_CONFIG_HOME", null);
        }
    }

    [Fact]
    public void ResolveConfigDirectory_WhenDotConfigExists_PrefersDotConfig()
    {
        var fakeProfile = Path.Combine(_tempDirectory, "profile");
        var fakeAppData = Path.Combine(_tempDirectory, "appdata");
        var dotConfig = Path.Combine(fakeProfile, ".config", ConfigService.DefaultFolderName);
        var appDataConfig = Path.Combine(fakeAppData, ConfigService.DefaultFolderName);

        Directory.CreateDirectory(dotConfig);
        Directory.CreateDirectory(appDataConfig);

        var resolved = ConfigService.ResolveConfigDirectory(fakeProfile, fakeAppData);
        Assert.Equal(dotConfig, resolved);
    }

    [Fact]
    public void ResolveConfigDirectory_WhenOnlyAppDataExists_UsesAppData()
    {
        var fakeProfile = Path.Combine(_tempDirectory, "profile2");
        var fakeAppData = Path.Combine(_tempDirectory, "appdata2");
        var appDataConfig = Path.Combine(fakeAppData, ConfigService.DefaultFolderName);

        Directory.CreateDirectory(appDataConfig);

        var resolved = ConfigService.ResolveConfigDirectory(fakeProfile, fakeAppData);
        Assert.Equal(appDataConfig, resolved);
    }

    [Fact]
    public void InitializeConfig_WhenFileMissingAndNoRegistry_CreatesDefaultJsonFile()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");

        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: false);

        Assert.True(File.Exists(filePath));
        Assert.False(service.CurrentConfig.ShowAllDesktopNames);
        Assert.True(service.CurrentConfig.AutostartEnabled);

        var json = File.ReadAllText(filePath);
        Assert.Contains("showAllDesktopNames", json);
    }

    [Fact]
    public void InitializeConfig_WhenRegistryHasValues_MigratesToNewJsonFile()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");

        using (var key = Registry.CurrentUser.CreateSubKey(_testRegistrySubKey))
        {
            key.SetValue("ShowAllDesktopNames", 1, RegistryValueKind.DWord);
            key.SetValue("AutostartEnabled", 0, RegistryValueKind.DWord);
        }

        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: false);

        Assert.True(File.Exists(filePath));
        Assert.True(service.CurrentConfig.ShowAllDesktopNames);
        Assert.False(service.CurrentConfig.AutostartEnabled);
    }

    [Fact]
    public void Load_WhenJsonHasCommentsAndTrailingCommas_DeserializesSuccessfully()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");
        var rawJson = """
        {
            // Custom comment
            "showAllDesktopNames": true,
            "autostartEnabled": false,
            "font": "JetBrains Mono Nerd Font",
            "desktopIcons": {
                "1": "󰈹",
                "2": "󰨞",
            },
        }
        """;
        File.WriteAllText(filePath, rawJson);

        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: false);

        Assert.True(service.CurrentConfig.ShowAllDesktopNames);
        Assert.False(service.CurrentConfig.AutostartEnabled);
        Assert.Equal("JetBrains Mono Nerd Font", service.CurrentConfig.Font);
        Assert.NotNull(service.CurrentConfig.DesktopIcons);
        Assert.Equal("󰈹", service.CurrentConfig.DesktopIcons["1"]);
    }

    [Fact]
    public void Load_WhenJsonIsCorrupt_RetainsActiveConfig()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");
        File.WriteAllText(filePath, "{ invalid json !!! }");

        var loggedMessages = new List<string>();
        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            logger: loggedMessages.Add,
            enableWatcher: false);

        // Fallback default retained
        Assert.NotNull(service.CurrentConfig);
        Assert.False(service.CurrentConfig.ShowAllDesktopNames);
        Assert.Contains(loggedMessages, m => m.Contains("syntax error") || m.Contains("Retaining current"));
    }

    [Fact]
    public void Update_PersistsChangesToFileAtomically()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");

        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: false);

        service.Update(cfg =>
        {
            cfg.ShowAllDesktopNames = true;
            cfg.Font = "Cascadia Code";
        });

        Assert.True(service.CurrentConfig.ShowAllDesktopNames);
        Assert.Equal("Cascadia Code", service.CurrentConfig.Font);

        var content = File.ReadAllText(filePath);
        Assert.Contains("\"showAllDesktopNames\": true", content);
        Assert.Contains("\"font\": \"Cascadia Code\"", content);
    }

    [Fact]
    public async Task FileWatcher_WhenFileModifiedExternally_FiresConfigChangedEvent()
    {
        var filePath = Path.Combine(_tempDirectory, "config.json");

        using var service = new ConfigService(
            customFilePath: filePath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: true);

        var changedTcs = new TaskCompletionSource<DeskbarConfig>();
        service.ConfigChanged += (sender, config) =>
        {
            changedTcs.TrySetResult(config);
        };

        // Simulate external editor write
        await Task.Delay(100);
        var updatedJson = """
        {
            "showAllDesktopNames": true,
            "autostartEnabled": false,
            "font": "Fira Code"
        }
        """;
        await File.WriteAllTextAsync(filePath, updatedJson);

        var timeoutTask = Task.Delay(2000);
        var completed = await Task.WhenAny(changedTcs.Task, timeoutTask);

        Assert.Same(changedTcs.Task, completed);
        var reloadedConfig = await changedTcs.Task;
        Assert.True(reloadedConfig.ShowAllDesktopNames);
        Assert.False(reloadedConfig.AutostartEnabled);
        Assert.Equal("Fira Code", reloadedConfig.Font);
    }

    [Fact]
    public void DeskbarConfig_Clone_ProducesIndependentCopy()
    {
        var original = new DeskbarConfig
        {
            ShowAllDesktopNames = true,
            AutostartEnabled = false,
            Font = "CustomFont",
            DesktopIcons = new Dictionary<string, string> { { "1", "Icon1" } },
            CustomTheme = "Nord"
        };

        var clone = original.Clone();
        clone.ShowAllDesktopNames = false;
        clone.DesktopIcons!["1"] = "IconChanged";

        Assert.True(original.ShowAllDesktopNames);
        Assert.Equal("Icon1", original.DesktopIcons["1"]);
    }

    [Fact]
    public void Reload_FiresConfigChanged_AndUpdatesCurrentConfig()
    {
        var configPath = Path.Combine(_tempDirectory, "config.json");
        var service = new ConfigService(
            customFilePath: configPath,
            customRegistrySubKey: _testRegistrySubKey,
            enableWatcher: false);

        var updated = new DeskbarConfig
        {
            ShowAllDesktopNames = true,
            Font = "Cascadia Code"
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(updated, ConfigService.JsonOptions));

        DeskbarConfig? fired = null;
        service.ConfigChanged += (_, cfg) => fired = cfg;

        service.Reload();

        Assert.NotNull(fired);
        Assert.True(fired!.ShowAllDesktopNames);
        Assert.Equal("Cascadia Code", service.CurrentConfig.Font);
    }
}
