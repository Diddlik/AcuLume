using AcuLume.Gui.Services;

namespace AcuLume.Gui.Tests;

public class UpdateServiceTests
{
    /// <summary>
    /// The test run is not a Velopack install, so the service must say so instead of throwing or
    /// reaching out to GitHub. This is also what a developer running from the build tree sees.
    /// </summary>
    [Fact]
    public async Task Check_ReportsNotInstalled_OutsideAnInstall()
    {
        var service = new UpdateService();

        Assert.False(service.IsInstalled);
        Assert.Equal(UpdateState.NotInstalled, await service.CheckAsync());
    }

    [Fact]
    public void CurrentVersion_FallsBackToTheAssemblyVersion_OutsideAnInstall()
    {
        Assert.Equal(AppInfo.Version, new UpdateService().CurrentVersion);
    }

    [Fact]
    public async Task Download_Fails_WithoutAPendingUpdate()
    {
        Assert.Equal(UpdateState.Failed, await new UpdateService().DownloadAsync());
    }
}
