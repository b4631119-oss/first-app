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
    string Sha256
);

public static class UpdateService
{
    private const string Repository = "b4631119-oss/first-app";

    private static readonly HttpClient Http = CreateHttpClient();

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
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"https://api.github.com/repos/{Repository}/releases/latest");

        using HttpResponseMessage response =
            await Http.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using Stream stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;

        string tagName =
            root.GetProperty("tag_name").GetString()
            ?? throw new InvalidOperationException(
                "GitHub release tag is missing.");

        string versionText = tagName.TrimStart('v');

        if (!Version.TryParse(
                versionText,
                out Version? latestVersion))
        {
            throw new InvalidOperationException(
                $"Invalid GitHub release version: {versionText}");
        }

        if (latestVersion <= CurrentVersion)
        {
            return null;
        }

        if (!root.TryGetProperty(
                "assets",
                out JsonElement assets))
        {
            throw new InvalidOperationException(
                "GitHub release assets are missing.");
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name =
                asset.GetProperty("name").GetString()
                ?? string.Empty;

            if (!name.EndsWith(
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!name.StartsWith(
                    "MyFirstApp-Setup-",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string downloadUrl =
                asset.GetProperty("browser_download_url").GetString()
                ?? throw new InvalidOperationException(
                    "Download URL is missing.");

            if (!asset.TryGetProperty(
                    "digest",
                    out JsonElement digestElement))
            {
                throw new InvalidOperationException(
                    "GitHub release does not contain a SHA-256 digest.");
            }

            string sha256 =
                digestElement.GetString()
                ?? throw new InvalidOperationException(
                    "SHA-256 digest is missing.");

            if (sha256.StartsWith(
                    "sha256:",
                    StringComparison.OrdinalIgnoreCase))
            {
                sha256 = sha256["sha256:".Length..];
            }

            return new UpdateInfo(
                latestVersion,
                downloadUrl,
                name,
                sha256);
        }

        return null;
    }

    public static async Task<string> DownloadUpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "MyFirstApp",
            "Updates");

        Directory.CreateDirectory(tempDirectory);

        string destinationPath =
            Path.Combine(tempDirectory, update.FileName);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            update.DownloadUrl);

        request.Headers.Accept.ParseAdd(
            "application/octet-stream");

        using HttpResponseMessage response =
            await Http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        long? totalBytes =
            response.Content.Headers.ContentLength;

        await using Stream input =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        await using FileStream output = new(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        byte[] buffer = new byte[128 * 1024];
        long totalRead = 0;

        while (true)
        {
            int read = await input.ReadAsync(
                buffer,
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);

            totalRead += read;

            if (totalBytes is > 0)
            {
                progress?.Report(
                    Math.Clamp(
                        (double)totalRead / totalBytes.Value,
                        0,
                        1));
            }
        }

        await output.FlushAsync(cancellationToken);

        string actualSha256;

        await using (FileStream verifyStream =
                     File.OpenRead(destinationPath))
        {
            byte[] hash =
                await SHA256.HashDataAsync(
                    verifyStream,
                    cancellationToken);

            actualSha256 =
                Convert.ToHexString(hash).ToLowerInvariant();
        }

        if (!string.Equals(
                actualSha256,
                update.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);

            throw new InvalidOperationException(
                "Downloaded update failed SHA-256 verification.");
        }

        progress?.Report(1);

        return destinationPath;
    }

    public static void LaunchInstallerAndExit(
        string installerPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows installer updates are only supported on Windows.");
        }

        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException(
                "Downloaded installer was not found.",
                installerPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });

        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes
                .IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MyFirstApp/1.0");

        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/vnd.github+json");

        return client;
    }
}