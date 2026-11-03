using System;
using System.IO;

namespace IPropertyManager.Core
{
    /// <summary>Appends diagnostics to %LOCALAPPDATA%\IPropertyManager\log.txt so failures can be inspected afterwards.</summary>
    internal static class Log
    {
        public static string Path => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPropertyManager", "log.txt");

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { /* logging must never break the panel */ }
        }
    }
}
