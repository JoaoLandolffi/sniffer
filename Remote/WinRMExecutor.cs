using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;

namespace NetworkScanner.Remote
{
    public class WinRMInteractiveExecutor

    {
        public static string Execute(string host, string user, string password, string command)
        {
            try
            {
                var securePassword = new SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                var credentials = new PSCredential(user, securePassword);

                var connectionInfo = new WSManConnectionInfo(
                    new Uri($"http://{host}:5985/wsman"),   // Porta padrão WinRM
                    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                    credentials)
                {
                    SkipCACheck = true,
                    SkipCNCheck = true,
                    SkipRevocationCheck = true,
                    OperationTimeout = 4 * 60 * 1000,
                    OpenTimeout = 1 * 60 * 1000
                };

                using (var runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    runspace.Open();

                    using (var pipeline = runspace.CreatePipeline())
                    {
                        pipeline.Commands.AddScript(command);
                        pipeline.Commands.Add("Out-String");

                        var results = pipeline.Invoke();
                        runspace.Close();

                        var output = "";
                        foreach (var item in results)
                            output += item.ToString();

                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[WinRM Error] {ex.Message}";
            }
        }
    }
}
