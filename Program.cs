using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spectre.Console;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Renci.SshNet;
using NetworkScanner.Remote;

namespace NetworkScanner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Contains("--smbdir"))
            {
                int idx = Array.IndexOf(args, "--smbdir");

                if (args.Length <= idx + 4)
                {
                    Console.WriteLine("Uso incorreto do --smbdir");
                    Console.WriteLine("Exemplo: --smbdir \\\\192.168.1.10\\C$\\Destino C:\\Local\\Pasta usuario senha");
                    return;
                }

                string remoteDir = args[idx + 1];
                string localDir = args[idx + 2];
                string user = args[idx + 3];
                string pass = args[idx + 4];

                SMBUploader.UploadDirectory(remoteDir, localDir, user, pass);
                return;
            }

            if (args.Contains("--winrm"))
            {
                int idx = Array.IndexOf(args, "--winrm");
                string host = args[idx + 1];
                string user = args[idx + 2];
                string pass = args[idx + 3];
                string cmd = args[idx + 4];

                WinRMExecutor.ExecuteCommand(host, user, pass, cmd);
                return;
            }
            // Execução remota via WinRM
            else if (args.Contains("--winrm"))
            {
                if (args.Length < 5)
                {
                    Console.WriteLine("Uso: --winrm [host] [user] [password] [\"comando\"]");
                    return;
                }

                string host = args[1];
                string user = args[2];
                string pass = args[3];
                string command = args[4];

                string result = WinRMInteractiveExecutor.Execute (host, user, pass, command);
                Console.WriteLine("[WinRM] Resultado:");
                Console.WriteLine(result);
                return;
            }

            if (args.Contains("--smb"))
            {
                // Exemplo: --smb \\192.168.1.10\C$\Temp\arquivo.txt C:\local\arquivo.txt USER senha
                int idx = Array.IndexOf(args, "--smb");
                string remotePath = args[idx + 1];     // ex: \\192.168.1.10\C$\Temp\arquivo.txt
                string localPath = args[idx + 2];
                string user = args[idx + 3];           // pode ser: nome ou dominio\usuario
                string pass = args[idx + 4];

                SMBUploader.UploadFile(remotePath, localPath, user, pass);
                return;
            }

            if (args.Contains("--sftp"))
            {
                var uploader = new SFTPUploader("192.168.1.4", "USER", "attdfigth");
                uploader.UploadFile(@"C:\Users\user\Desktop\teste.txt", "teste.txt");
                return;
            }
            else if (args.Contains("--checksum"))
            {
                if (args.Length < 6)
                {
                    Console.WriteLine("Uso: --checksum [host] [user] [password] [localPath] [remotePath] [windows?]");
                    return;
                }

                string host = args[1];
                string user = args[2];
                string pass = args[3];
                string local = args[4];
                string remote = args[5];
                bool isWindows = args.Length >= 7 && args[6].ToLower() == "true";

                ChecksumValidator.Compare(host, user, pass, local, remote, isWindows);
            }

            if (args.Contains("--ssh"))
            {
                SSHExecutor.ExecuteCommand(
                    host: "192.168.1.4",              // seu IP local
                    username: "USER",  // nome de login do Windows (sem @)
                    password: "attdfigth",            // senha da sua conta local
                    command: "ipconfig"                 // ou "dir" / "hostname"
                );
                return;

            }
            else if (args.Contains("--ssh-session"))
            {
                if (args.Length < 4)
                {
                    Console.WriteLine("Uso: --ssh-session [host] [user] [password]");
                    return;
                }

                string host = args[1];
                string user = args[2];
                string pass = args[3];

                SSHSession.Start(host, user, pass);
                return;
            }


            if (args.Contains("--help") || args.Length == 0)
            {
                AnsiConsole.Write(new Panel(@"
[bold yellow]Uso:[/]

NetworkScanner.exe [[subnet]] [[exportType]] [[--ports=22,80,443]]

[bold yellow]Exemplos:[/]
  NetworkScanner.exe 192.168.1 json
  NetworkScanner.exe 192.168.0 csv --ports=22,445
  NetworkScanner.exe 10.0.0 xml

[bold yellow]Export types:[/] json | csv | xml")
.Border(BoxBorder.Double));
                return;
            }

            string subnet = args.ElementAtOrDefault(0) ?? "192.168.0.";
            string exportType = args.ElementAtOrDefault(1)?.ToLower() ?? "json";
            string portArg = args.FirstOrDefault(a => a.StartsWith("--ports="));
            List<int> customPorts = null;

            if (!subnet.EndsWith(".")) subnet += ".";

            if (portArg != null)
            {
                string portList = portArg.Replace("--ports=", "");
                customPorts = portList.Split(',').Select(p => int.TryParse(p, out var n) ? n : -1).Where(p => p > 0).ToList();
            }

            AnsiConsole.MarkupLine($"[bold blue]🔍 Iniciando scanner na subrede: {subnet}0/24[/]");
            List<HostInfo> activeHosts = new List<HostInfo>();

            var tasks = new List<Task>();

            for (int i = 1; i < 255; i++)
            {
                string ip = subnet + i;
                tasks.Add(Task.Run(async () =>
                {
                    if (await IsHostAlive(ip))
                    {
                        var ports = await ScanOpenPorts(ip, customPorts);
                        var mac = GetMacFromArp(ip);
                        lock (activeHosts)
                        {
                            activeHosts.Add(new HostInfo { IP = ip, MAC = mac, OpenPorts = ports });
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            AnsiConsole.MarkupLine("\n[green]✅ Varredura concluída. Hosts encontrados:[/]");
            foreach (var host in activeHosts)
            {
                AnsiConsole.MarkupLine($"[yellow]{host.IP}[/] [gray]({host.MAC})[/] -> Portas: [blue]{string.Join(", ", host.OpenPorts)}[/]");
            }

            switch (exportType)
            {
                case "json":
                    File.WriteAllText("scan_result.json", JsonConvert.SerializeObject(activeHosts, Formatting.Indented));
                    AnsiConsole.MarkupLine("[green]📄 Exportado: scan_result.json[/]");
                    break;
                case "csv":
                    var lines = new List<string> { "IP,MAC,Ports" };
                    lines.AddRange(activeHosts.Select(h => $"{h.IP},{h.MAC},\"{string.Join(" ", h.OpenPorts)}\""));
                    File.WriteAllLines("scan_result.csv", lines);
                    AnsiConsole.MarkupLine("[green]📄 Exportado: scan_result.csv[/]");
                    break;
                case "xml":
                    var serializer = new XmlSerializer(typeof(List<HostInfo>));
                    using (var sw = new StreamWriter("scan_result.xml"))
                    {
                        serializer.Serialize(sw, activeHosts);
                    }
                    AnsiConsole.MarkupLine("[green]📄 Exportado: scan_result.xml[/]");
                    break;
            }
        }

        static async Task<bool> IsHostAlive(string ip)
        {
            try
            {
                Ping ping = new Ping();
                PingReply reply = await ping.SendPingAsync(ip, 100);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        static async Task<List<int>> ScanOpenPorts(string ip, List<int> customPorts = null)
        {
            List<int> openPorts = new List<int>();
            int[] defaultPorts = { 21, 22, 23, 80, 443, 445, 3389 };
            int[] portsToScan = (customPorts != null && customPorts.Any()) ? customPorts.ToArray() : defaultPorts;
            List<Task> scanTasks = new List<Task>();

            foreach (int port in portsToScan)
            {
                scanTasks.Add(Task.Run(async () =>
                {
                    var client = new System.Net.Sockets.TcpClient();
                    try
                    {
                        var connectTask = client.ConnectAsync(ip, port);
                        if (await Task.WhenAny(connectTask, Task.Delay(200)) == connectTask && client.Connected)
                        {
                            lock (openPorts) openPorts.Add(port);
                        }
                    }
                    catch { }
                }));
            }

            await Task.WhenAll(scanTasks);
            return openPorts;
        }

        static string GetMacFromArp(string ip)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "arp",
                        Arguments = "-a",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                string line = output.Split('\n').FirstOrDefault(l => l.Contains(ip));
                if (line != null)
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        return parts[1];
                }
            }
            catch { }

            return "N/A";
        }
    }

    public class HostInfo
    {
        public string IP { get; set; }
        public string MAC { get; set; } = "N/A";
        public List<int> OpenPorts { get; set; } = new List<int>();
    }

    public static class SSHExecutor
    {
        public static void ExecuteCommand(string host, string username, string password, string command)
        {
            try
            {
                var client = new SshClient(host, username, password);
                client.Connect();
                var cmd = client.RunCommand(command);
                AnsiConsole.MarkupLine($"[bold blue]{host}[/]:\n{cmd.Result.Trim()}");
                client.Disconnect();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Erro SSH em {host}: {ex.Message}[/]");
            }
        }
    }
}
