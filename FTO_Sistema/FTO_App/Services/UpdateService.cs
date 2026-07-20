using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FTO_App.Services
{
    /// <summary>
    /// Verifica e aplica atualizações a partir das Releases do GitHub.
    /// Preserva .env e banco SQLite durante a troca de arquivos.
    /// </summary>
    public static class UpdateService
    {
        public const string GitHubOwner = "kapimkk";
        public const string GitHubRepo = "FTO-Main";
        public const string PreferredAssetName = "FTO_App-win-x64.zip";

        private static readonly string[] PreserveFileNames =
        {
            ".env",
            "FTO.db",
            "FTO.db-shm",
            "FTO.db-wal"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static Version GetLocalVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                string clean = info.Split('+')[0].Trim().TrimStart('v', 'V');
                if (Version.TryParse(NormalizeVersion(clean), out var fromInfo))
                    return fromInfo;
            }

            return asm.GetName().Version ?? new Version(1, 0, 0, 0);
        }

        public static string GetLocalVersionDisplay()
        {
            Version v = GetLocalVersion();
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }

        public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
        {
            using var client = CreateHttpClient();
            string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.Fail(
                    "Nenhuma release encontrada no GitHub.\n\n" +
                    "Crie uma Release com o asset FTO_App-win-x64.zip.");
            }

            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions)
                ?? throw new InvalidOperationException("Resposta inválida da API do GitHub.");

            Version remote = ParseTagVersion(release.TagName);
            Version local = GetLocalVersion();
            bool available = remote > TruncateToThreeParts(local);

            GitHubAsset? asset = FindAsset(release);
            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = available && asset != null,
                LocalVersion = local,
                RemoteVersion = remote,
                ReleaseName = release.Name ?? release.TagName,
                ReleaseNotes = release.Body ?? string.Empty,
                ReleaseUrl = release.HtmlUrl ?? string.Empty,
                DownloadUrl = asset?.BrowserDownloadUrl,
                AssetName = asset?.Name,
                AssetSizeBytes = asset?.Size ?? 0,
                ErrorMessage = asset == null && available
                    ? $"Release {release.TagName} sem o arquivo {PreferredAssetName}."
                    : null
            };
        }

        public static async Task DownloadAndPrepareUpdateAsync(
            UpdateCheckResult check,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            if (check is null || !check.UpdateAvailable || string.IsNullOrWhiteSpace(check.DownloadUrl))
                throw new InvalidOperationException("Não há atualização válida para baixar.");

            string tempRoot = Path.Combine(Path.GetTempPath(), "FTO_Update", Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempRoot, check.AssetName ?? PreferredAssetName);
            string extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractDir);

            progress?.Report("Baixando pacote...");
            using (var client = CreateHttpClient())
            using (var response = await client.GetAsync(check.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            progress?.Report("Extraindo arquivos...");
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            string sourceDir = ResolvePublishRoot(extractDir);
            string targetDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string exePath = Path.Combine(targetDir, "FTO_App.exe");
            string scriptPath = Path.Combine(tempRoot, "ApplyUpdate.ps1");

            File.WriteAllText(scriptPath, BuildApplyUpdateScript());

            progress?.Report("Aplicando atualização...");
            int pid = Environment.ProcessId;
            string args =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                $"-Source \"{sourceDir}\" -Target \"{targetDir}\" -ExePath \"{exePath}\" -WaitPid {pid}";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempRoot
            };

            Process.Start(psi);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FTO-App-Updater");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            string? token = TryReadUpdateToken();
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        private static string? TryReadUpdateToken()
        {
            string? fromEnv = Environment.GetEnvironmentVariable("FTO_UPDATE_TOKEN");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv.Trim();

            string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
            if (!File.Exists(envPath))
                return null;

            foreach (string raw in File.ReadAllLines(envPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                if (!line.StartsWith("FTO_UPDATE_TOKEN=", StringComparison.OrdinalIgnoreCase))
                    continue;

                return line[(line.IndexOf('=') + 1)..].Trim().Trim('"');
            }

            return null;
        }

        private static GitHubAsset? FindAsset(GitHubRelease release)
        {
            if (release.Assets == null || release.Assets.Count == 0)
                return null;

            return release.Assets.FirstOrDefault(a =>
                       a.Name.Equals(PreferredAssetName, StringComparison.OrdinalIgnoreCase))
                   ?? release.Assets.FirstOrDefault(a =>
                       a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                       a.Name.Contains("FTO", StringComparison.OrdinalIgnoreCase))
                   ?? release.Assets.FirstOrDefault(a =>
                       a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolvePublishRoot(string extractDir)
        {
            // Zip pode ter arquivos na raiz ou dentro de FTO_App-win-x64/
            string directExe = Path.Combine(extractDir, "FTO_App.exe");
            if (File.Exists(directExe))
                return extractDir;

            string? nested = Directory.GetDirectories(extractDir)
                .Select(d => new { Dir = d, Exe = Path.Combine(d, "FTO_App.exe") })
                .FirstOrDefault(x => File.Exists(x.Exe))
                ?.Dir;

            if (!string.IsNullOrEmpty(nested))
                return nested;

            throw new DirectoryNotFoundException(
                "Pacote inválido: FTO_App.exe não encontrado no ZIP da release.");
        }

        private static Version ParseTagVersion(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return new Version(0, 0, 0);

            // Aceita v1.2.3 e o formato comum v.1.2.3
            string clean = System.Text.RegularExpressions.Regex.Replace(
                tag.Trim(),
                @"^[vV]\.?",
                "");

            if (Version.TryParse(NormalizeVersion(clean), out var v))
                return TruncateToThreeParts(v);

            return new Version(0, 0, 0);
        }

        private static string NormalizeVersion(string value)
        {
            string[] parts = value.Split('.');
            if (parts.Length >= 3)
                return $"{parts[0]}.{parts[1]}.{parts[2]}";
            if (parts.Length == 2)
                return $"{parts[0]}.{parts[1]}.0";
            if (parts.Length == 1)
                return $"{parts[0]}.0.0";
            return "0.0.0";
        }

        private static Version TruncateToThreeParts(Version v) =>
            new(v.Major, v.Minor, Math.Max(v.Build, 0));

        private static string BuildApplyUpdateScript()
        {
            string preserveList = string.Join(", ", PreserveFileNames.Select(n => $"'{n}'"));
            return $$"""
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][int]$WaitPid
)

$ErrorActionPreference = 'Stop'
$preserve = @({{preserveList}})

if ($WaitPid -gt 0) {
    try { Wait-Process -Id $WaitPid -Timeout 90 -ErrorAction SilentlyContinue } catch {}
}
Start-Sleep -Seconds 2

Get-ChildItem -LiteralPath $Source -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($Source.Length).TrimStart('\', '/')
    if ($preserve -contains $_.Name) { return }

    $dest = Join-Path $Target $rel
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
}

Start-Sleep -Seconds 1
Start-Process -FilePath $ExePath
""";
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = "";

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("assets")]
            public System.Collections.Generic.List<GitHubAsset>? Assets { get; set; }
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = "";

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }
    }

    public sealed class UpdateCheckResult
    {
        public bool Success { get; init; }
        public bool UpdateAvailable { get; init; }
        public Version LocalVersion { get; init; } = new(0, 0, 0);
        public Version RemoteVersion { get; init; } = new(0, 0, 0);
        public string ReleaseName { get; init; } = "";
        public string ReleaseNotes { get; init; } = "";
        public string ReleaseUrl { get; init; } = "";
        public string? DownloadUrl { get; init; }
        public string? AssetName { get; init; }
        public long AssetSizeBytes { get; init; }
        public string? ErrorMessage { get; init; }

        public static UpdateCheckResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };

        public string RemoteVersionDisplay =>
            $"v{RemoteVersion.Major}.{RemoteVersion.Minor}.{RemoteVersion.Build}";

        public string LocalVersionDisplay =>
            $"v{LocalVersion.Major}.{LocalVersion.Minor}.{Math.Max(LocalVersion.Build, 0)}";
    }
}
