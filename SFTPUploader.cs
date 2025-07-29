using System;
using System.IO;
using Renci.SshNet;

namespace NetworkScanner.Remote
{
    public class SFTPUploader
    {
        private string host;
        private string username;
        private string password;
        private int port;

        public SFTPUploader(string host, string username, string password, int port = 22)
        {
            this.host = host;
            this.username = username;
            this.password = password;
            this.port = port;
        }

        public void UploadFile(string localPath, string remotePath)
        {
            try
            {
                using (var sftp = new SftpClient(host, port, username, password))
                {
                    Console.WriteLine($"[+] Conectando ao host {host} via SFTP...");
                    sftp.Connect();

                    using (var fileStream = new FileStream(localPath, FileMode.Open))
                    {
                        Console.WriteLine($"[+] Enviando arquivo: {Path.GetFileName(localPath)}");
                        sftp.UploadFile(fileStream, remotePath, true);
                    }

                    Console.WriteLine("[✔] Upload concluído com sucesso!");
                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Erro] Falha no upload: {ex.Message}");
            }
        }
    }
}
