using System;
using System.IO;
using System.Security.Cryptography;
using Renci.SshNet;

namespace NetworkScanner.Remote
{
    public class ChecksumValidator
    {
        public static string CalculateLocalChecksum(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string GetRemoteChecksum(string host, string user, string password, string remotePath, bool isWindows = false)
        {
            using (var client = new SshClient(host, user, password))
            {
                try
                {
                    client.Connect();
                    string cmd = isWindows
                        ? $"powershell -command \"Get-FileHash '{remotePath}' -Algorithm SHA256 | Select-Object -ExpandProperty Hash\""
                        : $"sha256sum {remotePath} | cut -d ' ' -f1";

                    var result = client.RunCommand(cmd);
                    client.Disconnect();

                    if (!string.IsNullOrWhiteSpace(result.Error))
                        throw new Exception(result.Error);

                    return result.Result.Trim().ToLowerInvariant();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Checksum Error] {ex.Message}");
                    return null;
                }
            }
        }

        public static void Compare(string host, string user, string password, string localPath, string remotePath, bool isWindows = false)
        {
            Console.WriteLine("[Checksum] Verificando integridade...");

            string localHash = CalculateLocalChecksum(localPath);
            Console.WriteLine($"[Checksum] Local:  {localHash}");

            string remoteHash = GetRemoteChecksum(host, user, password, remotePath, isWindows);
            if (remoteHash == null)
            {
                Console.WriteLine("[Checksum] Falha ao obter checksum remoto.");
                return;
            }

            Console.WriteLine($"[Checksum] Remoto: {remoteHash}");

            if (localHash == remoteHash)
                Console.WriteLine("[Checksum] ✅ Arquivos idênticos.");
            else
                Console.WriteLine("[Checksum] ❌ Arquivos diferentes.");
        }
    }
}
