using DeskbarWidget.Core;
using Microsoft.Win32;

namespace DeskbarWidget.Tests;

public sealed class AutostartServiceTests : IDisposable
{
    private readonly string _testSubKeyPath;
    private readonly string _tempFile;

    public AutostartServiceTests()
    {
        _testSubKeyPath = $@"Software\DeskbarWidgetTests_{Guid.NewGuid():N}";
        _tempFile = Path.Combine(Path.GetTempPath(), $"deskbar_test_{Guid.NewGuid():N}.exe");
        File.WriteAllText(_tempFile, "fake exe content");
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testSubKeyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ExtractExecutablePath_WhenQuoted_ReturnsCleanPath()
    {
        var raw = "\"C:\\Program Files\\DeskbarWidget\\DeskbarWidget.exe\"";
        var result = AutostartService.ExtractExecutablePath(raw);
        Assert.Equal(@"C:\Program Files\DeskbarWidget\DeskbarWidget.exe", result);
    }

    [Fact]
    public void ExtractExecutablePath_WhenQuotedWithArguments_ReturnsCleanExePath()
    {
        var raw = "\"C:\\Program Files\\DeskbarWidget\\DeskbarWidget.exe\" --align=right";
        var result = AutostartService.ExtractExecutablePath(raw);
        Assert.Equal(@"C:\Program Files\DeskbarWidget\DeskbarWidget.exe", result);
    }

    [Fact]
    public void ExtractExecutablePath_WhenUnquotedWithoutArguments_ReturnsPath()
    {
        var raw = @"C:\DeskbarWidget\DeskbarWidget.exe";
        var result = AutostartService.ExtractExecutablePath(raw);
        Assert.Equal(@"C:\DeskbarWidget\DeskbarWidget.exe", result);
    }

    [Fact]
    public void ExtractExecutablePath_WhenUnquotedWithArguments_ReturnsBeforeSpace()
    {
        var raw = @"C:\DeskbarWidget\DeskbarWidget.exe --align=right";
        var result = AutostartService.ExtractExecutablePath(raw);
        Assert.Equal(@"C:\DeskbarWidget\DeskbarWidget.exe", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractExecutablePath_WhenNullOrWhitespace_ReturnsEmpty(string? raw)
    {
        var result = AutostartService.ExtractExecutablePath(raw!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CheckIsEnabled_WhenKeyDoesNotExist_ReturnsFalse()
    {
        var service = new AutostartService(_testSubKeyPath);
        Assert.False(service.CheckIsEnabled());
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void CheckIsEnabled_WhenValueDoesNotExist_ReturnsFalse()
    {
        using (Registry.CurrentUser.CreateSubKey(_testSubKeyPath)) { }

        var service = new AutostartService(_testSubKeyPath);
        Assert.False(service.CheckIsEnabled());
    }

    [Fact]
    public void Enable_WritesQuotedPathToRegistry()
    {
        var service = new AutostartService(_testSubKeyPath);
        var success = service.Enable(_tempFile);
        Assert.True(success);

        using var key = Registry.CurrentUser.OpenSubKey(_testSubKeyPath);
        var value = key?.GetValue(AutostartService.DefaultValueName) as string;
        Assert.Equal($"\"{_tempFile}\"", value);
    }

    [Fact]
    public void CheckIsEnabled_WhenFileExists_ReturnsTrue()
    {
        var service = new AutostartService(_testSubKeyPath);
        service.Enable(_tempFile);

        Assert.True(service.CheckIsEnabled());
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void CheckIsEnabled_WhenFileDoesNotExist_ReturnsFalse()
    {
        var nonExistentPath = @"C:\NonExistentFolder\NonExistentApp.exe";
        var service = new AutostartService(_testSubKeyPath, validateFileExists: true);
        service.Enable(nonExistentPath);

        Assert.False(service.CheckIsEnabled());
    }

    [Fact]
    public void CheckIsEnabled_WhenValidationDisabled_ReturnsTrueEvenIfFileDoesNotExist()
    {
        var nonExistentPath = @"C:\NonExistentFolder\NonExistentApp.exe";
        var service = new AutostartService(_testSubKeyPath, validateFileExists: false);
        service.Enable(nonExistentPath);

        Assert.True(service.CheckIsEnabled());
    }

    [Fact]
    public void Disable_RemovesRegistryValue()
    {
        var service = new AutostartService(_testSubKeyPath);
        service.Enable(_tempFile);
        Assert.True(service.CheckIsEnabled());

        var disableSuccess = service.Disable();
        Assert.True(disableSuccess);
        Assert.False(service.CheckIsEnabled());
        Assert.False(service.IsEnabled);

        using var key = Registry.CurrentUser.OpenSubKey(_testSubKeyPath);
        var value = key?.GetValue(AutostartService.DefaultValueName);
        Assert.Null(value);
    }

    [Fact]
    public void Disable_WhenValueAlreadyMissing_SucceedsWithoutError()
    {
        var service = new AutostartService(_testSubKeyPath);
        var disableSuccess = service.Disable();
        Assert.True(disableSuccess);
    }

    [Fact]
    public void SetAutostart_True_Enables_And_False_Disables()
    {
        var service = new AutostartService(_testSubKeyPath);

        Assert.True(service.SetAutostart(true, _tempFile));
        Assert.True(service.CheckIsEnabled());

        Assert.True(service.SetAutostart(false));
        Assert.False(service.CheckIsEnabled());
    }

    [Fact]
    public void IsEnabled_PropertySetter_UpdatesAutostartState()
    {
        var service = new AutostartService(_testSubKeyPath);
        // Initial state
        Assert.False(service.IsEnabled);

        // Pre-create entry with valid temp file
        service.Enable(_tempFile);
        Assert.True(service.IsEnabled);

        // Disable via property
        service.IsEnabled = false;
        Assert.False(service.IsEnabled);
    }
}
