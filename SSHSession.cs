using System;
using Renci.SshNet;

namespace NetworkScanner.Remote
{
    public class SSHSession
    {
        public static void Start(string host, string user, string password)
        {
            using (var client = new SshClient(host, user, password))
            {
                try
                {
                    client.Connect();
                    Console.WriteLine($"[SSH] Sessão iniciada com {host}. Digite 'exit' para sair.\n");

                    while (true)
                    {
                        Console.Write($"{user}@{host}$ ");
                        string commandText = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(commandText))
                            continue;

                        if (commandText.ToLower() == "exit" || commandText.ToLower() == "quit")
                            break;

                        var cmd = client.RunCommand(commandText);
                        Console.WriteLine(cmd.Result);
                        if (!string.IsNullOrWhiteSpace(cmd.Error))
                            Console.WriteLine($"[stderr] {cmd.Error}");
                    }

                    client.Disconnect();
                    Console.WriteLine("[SSH] Sessão encerrada.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SSH Error] {ex.Message}");
                }
            }
        }
    }
}
