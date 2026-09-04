using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MyFirstApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = new();
            desktop.MainWindow = mainWindow;

            mainWindow.Opened += async (_, _) =>
            {
                await CheckForUpdatesAsync(mainWindow);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task CheckForUpdatesAsync(
        MainWindow owner)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            UpdateInfo? update =
                await UpdateService.CheckForUpdateAsync();

            if (update is null)
                return;

            UpdateWindow updateWindow =
                new(update);

            await updateWindow.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            // Обновление не должно ломать запуск приложения.
            System.Diagnostics.Trace.WriteLine($"Update check failed: {ex}");
        }
    }
}