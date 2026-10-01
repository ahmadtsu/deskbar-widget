// Adapted from EverythingToolbar (MIT © 2023 Stephan Rumswinkel) and YASB Reborn (MIT © 2024 amnweb).
// See ATTRIBUTION.md for license details.

using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DeskbarWidget.Core;

/// <summary>
/// Manages application autostart registration in HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Adapted from EverythingToolbar's AutostartAdapter and YASB's win32 autostart utilities.
/// </summary>
public sealed class AutostartService : IAutostartService
{
    public const string DefaultRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "DeskbarWidget";

    private readonly string _registryKeyPath;
    private readonly string _valueName;
    private readonly RegistryKey? _rootKey;
    private readonly bool _validateFileExists;

    public AutostartService(
        string registryKeyPath = DefaultRegistryKeyPath,
        string valueName = DefaultValueName,
        RegistryKey? rootKey = null,
        bool validateFileExists = true)
    {
        _registryKeyPath = registryKeyPath;
        _valueName = valueName;
        _rootKey = rootKey;
        _validateFileExists = validateFileExists;
    }

    private RegistryKey EffectiveRootKey => _rootKey ?? Registry.CurrentUser;

    public bool IsEnabled
    {
        get => CheckIsEnabled();
        set => SetAutostart(value);
    }

    public bool CheckIsEnabled()
    {
        try
        {
            using var key = EffectiveRootKey.OpenSubKey(_registryKeyPath, writable: false);
            var value = key?.GetValue(_valueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var path = ExtractExecutablePath(value);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return !_validateFileExists || File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public bool Enable(string? executablePath = null)
    {
        try
        {
            var cleanPath = ResolveExecutablePath(executablePath);
            if (string.IsNullOrWhiteSpace(cleanPath))
            {
                return false;
            }

            using var key = EffectiveRootKey.CreateSubKey(_registryKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue(_valueName, $"\"{cleanPath}\"", RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Disable()
    {
        try
        {
            using var key = EffectiveRootKey.OpenSubKey(_registryKeyPath, writable: true);
            if (key is not null)
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SetAutostart(bool enable, string? executablePath = null)
    {
        return enable ? Enable(executablePath) : Disable();
    }

    /// <summary>
    /// Extracts the file path from a raw command line string, handling quotes and arguments.
    /// </summary>
    public static string ExtractExecutablePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('\"'))
        {
            var closingQuote = trimmed.IndexOf('\"', 1);
            if (closingQuote > 1)
            {
                return trimmed.Substring(1, closingQuote - 1).Trim();
            }
            return trimmed.Trim('\"').Trim();
        }

        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0 && !File.Exists(trimmed))
        {
            return trimmed.Substring(0, firstSpace).Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// Resolves the effective executable path to register, intelligently detecting
    /// if the host process is the dotnet CLI and preferring the corresponding application exe.
    /// </summary>
    public static string ResolveExecutablePath(string? candidate = null)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate.Trim().Trim('\"');
        }

        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var fileName = Path.GetFileName(processPath);
            if (fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var entry = System.Reflection.Assembly.GetEntryAssembly();
                var appName = entry?.GetName().Name;
                if (!string.IsNullOrEmpty(appName))
                {
                    var exeInBase = Path.Combine(AppContext.BaseDirectory, appName + ".exe");
                    if (File.Exists(exeInBase))
                    {
                        return exeInBase;
                    }
                }

                var defaultExe = Path.Combine(AppContext.BaseDirectory, "DeskbarWidget.exe");
                if (File.Exists(defaultExe))
                {
                    return defaultExe;
                }

                var appExe = Path.Combine(AppContext.BaseDirectory, "DeskbarWidget.App.exe");
                if (File.Exists(appExe))
                {
                    return appExe;
                }
            }

            return processPath.Trim().Trim('\"');
        }

        return string.Empty;
    }
}
