using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyFirstApp;

public partial class UpdateWindow : Window
{
    private UpdateInfo? _update;
    private CancellationTokenSource? _downloadCancellation;

    public UpdateWindow()
    {
        InitializeComponent();
    }

    public UpdateWindow(UpdateInfo update)
    {
        InitializeComponent();

        _update = update;

        VersionText.Text =
            $"Новая версия {_update.Version} доступна.\n\n" +
            $"Текущая версия: {UpdateService.CurrentVersion}\n" +
            $"Новая версия: {_update.Version}";
    }

    private void LaterButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private async void UpdateButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_update is null)
            return;

        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressBar.IsVisible = true;

        _downloadCancellation = new CancellationTokenSource();

        try
        {
            Progress<double> progress = new(value =>
            {
                ProgressBar.Value = value;
            });

            string installerPath =
                await UpdateService.DownloadUpdateAsync(
                    _update,
                    progress,
                    _downloadCancellation.Token);

            UpdateService.LaunchInstallerAndExit(installerPath);
        }
        catch (OperationCanceledException)
        {
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            ProgressBar.IsVisible = false;
        }
        catch
        {
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            ProgressBar.IsVisible = false;

            VersionText.Text =
                "Не удалось скачать обновление.\n\n" +
                "Попробуйте позже.";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _downloadCancellation?.Cancel();
        _downloadCancellation?.Dispose();

        base.OnClosed(e);
    }
}