using System;
using System.Diagnostics;
using System.IO;
using Spectre.Console;

namespace NetworkScanner.Remote
{
    public static class SMBUploader
    {
        private const string DriveLetter = "Z:";

        public static void UploadFile(string remotePath, string localFilePath, string username, string password)
        {
            try
            {
                MapNetworkDrive(remotePath, username, password);

                string fileName = Path.GetFileName(localFilePath);
                string destination = Path.Combine(DriveLetter, fileName);
                long totalBytes = new FileInfo(localFilePath).Length;

                using (var input = new FileStream(localFilePath, FileMode.Open, FileAccess.Read))
                using (var output = new FileStream(destination, FileMode.Create))
                {
                    byte[] buffer = new byte[4096];
                    long totalCopied = 0;

                    AnsiConsole.Progress()
                        .Start(ctx =>
                        {
                            var task = ctx.AddTask($"[green]Enviando {fileName}[/]", maxValue: totalBytes);

                            int bytesRead;
                            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                output.Write(buffer, 0, bytesRead);
                                totalCopied += bytesRead;
                                task.Value = totalCopied;
                            }
                        });
                }

                Console.WriteLine($"[SMB] Upload de arquivo concluído: {fileName}");

                UnmapNetworkDrive();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] Erro: {ex.Message}");
            }
        }

        public static void UploadDirectory(string remoteDir, string localDir, string username, string password)
        {
            try
            {
                if (!Directory.Exists(localDir))
                {
                    Console.WriteLine($"[SMB] Diretório local não existe: {localDir}");
                    return;
                }

                MapNetworkDrive(remoteDir, username, password);

                // Obtém o caminho raiz da unidade mapeada (Z:)
                string remoteRoot = DriveLetter;

                UploadDirectoryRecursive(localDir, remoteRoot);

                UnmapNetworkDrive();

                Console.WriteLine("[SMB] Upload de diretório concluído.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] Erro: {ex.Message}");
            }
        }

        private static void UploadDirectoryRecursive(string localPath, string remotePath)
        {
            // Cria o diretório remoto se não existir
            if (!Directory.Exists(remotePath))
            {
                Directory.CreateDirectory(remotePath);
            }

            // Copia todos os arquivos
            foreach (var filePath in Directory.GetFiles(localPath))
            {
                string fileName = Path.GetFileName(filePath);
                string destination = Path.Combine(remotePath, fileName);
                UploadSingleFile(filePath, destination);
            }

            // Chama recursivamente para subdiretórios
            foreach (var dirPath in Directory.GetDirectories(localPath))
            {
                string dirName = Path.GetFileName(dirPath);
                string destinationDir = Path.Combine(remotePath, dirName);
                UploadDirectoryRecursive(dirPath, destinationDir);
            }
        }

        private static void UploadSingleFile(string localFilePath, string remoteFilePath)
        {
            long totalBytes = new FileInfo(localFilePath).Length;

            using (var input = new FileStream(localFilePath, FileMode.Open, FileAccess.Read))
            using (var output = new FileStream(remoteFilePath, FileMode.Create))
            {
                byte[] buffer = new byte[4096];
                long totalCopied = 0;

                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask($"[green]Enviando {Path.GetFileName(localFilePath)}[/]", maxValue: totalBytes);

                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                            totalCopied += bytesRead;
                            task.Value = totalCopied;
                        }
                    });
            }
        }

        private static void MapNetworkDrive(string remotePath, string username, string password)
        {
            string uncPath = remotePath;

            // Se o path tiver arquivo, obtém a pasta raiz
            if (!Directory.Exists(remotePath))
            {
                uncPath = Path.GetDirectoryName(remotePath);
            }

            var netUse = new ProcessStartInfo("net", $"use {DriveLetter} \"{uncPath}\" /user:{username} {password}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = Process.Start(netUse);
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                string error = p.StandardError.ReadToEnd();
                throw new Exception($"Falha ao mapear o compartilhamento: {error}");
            }
        }

        private static void UnmapNetworkDrive()
        {
            var netUseDelete = new ProcessStartInfo("net", $"use {DriveLetter} /delete /yes")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(netUseDelete)?.WaitForExit();
        }
    }
}
