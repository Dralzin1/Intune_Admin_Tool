namespace IntuneAdminTool.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntuneAdminTool.Services;

public partial class WindowsUpdatesViewModel : ObservableObject
{
    private readonly IGraphService _graphService;

    [ObservableProperty]
    private ObservableCollection<WindowsUpdateRing> _updateRings = [];

    [ObservableProperty]
    private ObservableCollection<WindowsFeatureUpdate> _featureUpdates = [];

    [ObservableProperty]
    private ObservableCollection<WindowsDriverUpdate> _driverUpdates = [];

    [ObservableProperty]
    private WindowsUpdateRing? _selectedRing;

    [ObservableProperty]
    private WindowsFeatureUpdate? _selectedFeatureUpdate;

    [ObservableProperty]
    private WindowsDriverUpdate? _selectedDriverUpdate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _selectedTabIndex;

    public WindowsUpdatesViewModel(IGraphService graphService)
    {
        _graphService = graphService;
    }

    [RelayCommand]
    private async Task LoadWindowsUpdatesAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var errors = new List<string>();

            var ringsTask = SafeLoadAsync(_graphService.GetWindowsUpdateRingsAsync);
            var featureTask = SafeLoadAsync(_graphService.GetWindowsFeatureUpdatesAsync);
            var driverTask = SafeLoadAsync(_graphService.GetWindowsDriverUpdatesAsync);

            await Task.WhenAll(ringsTask, featureTask, driverTask);

            UpdateRings = new ObservableCollection<WindowsUpdateRing>(ringsTask.Result.Data);
            if (ringsTask.Result.Error != null) errors.Add($"Update Rings: {ringsTask.Result.Error}");

            FeatureUpdates = new ObservableCollection<WindowsFeatureUpdate>(featureTask.Result.Data);
            if (featureTask.Result.Error != null) errors.Add($"Feature Updates: {featureTask.Result.Error}");

            DriverUpdates = new ObservableCollection<WindowsDriverUpdate>(driverTask.Result.Data);
            if (driverTask.Result.Error != null) errors.Add($"Driver Updates: {driverTask.Result.Error}");

            if (errors.Count > 0)
                ErrorMessage = $"Failed to load: {string.Join("; ", errors)}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load Windows Updates data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<(List<T> Data, string? Error)> SafeLoadAsync<T>(Func<Task<List<T>>> loader)
    {
        try
        {
            return (await loader(), null);
        }
        catch (Exception ex)
        {
            return ([], ex.Message);
        }
    }
}
