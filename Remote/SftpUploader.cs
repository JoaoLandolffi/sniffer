using System;
using System.IO;
using Renci.SshNet;
using Spectre.Console;

namespace NetworkScanner.Remote
{
    public class SftpUploader
    {
        public static bool UploadFile(string host, string username, string password, string localPath, string remotePath)
        {
            try
            {
                var sftp = new SftpClient(host, username, password);
                sftp.Connect();
                var fileStream = new FileStream(localPath, FileMode.Open);
                sftp.UploadFile(fileStream, remotePath, true);
                sftp.Disconnect();

                AnsiConsole.MarkupLine($"[green]✅ Arquivo enviado com sucesso para {host}:{remotePath}[/]");
                return true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Erro no upload para {host}: {ex.Message}[/]");
                return false;
            }
        }
    }
}
