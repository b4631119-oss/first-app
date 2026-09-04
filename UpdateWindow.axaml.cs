using System;
using System.Threading;
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

    private void LaterButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void UpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_update is null)
            return;

        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressBar.IsVisible = true;

        _downloadCancellation = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(value =>
            {
                ProgressBar.Value = value;
            });

            VersionText.Text = $"Скачивание обновления {_update.Version}...";

            var installerPath = await UpdateService.DownloadUpdateAsync(
                _update,
                progress,
                _downloadCancellation.Token);

            VersionText.Text = "Обновление скачано.\nЗапускаю установщик...";

            UpdateService.LaunchInstallerAndExit(installerPath);
        }
        catch (OperationCanceledException)
        {
            ResetUI();
            VersionText.Text = "Обновление отменено.";
        }
        catch (UpdateCheckException ex)
        {
            ResetUI();
            ShowError("Ошибка проверки обновлений", ex.Message);
        }
        catch (UpdateDownloadException ex)
        {
            ResetUI();
            ShowError("Ошибка скачивания", ex.Message);
        }
        catch (Exception ex)
        {
            ResetUI();
            ShowError("Неожиданная ошибка", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ResetUI()
    {
        UpdateButton.IsEnabled = true;
        LaterButton.IsEnabled = true;
        ProgressBar.IsVisible = false;
        ProgressBar.Value = 0;
    }

    private void ShowError(string title, string message)
    {
        VersionText.Text = $"{title}:\n\n{message}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _downloadCancellation?.Cancel();
        _downloadCancellation?.Dispose();

        base.OnClosed(e);
    }
}