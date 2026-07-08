using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace JiaoLongControl.Server
{
    public class InnoUpdater
    {
        private readonly string _currentVersion;
        private const string RepoApiUrl = "https://api.github.com/repos/GaoXanSheng/JiaolongControl/releases/latest";

        public InnoUpdater(string currentVersion)
        {
            _currentVersion = currentVersion;
        }

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                string jsonResponse = await client.GetStringAsync(RepoApiUrl);
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                string latestVersion = root.GetProperty("tag_name").GetString() ?? "";
                if (string.IsNullOrEmpty(latestVersion)) return;
                if (!IsNewerVersion(latestVersion, _currentVersion))
                {
                    return;
                }

                string skippedVersion = UpdateSettings.GetSkippedVersion();
                if (latestVersion == skippedVersion)
                {
                    return; 
                }
                string releaseNotes = root.GetProperty("body").GetString() ?? "";
                string downloadUrl = "";
                var assets = root.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.EndsWith("_Setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
                if (string.IsNullOrEmpty(downloadUrl)) return;
                bool shouldUpdate = false;
                bool shouldSkip = false;
                string downloadPath = "";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new UpdateDialog(latestVersion, _currentVersion, releaseNotes, downloadUrl);
                    if (dialog.ShowDialog() == true)
                    {
                        if (dialog.Result == UpdateDialog.UpdateChoice.UpdateNow)
                        {
                            shouldUpdate = true;
                            downloadPath = dialog.DownloadedFilePath;
                        }
                        else if (dialog.Result == UpdateDialog.UpdateChoice.SkipVersion)
                        {
                            shouldSkip = true;
                        }
                    }
                });

                if (shouldSkip)
                {
                    UpdateSettings.SaveSkippedVersion(latestVersion);
                    return;
                }

                if (shouldUpdate)
                {
                    ExecuteInstaller(downloadPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新检查失败: {ex.Message}");
            }
        }

        private void ExecuteInstaller(string filePath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装程序执行失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var vLatest = new AppVersion(latest);
                var vCurrent = new AppVersion(current);

                return vLatest.CompareTo(vCurrent) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 自定义版本解析类
        /// </summary>
        private class AppVersion : IComparable<AppVersion>
        {
            public Version BaseVersion { get; }
            public int SuffixPriority { get; } // 1: Alpha(.A), 2: Beta(.B), 3: Stable

            public AppVersion(string versionStr)
            {
                versionStr = versionStr.Trim();
                if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    versionStr = versionStr[1..];

                string suffix = "";
                int lastDot = versionStr.LastIndexOf('.');
                if (lastDot > 0)
                {
                    string lastPart = versionStr[(lastDot + 1)..];
                    if (lastPart is "A" or "B")
                    {
                        suffix = lastPart;
                        versionStr = versionStr[..lastDot];
                    }
                }

                BaseVersion = Version.TryParse(versionStr, out Version? parsedBase)
                    ? parsedBase
                    : new Version(0, 0, 0);
                SuffixPriority = suffix switch
                {
                    "A" => 1,
                    "B" => 2,
                    _ => 3
                };
            }
            public int CompareTo(AppVersion? other)
            {
                if (other == null) return 1;
                int baseComparison = BaseVersion.CompareTo(other.BaseVersion);
                if (baseComparison != 0)
                {
                    return baseComparison;
                }
                return SuffixPriority.CompareTo(other.SuffixPriority);
            }
        }
    }
}