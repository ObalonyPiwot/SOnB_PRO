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
            while (true)
            {
                try
                {
                    var cts = new CancellationTokenSource();
                    _ = Task.Run(() => MonitorCommands(cts));

                    using var client = new TcpClient();
                    await client.ConnectAsync(_serverAddress, _config.ServerPort);
                    Console.WriteLine("[Client] Połączono z serwerem.");
                    ConsoleNamer.SetTitle($"CLIENT {Environment.ProcessId}");
                    await ReceiveData(client);
                    Random rand = new Random();

                    while (!cts.Token.IsCancellationRequested)
                    {
                        await SendData(client, rand);

                        await ReceiveData(client);

                        await Task.Delay(1000);
                    }
                }
                catch
                {
                    Console.WriteLine("[Client] Błąd połączenia. Próba ponownego połączenia za 5 sek...");
                    await Task.Delay(5000);
                }
            }
        }

        private async Task ReceiveData(TcpClient client)
        {
            var stream = client.GetStream();

            // Odbieranie danych TimestampMin i TimestampMax od serwera
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer);
            string configData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string[] parts = configData.Split('|');

            if (parts.Length != 2 ||
                !double.TryParse(parts[0], out double timestampMin) ||
                !double.TryParse(parts[1], out double timestampMax))
            {
                Console.WriteLine("[Client] Niepoprawna konfiguracja z serwera.");
                _validConfiguration = false;
                return;
            }
            Console.WriteLine($"[Client] Otrzymano zakres: {timestampMin} - {timestampMax}");
            _timestampMax = timestampMax;
            _timestampMin = timestampMin;
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
                message = _sendInvalidData ? "INVALID_DATA" : $"{timestamp}|{_weight}";
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
