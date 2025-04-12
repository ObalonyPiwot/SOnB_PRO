using System.Net.Sockets;
using System.Text;

namespace SonB
{
    public class Client
    {
        private Config _config;
        private double _timestampMin;
        private double _timestampMax;
        private string _serverAddress;
        private int _weight;
        private Guid _clientId;
        private bool _sendInvalidData = false;
        private bool _validConfiguration = false;

        public Client(Config config, string serverAddress, int weight)
        {
            _config = config;
            _serverAddress = serverAddress;
            _weight = weight;
        }
        public async Task StartAsync()
        {
            var retryCount = 0;
            while (true)
            {
                try
                {
                    ConsoleNamer.SetTitle($"CLIENT {Environment.ProcessId}");
                    var cts = new CancellationTokenSource();
                    _ = Task.Run(() => MonitorCommands(cts));

                    using var client = new TcpClient();

                    await client.ConnectAsync(_serverAddress, _config.ServerPort);
                    await ReceiveData(client);
                    Console.WriteLine("[Client] Połączono z serwerem.");
                    Random rand = new Random();

                    while (!cts.Token.IsCancellationRequested)
                    {
                        await SendData(client, rand);
                        await ReceiveData(client);
                        await Task.Delay(1000);
                        retryCount = 0;
                    }
                }
                catch
                {
                    if (retryCount >= 2)
                    {
                        await TryBecomeServer();
                        return;
                    }
                    retryCount++;
                    Console.WriteLine("[Client] Błąd połączenia. Próba ponownego połączenia za 5 sek...");
                    await Task.Delay(5000);
                }
            }
        }
        private async Task TryBecomeServer()
        {
            Console.WriteLine("[Client] Przekształcam się w serwer...");
            ConsoleNamer.SetTitle($"SERVER {Environment.ProcessId}");

            var server = new Server(_config);
            await server.StartAsync();
        }
        private async Task ReceiveData(TcpClient client)
        {
            var stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer);
            string configData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string[] parts = configData.Split('|');

            if (parts.Length < 2 || parts.Length > 3 ||
                !double.TryParse(parts[0], out double timestampMin) ||
                !double.TryParse(parts[1], out double timestampMax))
            {
                Console.WriteLine("[Client] Niepoprawna konfiguracja z serwera.");
                _validConfiguration = false;
                return;
            }

            // Jeśli jest trzeci element – musi być poprawnym GUID-em
            Guid id = Guid.Empty;
            if (parts.Length == 3)
            {
                if (!Guid.TryParse(parts[2], out id))
                {
                    Console.WriteLine("[Client] Niepoprawny GUID w konfiguracji.");
                    _validConfiguration = false;
                    return;
                }

                Console.WriteLine($"[Client] Otrzymano id: {id}");
                _clientId = id;
            }

            Console.WriteLine($"[Client] Otrzymano zakres: {timestampMin} - {timestampMax}");

            _timestampMin = timestampMin;
            _timestampMax = timestampMax;
            _validConfiguration = true;
        }
        private async Task SendData(TcpClient client, Random rand)
        {
            var stream = client.GetStream();
            string message;
            if(!_validConfiguration)
            {
                message = "INVALID_CONFIGURATION";
            } 
            else 
            {
                double timestamp = rand.NextDouble() * (_timestampMax - _timestampMin) + _timestampMin;
                message = _sendInvalidData ? "INVALID_DATA" : $"{timestamp}|{_weight}|{_clientId}";
            }
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data);
            Console.WriteLine($"[Client] Wysłano: {message}");
        }

        private void MonitorCommands(CancellationTokenSource cts)
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
                    {
                        Console.WriteLine("\n[Client] Wciśnięto klawisz 1 - Zatrzymywanie klienta.");
                        cts.Cancel();
                        Environment.Exit(0); // zakończenie całego procesu
                    }
                    if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
                    {
                        _sendInvalidData = !_sendInvalidData;
                        Console.WriteLine("\n[Client] Wciśnięto klawisz 2 - Wysyłanie " +
                            (_sendInvalidData ? "nie" : "") +
                            " poprawnych danych.");
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
