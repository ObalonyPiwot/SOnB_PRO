using System;
using System.Runtime.InteropServices;

namespace SonB
{
    class Program
    {
        private static readonly string Mutex = "SonB_Mutex";
        static async Task Main(string[] args)
        {
            string baseDir = AppContext.BaseDirectory;
            string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var path = Path.Combine(projectDir, "config.json");
            var config = Config.Load(path);

            bool isFirstInstance;
            using (var mutex = new Mutex(true, Mutex, out isFirstInstance))
            {
                if (isFirstInstance)
                {
                    Console.WriteLine("[SYSTEM] Uruchamiam jako SERWER");
                    ConsoleNamer.SetTitle($"SERVER {Environment.ProcessId}");
                    var server = new Server(config);
                    await server.StartAsync();
                }
                else
                {
                    Console.WriteLine("[SYSTEM] Uruchamiam jako KLIENT");
                    ConsoleNamer.SetTitle($"CLIENT {Environment.ProcessId}");
                    string serverAddress = "localhost";
                    int weight = 1; //default;
                    if(config.AutomatedClientWeight == true)
                    {
                        Random random = new Random();
                        weight = random.Next(1, 4);
                        Console.WriteLine("[CLIENT] Ustawiano wagę: "+weight);
                    }
                    else
                    {
                        Console.WriteLine("[CLIENT] Podaj wagę klienta: ");
                        weight = int.Parse(Console.ReadLine());
                    }
                    var client = new Client(config, serverAddress, weight);
                    await client.StartAsync();
                }
            }
        }
    }
}
