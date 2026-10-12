using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DeskbarWidget.Core;

/// <summary>
/// Service managing reading, writing, migration, and live file-watching of DeskbarWidget configuration.
/// Follows modern Windows dotfiles best practices (~/.config/DeskbarWidget/config.json with %APPDATA% fallback).
/// </summary>
public sealed class ConfigService : IDisposable
{
    public const string DefaultFolderName = "DeskbarWidget";
    public const string DefaultFileName = "config.json";
    public const string DefaultRegistrySubKey = @"Software\DeskbarWidget";

    private static readonly Lazy<ConfigService> LazyInstance = new(() => new ConfigService());
    public static ConfigService Instance => LazyInstance.Value;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _gate = new();
    public Action<string>? Logger { get; set; }
    private readonly RegistryKey? _registryRootKey;
    private readonly string _registrySubKey;
    private readonly bool _enableWatcher;

    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private string? _lastKnownHash;
    private bool _disposed;

    public string ConfigDirectory { get; }
    public string ConfigFilePath { get; }
    public DeskbarConfig CurrentConfig { get; private set; }

    /// <summary>
    /// Event triggered when configuration is reloaded from an external file modification.
    /// </summary>
    public event EventHandler<DeskbarConfig>? ConfigChanged;

    public ConfigService(
        string? customFilePath = null,
        string? customRegistrySubKey = null,
        RegistryKey? customRegistryRoot = null,
        Action<string>? logger = null,
        bool enableWatcher = true)
    {
        Logger = logger;
        _registryRootKey = customRegistryRoot;
        _registrySubKey = customRegistrySubKey ?? DefaultRegistrySubKey;
        _enableWatcher = enableWatcher;

        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            ConfigFilePath = Path.GetFullPath(customFilePath);
            ConfigDirectory = Path.GetDirectoryName(ConfigFilePath) ?? Directory.GetCurrentDirectory();
        }
        else
        {
            ConfigDirectory = ResolveConfigDirectory();
            ConfigFilePath = Path.Combine(ConfigDirectory, DefaultFileName);
        }

        CurrentConfig = new DeskbarConfig();
        InitializeConfig();

