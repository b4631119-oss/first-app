using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyFirstApp;

public sealed record UpdateInfo(
    Version Version,
    string DownloadUrl,
    string FileName,
    string? ExpectedSha256
);

public static class UpdateService
{
    private const string Repository = "b4631119-oss/first-app";
    private const string UserAgent = "MyFirstApp-Updater/1.0";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    public static Version CurrentVersion
    {
        get
        {
            string? versionText =
                Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(versionText))
            {
                string cleanVersion = versionText.Split('+')[0];

                if (Version.TryParse(cleanVersion, out Version? version))
                {
                    return version;
                }
            }

            return new Version(1, 0, 0);
        }
    }

    public static async Task<UpdateInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient();

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{Repository}/releases/latest");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            var root = document.RootElement;

            if (root.TryGetProperty("draft", out var draftElement) && draftElement.GetBoolean())
            {
                return null;
            }

            if (root.TryGetProperty("prerelease", out var prereleaseElement) && prereleaseElement.GetBoolean())
            {
                return null;
            }

            var tagName = root.GetProperty("tag_name").GetString()
                ?? throw new InvalidOperationException("GitHub release tag is missing.");

            var versionText = tagName.TrimStart('v');

            if (!Version.TryParse(versionText, out var latestVersion))
            {
                throw new InvalidOperationException($"Invalid GitHub release version: {versionText}");
            }

            if (latestVersion <= CurrentVersion)
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out var assets))
            {
                throw new InvalidOperationException("GitHub release assets are missing.");
            }

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;

                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!name.StartsWith("MyFirstApp-Setup-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var downloadUrl = asset.GetProperty("browser_download_url").GetString()
                    ?? throw new InvalidOperationException("Download URL is missing.");

                string? sha256 = null;
                if (asset.TryGetProperty("digest", out var digestElement))
                {
                    sha256 = digestElement.GetString();
                    if (sha256?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        sha256 = sha256["sha256:".Length..];
                    }
                }

                return new UpdateInfo(latestVersion, downloadUrl, name, sha256);
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateCheckException("Failed to connect to GitHub. Check your internet connection.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new UpdateCheckException("Update check timed out.", ex);
        }
        catch (JsonException ex)
        {
            throw new UpdateCheckException("Failed to parse GitHub release data.", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not UpdateCheckException)
        {
            throw new UpdateCheckException($"Unexpected error checking for updates: {ex.Message}", ex);
        }
    }

    public static async Task<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "MyFirstApp",
            "Updates");

        Directory.CreateDirectory(tempDirectory);

        var destinationPath = Path.Combine(tempDirectory, update.FileName);

        if (File.Exists(destinationPath))
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch
            {
                var newName = Path.Combine(tempDirectory, $"MyFirstApp-Setup-{Guid.NewGuid()}.exe");
                File.Move(destinationPath, newName);
                destinationPath = newName;
            }
        }

        using var httpClient = CreateHttpClient();
        httpClient.Timeout = DownloadTimeout;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
            request.Headers.Accept.ParseAdd("application/octet-stream");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            if (response.RequestMessage?.RequestUri != null &&
                !response.RequestMessage.RequestUri.Equals(new Uri(update.DownloadUrl)))
            {
                var finalUrl = response.RequestMessage.RequestUri.ToString();
                if (!finalUrl.Contains("github.com") && !finalUrl.Contains("githubassets.com"))
                {
                    throw new InvalidOperationException($"Download redirected to unexpected domain: {finalUrl}");
                }
            }

            var totalBytes = response.Content.Headers.ContentLength;

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var output = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[128 * 1024];
            long totalRead = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await input.ReadAsync(buffer, cancellationToken);

                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                totalRead += read;

                if (totalBytes > 0)
                {
                    progress?.Report(Math.Clamp((double)totalRead / totalBytes.Value, 0, 1));
                }
            }

            await output.FlushAsync(cancellationToken);

            if (!File.Exists(destinationPath))
            {
                throw new FileNotFoundException("Downloaded file not found after write.", destinationPath);
            }

            var fileInfo = new FileInfo(destinationPath);
            if (fileInfo.Length == 0)
            {
                File.Delete(destinationPath);
                throw new InvalidOperationException("Downloaded installer is empty.");
            }

            if (update.ExpectedSha256 != null)
            {
                var actualSha256 = await ComputeSha256Async(destinationPath, cancellationToken);

                if (!string.Equals(actualSha256, update.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(destinationPath);
                    throw new InvalidOperationException(
                        $"SHA-256 verification failed. Expected: {update.ExpectedSha256}, Got: {actualSha256}");
                }
            }
            else
            {
                var actualSha256 = await ComputeSha256Async(destinationPath, cancellationToken);
                Console.WriteLine($"[Updater] Downloaded installer SHA-256 (no reference to verify): {actualSha256}");
            }

            progress?.Report(1.0);

            return destinationPath;
        }
        catch (HttpRequestException ex)
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw new UpdateDownloadException("Failed to download update. Check your internet connection.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw new UpdateDownloadException("Download timed out.", ex);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw;
        }
        catch (Exception ex) when (ex is not UpdateDownloadException)
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            throw new UpdateDownloadException($"Unexpected error downloading update: {ex.Message}", ex);
        }
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows installer updates are only supported on Windows.");
        }

        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Downloaded installer was not found.", installerPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("Installation requires administrator privileges. The operation was cancelled by the user.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch installer: {ex.Message}", ex);
        }

        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = HttpTimeout
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        return client;
    }
}

public class UpdateCheckException : Exception
{
    public UpdateCheckException(string message, Exception? inner = null) : base(message, inner) { }
}

public class UpdateDownloadException : Exception
{
    public UpdateDownloadException(string message, Exception? inner = null) : base(message, inner) { }
}