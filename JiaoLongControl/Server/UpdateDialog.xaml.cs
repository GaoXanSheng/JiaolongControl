using System.IO;
using System.Net.Http;
using System.Windows;

namespace JiaoLongControl.Server
{
    public partial class UpdateDialog : Window
    {
        public enum UpdateChoice
        {
            UpdateNow,
            SkipVersion,
            Later
        }

        /// <summary>
        /// 中国大陆地区下载镜像前缀,拼接在 GitHub 下载地址前
        /// </summary>
        private const string MirrorPrefix = "https://gh-proxy.org/";

        private readonly string _downloadUrl;
        private CancellationTokenSource? _cts;

        public UpdateDialog(string newVersion, string currentVersion, string releaseNotes, string downloadUrl)
        {
            InitializeComponent();
            _downloadUrl = downloadUrl;

            TxtCurrentVersion.Text = currentVersion;
            TxtNewVersion.Text = newVersion;
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(releaseNotes)
                ? "此版本无更新日志。"
                : releaseNotes;
        }

        public UpdateChoice Result { get; private set; } = UpdateChoice.Later;
        public string DownloadedFilePath { get; private set; } = "";

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync(_downloadUrl);
        }

        private async void BtnMirrorDownload_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync(MirrorPrefix + _downloadUrl);
        }

        private async Task StartDownloadAsync(string url)
        {
            bool isMirror = url != _downloadUrl;

            ButtonPanel.Visibility = Visibility.Collapsed;
            DownloadPanel.Visibility = Visibility.Visible;

            try
            {
                DownloadedFilePath = await DownloadAsync(url);
            }
            catch (OperationCanceledException)
            {
                ButtonPanel.Visibility = Visibility.Visible;
                DownloadPanel.Visibility = Visibility.Collapsed;
                return;
            }
            catch (Exception ex)
            {
                ButtonPanel.Visibility = Visibility.Visible;
                DownloadPanel.Visibility = Visibility.Collapsed;

                if (isMirror)
                {
                    MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show(
                        $"下载失败: {ex.Message}\n\n是否使用中国大陆镜像重试?",
                        "下载失败", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await StartDownloadAsync(MirrorPrefix + _downloadUrl);
                }
                return;
            }

            Result = UpdateChoice.UpdateNow;
            DialogResult = true;
            Close();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateChoice.SkipVersion;
            DialogResult = true;
            Close();
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateChoice.Later;
            DialogResult = false;
            Close();
        }

        private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private async Task<string> DownloadAsync(string url)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            string tempPath = Path.Combine(Path.GetTempPath(), "JiaoLongControl_Update_Setup.exe");

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long downloadedBytes = 0;
            int bytesRead;
            var lastReport = DateTime.MinValue;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead, token);
                downloadedBytes += bytesRead;

                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds < 150 && downloadedBytes < totalBytes)
                    continue;
                lastReport = now;

                var progress = new ProgressInfo(downloadedBytes, totalBytes);
                UpdateProgress(progress);
            }

            return tempPath;
        }

        private void UpdateProgress(ProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info.TotalBytes > 0)
                {
                    DownloadProgress.IsIndeterminate = false;
                    DownloadProgress.Value = (double)info.DownloadedBytes / info.TotalBytes * 100;
                    TxtDownloadStatus.Text =
                        $"下载中... {FormatBytes(info.DownloadedBytes)} / {FormatBytes(info.TotalBytes)}";
                }
                else
                {
                    DownloadProgress.IsIndeterminate = true;
                    TxtDownloadStatus.Text = $"下载中... {FormatBytes(info.DownloadedBytes)}";
                }
            });
        }

        private static string FormatBytes(long bytes)
        {
            return bytes switch
            {
                >= 1048576 => $"{bytes / 1048576.0:F1} MB",
                >= 1024 => $"{bytes / 1024.0:F0} KB",
                _ => $"{bytes} B"
            };
        }

        private readonly struct ProgressInfo
        {
            public long DownloadedBytes { get; }
            public long TotalBytes { get; }

            public ProgressInfo(long downloadedBytes, long totalBytes)
            {
                DownloadedBytes = downloadedBytes;
                TotalBytes = totalBytes;
            }
        }
    }
}
