using System.IO;

namespace JiaoLongControl.Server.Core.Utils;
/// <summary>
/// 日志记录器类，用于记录应用程序的日志信息。
/// </summary>
public class Logger
{

        /// <summary>
        /// 获取当前写日志的目录
        /// - 用户交互模式（控制台或命令行）写入当前工作目录（即运行exe的目录或命令行所在目录）
        /// - 服务模式写入程序目录（AppContext.BaseDirectory）
        /// </summary>
        private static string GetLogDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetLogFilePath()
        {
            var logDir = GetLogDirectory();
            return Path.Combine(logDir, "service.log");
        }

        /// <summary>
        /// 记录普通信息日志
        /// </summary>
        public static void Info(string format, params object[] args)
        {
            string logFilePath = GetLogFilePath();

            try
            {
                var  message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: " + string.Format(format, args);

                if (Environment.UserInteractive)
                {
                    Console.WriteLine(message);
                }
                else
                {
                    File.AppendAllText(logFilePath, message + Environment.NewLine);
                }
            }
            catch
            {
                // 防止服务崩溃，写文件失败时静默处理
            }
        }


        /// <summary>
        /// 记录错误信息日志
        /// </summary>
        public static void Error<T>(T msg)
        {
            Info("ERROR: " + msg);
        }
    }
