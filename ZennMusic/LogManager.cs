using System;
using System.IO;
using System.Text;

namespace ZennMusic
{
    public static class LogManager
    {
        private static FileStream logger;

        private static string CurrentTimeData => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff");

        public static void ActivateLogger()
        {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd");

            if (!Directory.Exists(@"D:\ZennBotLog"))
                Directory.CreateDirectory(@"D:\ZennBotLog");

            if (!File.Exists($@"D:\ZennBotLog\ZennLog {dateString}.txt"))
                File.Create($@"D:\ZennBotLog\ZennLog {dateString}.txt").Close();
            
            logger = File.Open($@"D:\ZennBotLog\ZennLog {dateString}.txt", FileMode.Append, FileAccess.Write, FileShare.Read);

            Log("------------ < LOGGING START > ------------");
        }

        public static void Log(string message, bool attachHeader = true)
        {
            if (attachHeader)
            {
                var header = Encoding.UTF8.GetBytes($"[{CurrentTimeData}] ");
                logger.Write(header, 0, header.Length);
                logger.Flush();
            }

            var messageBytes = Encoding.UTF8.GetBytes(message + "\n");
            logger.Write(messageBytes, 0, messageBytes.Length);
        }
    }
}
