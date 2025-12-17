using System.IO;
using System.Reflection;

namespace JiaoLongControl.Server.Core.Utils
{
    public static class EmbeddedResourceHelper
    {
        public static void ExtractEmbeddedResourceToFile(string resourceName, string outputFilePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            // string[] names = assembly.GetManifestResourceNames(); 

            using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
                throw new Exception($"未找到嵌入资源: {resourceName}");

            var directory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            resourceStream.CopyTo(fileStream);
        }

        public static string ExtractResourceToExeDir(string resourceName, string fileName)
        {
            string exeDir = AppContext.BaseDirectory;
            string outputPath = Path.Combine(exeDir, fileName);
            if (!File.Exists(outputPath))
            {
                ExtractEmbeddedResourceToFile(resourceName, outputPath);
            }
            return outputPath;
        }
    }
}