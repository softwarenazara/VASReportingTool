using System;
using System.IO;
using System.Text;

namespace VASReportingTool.Services
{
    public static class LocalDiagnostics
    {
        public static void Log(string category, string message)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = Path.Combine(baseDir, "Logs");
                Directory.CreateDirectory(logDir);
                var filePath = Path.Combine(logDir, "diagnostics.log");
                var builder = new StringBuilder();
                builder.AppendLine("[" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC] " + category);
                builder.AppendLine(message ?? string.Empty);
                builder.AppendLine(new string('-', 80));
                File.AppendAllText(filePath, builder.ToString());
            }
            catch
            {
            }
        }
    }
}
