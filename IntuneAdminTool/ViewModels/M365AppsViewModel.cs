namespace IntuneAdminTool.ViewModels;

using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntuneAdminTool.Services;

public partial class M365AppsViewModel : ObservableObject
{
    private readonly IGraphService _graphService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Dashboard summary
    [ObservableProperty]
    private int _totalDevices;

    [ObservableProperty]
    private int _upToDateDevices;

    [ObservableProperty]
    private int _outOfDateDevices;

    [ObservableProperty]
    private int _unknownDevices;

    [ObservableProperty]
    private string? _latestVersion;

    [ObservableProperty]
    private int _uniqueVersionCount;

    [ObservableProperty]
    private ObservableCollection<M365VersionSummary> _versionSummary = [];

    // Device detail list
    [ObservableProperty]
    private ObservableCollection<M365AppDeviceItem> _deviceDetails = [];

    [ObservableProperty]
    private M365AppDeviceItem? _selectedDevice;

    public M365AppsViewModel(IGraphService graphService)
    {
        _graphService = graphService;
    }

    [RelayCommand]
    private async Task LoadM365AppsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Fetching latest version info and detected apps...";

            // Run the version check and device fetch concurrently
            var versionTask = FetchLatestVersionAsync();
            var devicesTask = FetchDetectedDevicesAsync();

            await Task.WhenAll(versionTask, devicesTask);

            var latestVersion = versionTask.Result;
            var devices = devicesTask.Result;

            LatestVersion = latestVersion ?? "Unable to determine";

            StatusMessage = "Processing results...";

            // Build device items
            var items = devices
                .OrderBy(d => d.DeviceName)
                .Select(d =>
                {
                    string status;
                    if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(d.AppVersion))
                        status = "Unknown";
                    else
                        status = CompareVersions(d.AppVersion, latestVersion) < 0 ? "Out of Date" : "Up to Date";

                    return new M365AppDeviceItem(
                        d.DeviceName ?? "",
                        d.DeviceId ?? "",
                        d.AppVersion ?? "Unknown",
                        d.AppDisplayName ?? "",
                        latestVersion ?? "",
                        status,
                        d.UserPrincipalName ?? "",
                        DeriveUpdateChannel(d.AppDisplayName));
                }).ToList();

            // Populate collections
            DeviceDetails = new ObservableCollection<M365AppDeviceItem>(items);

            // Dashboard metrics
            TotalDevices = items.Count;
            UpToDateDevices = items.Count(i => i.Status == "Up to Date");
            OutOfDateDevices = items.Count(i => i.Status == "Out of Date");
            UnknownDevices = items.Count(i => i.Status == "Unknown");

            // Version summary
            var versionGroups = items
                .GroupBy(i => i.InstalledVersion)
                .OrderByDescending(g => g.Count())
                .Select(g => new M365VersionSummary(
                    g.Key,
                    g.Count(),
                    !string.IsNullOrEmpty(latestVersion) && g.Key != "Unknown"
                        ? CompareVersions(g.Key, latestVersion) < 0 ? "Out of Date" : "Current"
                        : "Unknown"))
                .ToList();

            VersionSummary = new ObservableCollection<M365VersionSummary>(versionGroups);
            UniqueVersionCount = versionGroups.Count;
            StatusMessage = $"{items.Count} device(s) loaded.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load Microsoft 365 Apps data: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<string?> FetchLatestVersionAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await httpClient.GetStringAsync(
                "https://clients.config.office.net/releases/v1.0/OfficeReleases");
            var match = System.Text.RegularExpressions.Regex.Match(response,
                "\"latestVersion\"\\s*:\\s*\"([^\"]+)\"");
            if (match.Success) return match.Groups[1].Value;
        }
        catch { }
        return null;
    }

    private async Task<List<DetectedAppDevice>> FetchDetectedDevicesAsync()
    {
        var result = await _graphService.GetDetectedAppDevicesAsync("Microsoft 365 Apps");
        if (result.Count > 0)
            return result;

        return await _graphService.GetDetectedAppDevicesAsync("Microsoft Office");
    }

    private static int CompareVersions(string installed, string latest)
    {
        var installedParts = installed.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var latestParts = latest.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

        var maxLength = Math.Max(installedParts.Length, latestParts.Length);
        for (int i = 0; i < maxLength; i++)
        {
            var a = i < installedParts.Length ? installedParts[i] : 0;
            var b = i < latestParts.Length ? latestParts[i] : 0;
            if (a < b) return -1;
            if (a > b) return 1;
        }
        return 0;
    }

    private static string DeriveUpdateChannel(string? appDisplayName)
    {
        if (string.IsNullOrEmpty(appDisplayName)) return "Unknown";

        var name = appDisplayName.ToLowerInvariant();
        if (name.Contains("current channel (preview)") || name.Contains("cc (preview)"))
            return "Current Channel (Preview)";
        if (name.Contains("current channel") || name.Contains("monthly"))
            return "Current Channel";
        if (name.Contains("monthly enterprise"))
            return "Monthly Enterprise Channel";
        if (name.Contains("semi-annual enterprise (preview)") || name.Contains("sac (preview)"))
            return "Semi-Annual Enterprise Channel (Preview)";
        if (name.Contains("semi-annual enterprise") || name.Contains("semi-annual") || name.Contains("sac"))
            return "Semi-Annual Enterprise Channel";
        if (name.Contains("beta"))
            return "Beta Channel";
        if (name.Contains("ltsc") || name.Contains("perpetual"))
            return "LTSC";

        return "Unknown";
    }
}

public record M365AppDeviceItem(
    string DeviceName,
    string DeviceId,
    string InstalledVersion,
    string AppDisplayName,
    string LatestVersion,
    string Status,
    string UserPrincipalName,
    string UpdateChannel);

public record M365VersionSummary(
    string Version,
    int DeviceCount,
    string Status);
