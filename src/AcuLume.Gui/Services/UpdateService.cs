using Velopack;
using Velopack.Sources;

namespace AcuLume.Gui.Services;

/// <summary>What a check or download left the app in, for the Settings view to report.</summary>
public enum UpdateState
{
    /// <summary>Running from a build tree or an unpacked folder, so there is nothing to update.</summary>
    NotInstalled,
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Failed,
}

/// <summary>
/// Checks GitHub releases for a newer build and applies it. Velopack does the packaging, signing
/// checks and swap; this only decides when to ask and carries the outcome back to the UI.
///
/// Nothing happens without the user pressing a button: no background polling, no automatic download.
/// The app makes no other network requests, so an update check is the single point where it talks to
/// anything outside the machine.
/// </summary>
public sealed class UpdateService
{
    private const string RepositoryUrl = "https://github.com/Diddlik/AcuLume";

    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    public UpdateService()
    {
        try
        {
            _manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
        }
        catch (Exception)
        {
            // A missing or malformed install directory must not stop the app from starting.
            _manager = null;
        }
    }

    /// <summary>False when running from a build tree — Velopack only manages an installed copy.</summary>
    public bool IsInstalled => _manager?.IsInstalled ?? false;

    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? AppInfo.Version;

    public string? AvailableVersion => _pending?.TargetFullRelease.Version.ToString();

    /// <summary>Asks GitHub whether a newer release exists. Returns the resulting state.</summary>
    public async Task<UpdateState> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is not { IsInstalled: true })
        {
            return UpdateState.NotInstalled;
        }

        _pending = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        return _pending is null ? UpdateState.UpToDate : UpdateState.UpdateAvailable;
    }

    /// <summary>
    /// Downloads the pending release. The swap only happens on the next start, so the download is
    /// safe to run while the user keeps working.
    /// </summary>
    public async Task<UpdateState> DownloadAsync(
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_manager is null || _pending is null)
        {
            return UpdateState.Failed;
        }

        await _manager.DownloadUpdatesAsync(_pending, p => progress?.Report(p), cancellationToken)
            .ConfigureAwait(false);
        return UpdateState.ReadyToRestart;
    }

    /// <summary>Applies the downloaded release and restarts. Does not return when it succeeds.</summary>
    public void ApplyAndRestart()
    {
        if (_manager is null || _pending is null)
        {
            return;
        }

        _manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
    }
}
