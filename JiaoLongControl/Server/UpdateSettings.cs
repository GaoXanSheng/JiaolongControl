using System;
using System.IO;

namespace JiaoLongControl.Server
{
    public static class UpdateSettings
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiaoLongControl"
        );
        private static readonly string FilePath = Path.Combine(SettingsDir, "skipped_version.txt");

        public static string GetSkippedVersion()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return File.ReadAllText(FilePath).Trim();
                }
            }
            catch { }
            return string.Empty;
        }

        public static void SaveSkippedVersion(string version)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                File.WriteAllText(FilePath, version);
            }
            catch { }
        }
    }
}