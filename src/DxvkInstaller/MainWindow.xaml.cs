using DxvkInstaller.Dialogs;
using DxvkInstaller.Models;
using DxvkInstaller.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DxvkInstaller;

public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ReleaseService _releaseService = new();
    private readonly InstallService _installService = new();

    private readonly AppSettings _appSettings;
    private ApiVerdict? _verdict;
    private string? _exePath;
    private bool _busy;
    private bool _started;

    public MainWindow()
    {
        InitializeComponent();

        _appSettings = _settingsService.Load();
        EditionToggle.IsOn = _appSettings.SelectedEdition == DxvkEdition.GplAsync;
        UpdateConfStatus();

        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 780));
        }
        catch
        {
            // Non-fatal; the default window size is fine.
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await LoadLatestVersionsAsync();
        await HandleResumeArgumentsAsync();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
        };
        picker.FileTypeFilter.Add(".exe");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        _exePath = file.Path;
        ExePathBox.Text = file.Path;
        await RunDetectionAsync();
    }

    private void EditionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _appSettings.SelectedEdition = EditionToggle.IsOn ? DxvkEdition.GplAsync : DxvkEdition.Upstream;
        try
        {
            _settingsService.Save(_appSettings);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    private async void ConfCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = ConfCombo.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        ConfCombo.SelectedIndex = -1; // reset so the same entry can be reopened

        var edition = index == 0 ? DxvkEdition.Upstream : DxvkEdition.GplAsync;
        var currentText = edition == DxvkEdition.Upstream ? _appSettings.UpstreamConfText : _appSettings.GplAsyncConfText;

        var dialog = new ConfEditorDialog(edition, currentText) { XamlRoot = Content.XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (edition == DxvkEdition.Upstream)
            {
                _appSettings.UpstreamConfText = dialog.ConfText;
            }
            else
            {
                _appSettings.GplAsyncConfText = dialog.ConfText;
            }

            try
            {
                _settingsService.Save(_appSettings);
            }
            catch
            {
                // Best-effort persistence.
            }

            UpdateConfStatus();
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        await InstallCoreAsync();
    }

    private async Task RunDetectionAsync()
    {
        StatusText.Text = "Scanning executable…";
        try
        {
            _verdict = await ApiDetector.AnalyzeAsync(_exePath!);
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _verdict = null;
            StatusText.Text = string.Empty;
            ShowResult(InfoBarSeverity.Error, "Could not read the executable", ex.Message);
        }

        UpdateDetectionUI();
    }

    private void UpdateDetectionUI()
    {
        AnticheatBar.IsOpen = false;

        if (_verdict is null)
        {
            DetectionBar.IsOpen = false;
            InstallButton.IsEnabled = false;
            return;
        }

        if (_verdict.CanInstall)
        {
            var apiName = _verdict.Api switch
            {
                GameApi.Direct3D8 => "Direct3D 8",
                GameApi.Direct3D9 => "Direct3D 9",
                GameApi.Direct3D10 => "Direct3D 10",
                GameApi.Direct3D11 => "Direct3D 11",
                _ => "Direct3D"
            };

            var via = string.IsNullOrEmpty(_verdict.DetectedVia) ? string.Empty : $" (detected via {_verdict.DetectedVia})";
            DetectionBar.Severity = InfoBarSeverity.Success;
            DetectionBar.Title = $"{apiName} detected | {_verdict.ArchLabel}{via}";
            DetectionBar.Message = $"Will install: {string.Join(", ", _verdict.FilesToInstall)} ({_verdict.ReleaseArchFolder}) next to the executable.";
            DetectionBar.IsOpen = true;
            InstallButton.IsEnabled = !_busy;

            var gameDirectory = Path.GetDirectoryName(_exePath!);
            if (gameDirectory is not null)
            {
                var hits = AnticheatScan.Scan(gameDirectory);
                if (hits.Count > 0)
                {
                    AnticheatBar.Title = "Possible anticheat files found";
                    AnticheatBar.Message = $"Found: {string.Join(", ", hits)}. DXVK installs in anticheat titles are ban risk. Prefer upstream DXVK, and avoid the async build in online play.";
                    AnticheatBar.IsOpen = true;
                }
            }
        }
        else
        {
            DetectionBar.Severity = InfoBarSeverity.Error;
            DetectionBar.Title = "Cannot install DXVK for this executable";
            DetectionBar.Message = _verdict.Reason;
            DetectionBar.IsOpen = true;
            InstallButton.IsEnabled = false;
        }
    }

    private async Task InstallCoreAsync()
    {
        if (_busy || _exePath is null || _verdict is null || !_verdict.CanInstall)
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(_exePath)!;
        var edition = EditionToggle.IsOn ? DxvkEdition.GplAsync : DxvkEdition.Upstream;

        var conflicts = _installService.GetConflicts(targetDirectory, _verdict.FilesToInstall);
        if (conflicts.Count > 0)
        {
            var conflictDialog = new FileConflictDialog(conflicts) { XamlRoot = Content.XamlRoot };
            var choice = await conflictDialog.ShowAsync();

            if (choice == ContentDialogResult.Secondary)
            {
                try
                {
                    var removed = _installService.UninstallDxvkFiles(targetDirectory);
                    ShowResult(
                        InfoBarSeverity.Success,
                        "Uninstalled",
                        removed.Count > 0 ? $"Removed: {string.Join(", ", removed)}" : "No DXVK files were present.");
                }
                catch (Exception ex)
                {
                    ShowResult(InfoBarSeverity.Error, "Uninstall failed", ex.Message);
                }

                return;
            }

            if (choice != ContentDialogResult.Primary)
            {
                return; // Cancel: do nothing
            }
        }

        SetBusy(true);
        try
        {
            StatusText.Text = "Fetching latest release info…";
            var release = await _releaseService.GetLatestAsync(edition);

            if (!ElevationHelper.CanWriteTo(targetDirectory))
            {
                SetBusy(false);
                var elevationDialog = new ContentDialog
                {
                    Title = "Administrator rights required",
                    Content = $"The app cannot write to:\n{targetDirectory}\n\nRelaunch as administrator and continue the install?",
                    PrimaryButtonText = "Relaunch as administrator",
                    CloseButtonText = "Cancel",
                    XamlRoot = Content.XamlRoot
                };

                if (await elevationDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ElevationHelper.RelaunchElevated(_exePath, edition);
                    Application.Current.Exit();
                }

                return;
            }

            StatusText.Text = $"Downloading {release.Tag}…";
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
            var progress = new Progress<double>(fraction => ProgressBar.Value = fraction * 100);
            var archivePath = await _releaseService.DownloadAsync(release, progress);

            StatusText.Text = "Extracting…";
            var extractDirectory = Path.Combine(Path.GetDirectoryName(archivePath)!, "extracted");
            await ArchiveExtractor.ExtractAsync(archivePath, extractDirectory);
            var archDirectory = ArchiveExtractor.FindArchDirectory(extractDirectory, _verdict.ReleaseArchFolder)
                ?? throw new InvalidOperationException("The downloaded archive does not contain the expected x64/x32 DLLs.");

            StatusText.Text = "Installing files…";
            var confText = edition == DxvkEdition.Upstream ? _appSettings.UpstreamConfText : _appSettings.GplAsyncConfText;
            var outcome = _installService.OverwriteInstall(targetDirectory, archDirectory, _verdict.FilesToInstall, confText);

            _settingsService.AppendInstallRecord(new InstallRecord(
                DateTimeOffset.UtcNow,
                _exePath,
                targetDirectory,
                edition.ToString(),
                release.Tag,
                outcome.CopiedFiles,
                outcome.ConfWritten));

            var confNote = outcome.ConfWritten
                ? "dxvk.conf written."
                : "dxvk.conf not created (profile empty; any existing file was left alone).";
            ShowResult(
                InfoBarSeverity.Success,
                $"Installed {release.Tag}",
                $"Folder: {targetDirectory}\nFiles: {string.Join(", ", outcome.CopiedFiles)}\n{confNote}");
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Empty;
            ShowResult(InfoBarSeverity.Error, "Install failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadLatestVersionsAsync()
    {
        try
        {
            var upstream = await _releaseService.GetLatestAsync(DxvkEdition.Upstream);
            var gplAsync = await _releaseService.GetLatestAsync(DxvkEdition.GplAsync);
            VersionText.Text = $"Latest: upstream {upstream.Tag} | GPLAsync {gplAsync.Tag}";
        }
        catch
        {
            VersionText.Text = "Latest versions could not be checked.";
        }
    }

    private async Task HandleResumeArgumentsAsync()
    {
        var arguments = Environment.GetCommandLineArgs();
        string? resumeExe = null;

        for (var i = 1; i < arguments.Length; i++)
        {
            if (arguments[i] == "--resume" && i + 1 < arguments.Length)
            {
                resumeExe = arguments[i + 1];
                i++;
            }
            else if (arguments[i] == "--edition" && i + 1 < arguments.Length
                     && Enum.TryParse<DxvkEdition>(arguments[i + 1], ignoreCase: true, out var edition))
            {
                _appSettings.SelectedEdition = edition;
                EditionToggle.IsOn = edition == DxvkEdition.GplAsync;
                i++;
            }
        }

        if (resumeExe is null || !File.Exists(resumeExe))
        {
            return;
        }

        _exePath = resumeExe;
        ExePathBox.Text = resumeExe;
        await RunDetectionAsync();

        if (_verdict?.CanInstall == true)
        {
            await InstallCoreAsync();
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BrowseButton.IsEnabled = !busy;
        InstallButton.IsEnabled = !busy && _verdict?.CanInstall == true;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Value = 0;
        }
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private void UpdateConfStatus()
    {
        ConfStatusText.Text =
            $"Upstream: {DescribeProfile(_appSettings.UpstreamConfText)} | GPLAsync: {DescribeProfile(_appSettings.GplAsyncConfText)} | an empty profile means no dxvk.conf is created";
    }

    private static string DescribeProfile(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "empty";
        }

        var lineCount = text.Replace("\r\n", "\n").Trim().Split('\n').Length;
        return $"{lineCount} line(s)";
    }
}
