using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FSTRaK.Utils;
using Newtonsoft.Json.Linq;
using Serilog;

namespace FSTRaK.BusinessLogic.VatsimService
{
    internal class DataFileUpdateService
    {
        // Separate clients: GitHub API requires Accept: vnd.github header; VATSIM/asset downloads must not have it.
        private static readonly HttpClient _githubClient = new HttpClient();
        private static readonly HttpClient _genericClient = new HttpClient();

        private readonly Action _onFilesUpdated;

        static DataFileUpdateService()
        {
            _githubClient.DefaultRequestHeaders.Add("User-Agent", "FSTRaK");
            _githubClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            _genericClient.DefaultRequestHeaders.Add("User-Agent", "FSTRaK");
        }

        public DataFileUpdateService(Action onFilesUpdated)
        {
            _onFilesUpdated = onFilesUpdated;
        }

        public async Task UpdateDataFilesAsync()
        {
            var dataDir = Path.Combine(PathUtil.GetApplicationLocalDataPath(), "Data");
            Directory.CreateDirectory(dataDir);

            bool anyUpdated = false;
            anyUpdated |= await UpdateFirBoundariesAsync(dataDir);
            anyUpdated |= await UpdateTraconBoundariesAsync(dataDir);
            anyUpdated |= await UpdateVatSpyDatAsync(dataDir);

            // Only reload if at least one file was newly downloaded
            if (anyUpdated)
                _onFilesUpdated?.Invoke();
        }

        // Returns true if the file was downloaded (new or updated).
        private async Task<bool> UpdateFirBoundariesAsync(string dataDir)
        {
            const string filename = "Boundaries.geojson";
            var localPath = Path.Combine(dataDir, filename);
            var storedTag = Properties.Settings.Default.FirBoundaryReleaseTag;

            try
            {
                var (latestTag, downloadUrl) = await GetLatestGitHubReleaseAssetAsync(
                    "vatsimnetwork", "vatspy-data-project", filename);

                if (latestTag == storedTag && File.Exists(localPath))
                {
                    Log.Debug("FIR boundaries up to date (tag: {Tag})", latestTag);
                    return false;
                }

                Log.Information("Downloading FIR boundaries {Filename} (tag: {Tag})", filename, latestTag);
                await DownloadFileAtomicAsync(downloadUrl, localPath);
                Properties.Settings.Default.FirBoundaryReleaseTag = latestTag;
                Properties.Settings.Default.Save();
                Log.Information("FIR boundaries updated successfully (tag: {Tag})", latestTag);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update FIR boundaries ({Filename})", filename);
                if (!File.Exists(localPath))
                    Log.Error("No local fallback available for {Filename}", filename);
                return false;
            }
        }

        // Returns true if the file was downloaded (new or updated).
        private async Task<bool> UpdateTraconBoundariesAsync(string dataDir)
        {
            const string filename = "TRACONBoundaries.geojson";
            var localPath = Path.Combine(dataDir, filename);
            var storedTag = Properties.Settings.Default.TraconBoundaryReleaseTag;

            try
            {
                var (latestTag, downloadUrl) = await GetLatestGitHubReleaseAssetAsync(
                    "vatsimnetwork", "simaware-tracon-project", filename);

                if (latestTag == storedTag && File.Exists(localPath))
                {
                    Log.Debug("TRACON boundaries up to date (tag: {Tag})", latestTag);
                    return false;
                }

                Log.Information("Downloading TRACON boundaries {Filename} (tag: {Tag})", filename, latestTag);
                await DownloadFileAtomicAsync(downloadUrl, localPath);
                Properties.Settings.Default.TraconBoundaryReleaseTag = latestTag;
                Properties.Settings.Default.Save();
                Log.Information("TRACON boundaries updated successfully (tag: {Tag})", latestTag);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update TRACON boundaries ({Filename})", filename);
                if (!File.Exists(localPath))
                    Log.Error("No local fallback available for {Filename}", filename);
                return false;
            }
        }

        // Returns true if the file was downloaded (new or updated).
        private async Task<bool> UpdateVatSpyDatAsync(string dataDir)
        {
            const string filename = "VATSpy.dat";
            var localPath = Path.Combine(dataDir, filename);
            var storedTag = Properties.Settings.Default.VatSpyReleaseTag;

            try
            {
                var (latestTag, downloadUrl) = await GetLatestGitHubReleaseAssetAsync(
                    "vatsimnetwork", "vatspy-data-project", filename);

                if (latestTag == storedTag && File.Exists(localPath))
                {
                    Log.Debug("VATSpy.dat up to date (tag: {Tag})", latestTag);
                    return false;
                }

                Log.Information("Downloading VATSpy.dat (tag: {Tag})", latestTag);
                await DownloadFileAtomicAsync(downloadUrl, localPath);
                Properties.Settings.Default.VatSpyReleaseTag = latestTag;
                Properties.Settings.Default.Save();
                Log.Information("VATSpy.dat updated successfully (tag: {Tag})", latestTag);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update VATSpy.dat");
                if (!File.Exists(localPath))
                    Log.Error("No local fallback available for VATSpy.dat");
                return false;
            }
        }

        private async Task<(string tag, string downloadUrl)> GetLatestGitHubReleaseAssetAsync(
            string owner, string repo, string assetFilename)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            var json = await _githubClient.GetStringAsync(url);
            var release = JObject.Parse(json);
            var tag = release["tag_name"]?.ToString()
                ?? throw new Exception($"tag_name missing in GitHub release for {owner}/{repo}");

            foreach (var asset in release["assets"])
            {
                if (asset["name"]?.ToString() == assetFilename)
                {
                    var downloadUrl = asset["browser_download_url"]?.ToString();
                    if (string.IsNullOrEmpty(downloadUrl))
                        throw new Exception($"browser_download_url missing for asset '{assetFilename}' in release {tag}");
                    return (tag, downloadUrl);
                }
            }

            throw new Exception($"Asset '{assetFilename}' not found in release {tag} of {owner}/{repo}");
        }

        // Downloads to a temp file first, then atomically replaces destination.
        // Preserves any existing good file if the download fails mid-write.
        private async Task DownloadFileAtomicAsync(string url, string destinationPath)
        {
            var tempPath = destinationPath + ".tmp";
            try
            {
                var response = await _genericClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(tempPath, content);
                File.Copy(tempPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
