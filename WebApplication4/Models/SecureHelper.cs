using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Configuration;

namespace WebApplication4.Models
{
    public class SecureHelper
    {
        private static readonly object _logLock = new object(); // Lock object for thread safety
        private const long MaxLogFileSize = 5 * 1024 * 1024; // 5 MB
        public static string Encrypt(string clearText)
        {
            string encryptionKey = WebConfigurationManager.AppSettings["EncryptionKey"];
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);

            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(encryptionKey, new byte[] {
            0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64,
            0x76, 0x65, 0x64, 0x65, 0x76 });

                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    string encryptedText = Convert.ToBase64String(ms.ToArray());

                    // Keep Base64 characters safe for URLs by replacing "/" and "+"
                    return encryptedText.Replace("/", "_").Replace("+", "-");
                }
            }
        }


        public static string Decrypt(string cipherText)
        {
            string encryptionKey = WebConfigurationManager.AppSettings["EncryptionKey"];

            // Restore Base64 characters that were replaced during encryption
            cipherText = cipherText.Replace("_", "/").Replace("-", "+");

            // Ensure the Base64 string is properly padded
            int padding = cipherText.Length % 4;
            if (padding > 0)
            {
                cipherText += new string('=', 4 - padding);
            }

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(encryptionKey, new byte[] {
            0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64,
            0x76, 0x65, 0x64, 0x65, 0x76 });

                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }

        public static string GetIPAddress()
        {
            System.Web.HttpContext context = System.Web.HttpContext.Current;
            string ipAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (!string.IsNullOrEmpty(ipAddress))
            {
                string[] addresses = ipAddress.Split(',');
                if (addresses.Length != 0)
                {
                    return addresses[0];
                }
            }

            return context.Request.ServerVariables["REMOTE_ADDR"];
        }
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public static void LogToFile(string Title, string LogMessages)
        {
            try
            {
                string logFolderPath = HttpContext.Current.Server.MapPath("~/LogFolder");

                if (!Directory.Exists(logFolderPath))
                {
                    Directory.CreateDirectory(logFolderPath);
                }

                string baseLogFileName = $"Log_{DateTime.Now:MMMyyyy}.txt";
                string logFilePath = Path.Combine(logFolderPath, baseLogFileName);

                lock (_logLock)
                {
                    // Step 1: Check if file exists and exceeds the size limit
                    if (File.Exists(logFilePath))
                    {
                        FileInfo fileInfo = new FileInfo(logFilePath);
                        if (fileInfo.Length > MaxLogFileSize)
                        {
                            // Step 2: Archive and compress
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            string archivedFileName = $"Archived_{timestamp}_{baseLogFileName}";
                            string archivedFilePath = Path.Combine(logFolderPath, archivedFileName);
                            string zipPath = archivedFilePath.Replace(".txt", ".zip");

                            // Rename and zip the file
                            File.Move(logFilePath, archivedFilePath);
                            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                            {
                                zip.CreateEntryFromFile(archivedFilePath, Path.GetFileName(archivedFilePath));
                            }

                            // Delete original archived txt file after compression
                            File.Delete(archivedFilePath);
                        }
                    }

                    // Step 3: Write to (new or existing) log file
                    string localIP = GetLocalIPAddress();
                    string publicIP = GetIPAddress();
                    string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    using (StreamWriter swlog = new StreamWriter(logFilePath, true))
                    {
                        swlog.WriteLine("--------------------------------------------------------------");
                        swlog.WriteLine($"DateTime   : {currentTime}");
                        swlog.WriteLine($"Local IP   : {localIP}");
                        swlog.WriteLine($"Public IP  : {publicIP}");
                        swlog.WriteLine($"Title      : {Title}");
                        swlog.WriteLine($"Message    : {LogMessages}");
                        swlog.WriteLine("--------------------------------------------------------------\n");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }



    }
}