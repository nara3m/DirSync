using PCNetworkBackup.Core.Models;
using PCNetworkBackup.Core.Services;
using Xunit;

namespace PCNetworkBackup.Tests;

public class ConfigServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips_AllFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcnb_test_{Guid.NewGuid():N}.json");
        try
        {
            var config = new AppConfig
            {
                DestinationDrive = "N:",
                DestinationUnc = @"\\FileServer\John",
                Folders = new List<string> { "Desktop", "Documents" },
                IntervalMinutes = 15,
                Enabled = true,
                StartWithWindows = false,
            };

            ConfigService.SaveTo(path, config);
            var loaded = ConfigService.LoadFrom(path);

            Assert.Equal(config.DestinationDrive, loaded.DestinationDrive);
            Assert.Equal(config.DestinationUnc, loaded.DestinationUnc);
            Assert.Equal(config.Folders, loaded.Folders);
            Assert.Equal(config.IntervalMinutes, loaded.IntervalMinutes);
            Assert.Equal(config.Enabled, loaded.Enabled);
            Assert.Equal(config.StartWithWindows, loaded.StartWithWindows);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MalformedFile_ReturnsDisabledDefaultConfig_InsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcnb_test_bad_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not valid json ]]]");
        try
        {
            var loaded = ConfigService.LoadFrom(path);
            Assert.False(loaded.Enabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsFreshDefaultConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcnb_test_missing_{Guid.NewGuid():N}.json");
        var loaded = ConfigService.LoadFrom(path);
        Assert.False(loaded.Enabled);
        Assert.Empty(loaded.Folders);
    }
}
