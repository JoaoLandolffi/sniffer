using System;
using System.Diagnostics;

namespace NetworkScanner.Remote
{
    public static class WinRMExecutor
    {
        /// <summary>
        /// Executa um comando PowerShell remoto via WinRM usando Invoke-Command.
        /// </summary>
        /// <param name="host">IP ou nome do host remoto</param>
        /// <param name="username">Usuário para autenticação</param>
        /// <param name="password">Senha do usuário</param>
        /// <param name="command">Comando PowerShell a ser executado</param>
        public static void ExecuteCommand(string host, string username, string password, string command)
        {
            try
            {
                // Comando para criar um objeto PSCredential em PowerShell
                string psCredential = $"$pass = ConvertTo-SecureString '{password}' -AsPlainText -Force; " +
                                      $"$cred = New-Object System.Management.Automation.PSCredential('{username}', $pass);";

                // Monta o Invoke-Command para executar remotamente
                string psCommand = $"{psCredential} Invoke-Command -ComputerName {host} -Credential $cred -ScriptBlock {{ {command} }}";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{psCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"[WinRM] Erro: {error}");
                    }
                    else
                    {
                        Console.WriteLine($"[WinRM] Resultado:\n{output}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WinRM] Exceção: {ex.Message}");
            }
        }
    }
}