        if (_enableWatcher)
        {
            InitializeWatcher();
        }
    }

    /// <summary>
    /// Resolves the base directory for DeskbarWidget configuration following standard priority:
    /// 1. DESKBAR_CONFIG_HOME or DESKBAR_CONFIG_DIR environment variables.
    /// 2. Existing %USERPROFILE%\.config\DeskbarWidget.
    /// 3. Existing %APPDATA%\DeskbarWidget.
    /// 4. Default for fresh installations: %USERPROFILE%\.config\DeskbarWidget.
    /// </summary>
    public static string ResolveConfigDirectory(string? userProfilePath = null, string? appDataPath = null)
    {
        var envOverride = Environment.GetEnvironmentVariable("DESKBAR_CONFIG_HOME")
            ?? Environment.GetEnvironmentVariable("DESKBAR_CONFIG_DIR");

        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var profile = userProfilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dotConfigPath = Path.Combine(profile, ".config", DefaultFolderName);
        if (Directory.Exists(dotConfigPath) || File.Exists(Path.Combine(dotConfigPath, DefaultFileName)))
        {
            return dotConfigPath;
        }

        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataConfigPath = Path.Combine(appData, DefaultFolderName);
        if (Directory.Exists(appDataConfigPath) || File.Exists(Path.Combine(appDataConfigPath, DefaultFileName)))
        {
            return appDataConfigPath;
        }

        return dotConfigPath;
    }

    private void InitializeConfig()
    {
        lock (_gate)
        {
            if (File.Exists(ConfigFilePath))
            {
                if (TryLoadFromFile(out var loadedConfig, out var hash))
                {
                    CurrentConfig = loadedConfig;
                    _lastKnownHash = hash;
                    Log($"Loaded existing configuration from '{ConfigFilePath}'.");
                    return;
                }
            }

            // File does not exist (or could not be read): check registry migration
            var migratedConfig = TryMigrateFromRegistry();
            CurrentConfig = migratedConfig ?? new DeskbarConfig();
            SaveInternal(CurrentConfig);
            Log(migratedConfig is not null
                ? $"Migrated registry settings to '{ConfigFilePath}'."
                : $"Generated default configuration at '{ConfigFilePath}'.");
        }
    }

    private DeskbarConfig? TryMigrateFromRegistry()
    {
        try
        {
            var root = _registryRootKey ?? Registry.CurrentUser;
            using var key = root.OpenSubKey(_registrySubKey, writable: false);
            if (key is null)
            {
                return null;
            }

            var rawShowAll = key.GetValue("ShowAllDesktopNames");
            var rawAutostart = key.GetValue("AutostartEnabled");

            if (rawShowAll is null && rawAutostart is null)
            {
                return null;
            }

            var config = new DeskbarConfig();
            if (rawShowAll is int showAllInt)
            {
                config.ShowAllDesktopNames = showAllInt != 0;
            }

            if (rawAutostart is int autostartInt)
            {
                config.AutostartEnabled = autostartInt != 0;
            }

            return config;
        }
        catch (Exception ex)
        {
            Log($"Failed to probe registry for migration: {ex.Message}");
            return null;
        }
    }

    private bool TryLoadFromFile(out DeskbarConfig config, out string hash)
    {
        config = CurrentConfig;
        hash = string.Empty;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var stream = new FileStream(ConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var json = reader.ReadToEnd();

                var bytes = Encoding.UTF8.GetBytes(json);
                hash = Convert.ToHexString(MD5.HashData(bytes));

                var deserialized = JsonSerializer.Deserialize<DeskbarConfig>(json, JsonOptions);
                if (deserialized is not null)
                {
                    config = deserialized;
                    return true;
                }

                return false;
            }
            catch (JsonException jsonEx)
            {
                Log($"Config file JSON syntax error at '{ConfigFilePath}': {jsonEx.Message}. Retaining current active configuration.");
                return false;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                Log($"Error reading config file '{ConfigFilePath}': {ex.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Updates configuration in memory and saves atomically to disk.
    /// </summary>
    public void Update(Action<DeskbarConfig> updateAction)
    {
        lock (_gate)
        {
            var updated = CurrentConfig.Clone();
            updateAction(updated);
            CurrentConfig = updated;
            SaveInternal(CurrentConfig);
        }
    }

    /// <summary>
    /// Forces an immediate reload of the configuration from disk and fires ConfigChanged.
    /// </summary>
    public void Reload()
    {
        lock (_gate)
        {
            if (_disposed || !File.Exists(ConfigFilePath))
            {
                return;
            }

            if (TryLoadFromFile(out var newConfig, out var newHash))
            {
                _lastKnownHash = newHash;
                CurrentConfig = newConfig;
                Log($"Reloaded configuration from '{ConfigFilePath}'.");

                try
                {
                    ConfigChanged?.Invoke(this, newConfig.Clone());
                }
                catch (Exception ex)
                {
                    Log($"Exception dispatching ConfigChanged event: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Saves the provided configuration directly to disk.
    /// </summary>
    public void Save(DeskbarConfig config)
    {
        lock (_gate)
        {
            CurrentConfig = config.Clone();
            SaveInternal(CurrentConfig);
        }
    }

    private void SaveInternal(DeskbarConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Compute hash so watcher will ignore our own write
            _lastKnownHash = Convert.ToHexString(MD5.HashData(bytes));

            var tempFilePath = Path.Combine(ConfigDirectory, $".{Path.GetFileName(ConfigFilePath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(tempFilePath, bytes);
            File.Move(tempFilePath, ConfigFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log($"Failed to save config to '{ConfigFilePath}': {ex.Message}");
            throw;
        }
    }

    private void InitializeWatcher()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            _watcher = new FileSystemWatcher(ConfigDirectory)
            {
                Filter = Path.GetFileName(ConfigFilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileEvent;
            _watcher.Created += OnConfigFileEvent;
            _watcher.Renamed += OnConfigFileEvent;
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize file watcher for '{ConfigDirectory}': {ex.Message}");
        }
    }

    private void OnConfigFileEvent(object sender, FileSystemEventArgs e)
    {
        // Debounce: editors often fire multiple events or lock briefly during writes
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(OnDebouncedFileChanged, null, 150, Timeout.Infinite);
    }

    private void OnDebouncedFileChanged(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !File.Exists(ConfigFilePath))
            {
                return;
            }

            if (TryLoadFromFile(out var newConfig, out var newHash))
            {
                if (string.Equals(newHash, _lastKnownHash, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Same content, no need to reload
                }

                _lastKnownHash = newHash;
                CurrentConfig = newConfig;
                Log($"Detected file change. Reloaded configuration from '{ConfigFilePath}'.");

                try
                {
                    ConfigChanged?.Invoke(this, newConfig.Clone());
                }
                catch (Exception ex)
                {
                    Log($"Exception dispatching ConfigChanged event: {ex.Message}");
                }
            }
        }
    }

    private void Log(string message)
    {
        Logger?.Invoke(message);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _debounceTimer?.Dispose();
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnConfigFileEvent;
                _watcher.Created -= OnConfigFileEvent;
                _watcher.Renamed -= OnConfigFileEvent;
                _watcher.Dispose();
            }
        }
    }
}
