using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Linq;

namespace SonB
{
    public class Server
    {
        private readonly Config _config;
        private readonly List<TcpClient> _clients = new();
        private readonly List<TcpClient> _disconnectedClients = new();
        private readonly List<Guid> _clientIds = new();
        private readonly List<string> _messages = new();
        private readonly object _lock = new();
        private bool _running = true;
        private bool[] _sendInvalidDataForClient;
        private int _toggleIndex = 0;

        public Server(Config config)
        {
            _config = config;
            _sendInvalidDataForClient = new bool[_config.ExpectedClients];
        }

        public async Task StartAsync()
        {
            var cts = new CancellationTokenSource();
            _ = Task.Run(() => MonitorCommands(cts));

            while (_running && !cts.Token.IsCancellationRequested)
            {
                _clients.Clear();
                _disconnectedClients.Clear();
                _sendInvalidDataForClient = new bool[_config.ExpectedClients];
                _toggleIndex = 0;

                TcpListener listener = new TcpListener(IPAddress.Any, _config.ServerPort);
                listener.Start();
                Console.WriteLine($"[Serwer] Oczekiwanie na {_config.ExpectedClients} klientów...");
                ConsoleNamer.SetTitle($"SERVER {Environment.ProcessId} ({_clients.Count()}/{_config.ExpectedClients})");

                while (_clients.Count < _config.ExpectedClients)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _clients.Add(client);
                    Console.WriteLine($"[Serwer] Klient {_clients.Count}/{_config.ExpectedClients} połączony.");
                    ConsoleNamer.SetTitle($"SERVER {Environment.ProcessId} ({_clients.Count()}/{_config.ExpectedClients})");
                }

                Console.WriteLine("[Serwer] Wszyscy klienci połączeni.");

                // Pierwsza konfiguracja
                await SendConfigurationToAll(true);

                while (_running && !cts.Token.IsCancellationRequested)
                {
                    _messages.Clear();

                    for (int i = 0; i < _clients.Count; i++)
                    {
                        try
                        {
                            await ReceiveData(i).WaitAsync(TimeSpan.FromMilliseconds(_config.AwaitForClients));
                        }
                        catch (TimeoutException)
                        {
                            Console.WriteLine($"[Serwer] Klient {i} nie odpowiedział w czasie {_config.AwaitForClients} ms.");
                        }
                    }

                    ProcessResults();

                    foreach (var dc in _disconnectedClients)
                    {
                        Console.WriteLine("[Serwer] Klient rozłączony.");
                        _clients.Remove(dc);
                        dc.Close();
                    }

                    await SendConfigurationToAll();

                    if (_clients.Count == 0)
                    {
                        Console.WriteLine("[Serwer] Wszyscy klienci się rozłączyli. Czekam na restart...");
                        break;
                    }
                }

                foreach (var client in _clients)
                {
                    var stream = client.GetStream();
                    byte[] msg = Encoding.UTF8.GetBytes("RESTART");
                    await stream.WriteAsync(msg);
                    client.Close();
                }

                listener.Stop();
                Console.WriteLine("[Serwer] Zatrzymano listener. Oczekiwanie na restart lub zakończenie.");
            }
        }

        private async Task ReceiveData(int i)
        {
            var client = _clients[i];
            try
            {
                var stream = client.GetStream();

                if (!client.Connected || !stream.CanRead)
                {
                    _disconnectedClients.Add(client);
                    return;
                }

                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer);

                if (bytesRead == 0)
                {
                    _disconnectedClients.Add(client);
                    return;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"[Serwer] Otrzymano od klienta {i}: {message}");

                if (message == "INVALID_CONFIGURATION")
                {
                    await RetrySendConfigurationToClient(i);
                    return;
                }

                lock (_lock)
                {
                    var parts = message.Split('|');
                    if (parts.Length == 3 &&
                        double.TryParse(parts[0], out double _) &&
                        int.TryParse(parts[1], out int _) &&
                        Guid.TryParse(parts[2], out Guid parsedGuid) &&
                        _clientIds.Contains(parsedGuid))
                    {
                        _messages.Add(message);
                    }
                    else
                    {
                        Console.WriteLine($"[Serwer] Odrzucono niepoprawną wiadomość od klienta {i}.");
                    }
                }
            }
            catch
            {
                _disconnectedClients.Add(client);
            }
        }
        private async Task RetrySendConfigurationToClient(int i)
        {
            Console.WriteLine($"[Serwer] Klient {i} zgłasza niepoprawną konfigurację. Ponawiam wysyłkę...");

            int retryCount = 0;
            while (_running && retryCount < 5)
            {
                await SendConfigurationToClient(i);
                retryCount++;
                Console.WriteLine($"[Serwer] Próba ponownej wysyłki konfiguracji ({retryCount}/5) do klienta {i}...");
                await Task.Delay(1000);
            }

            if (retryCount >= 5)
            {
                Console.WriteLine($"[Serwer] Przekroczono limit prób dla klienta {i}. Oczekiwanie na dalsze dane...");
            }
        }
        private async Task SendConfigurationToClient(int i, bool isFirst = false)
        {
            if (i >= _clients.Count) return;

            var client = _clients[i];
            var stream = client.GetStream();
            string message;
            if (_sendInvalidDataForClient[i])
            {

                message = "INVALID_CONFIGURATION_MESSAGE";
            }
            else
            {
                Random rand = new Random();
                double min = rand.NextDouble() * (_config.TimestampMax - _config.TimestampMin) + _config.TimestampMin;
                double max = rand.NextDouble() * (_config.TimestampMax - min) + min;
                message = $"{min}|{max}";
                if (isFirst)
                {
                    var id = Guid.NewGuid();
                    _clientIds.Add(id);
                    message += $"|{id}";
                }
            }
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task SendConfigurationToAll(bool isFirst = false)
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                await SendConfigurationToClient(i, isFirst);
            }
        }

        private void ProcessResults()
        {
            var timestamps = new List<double>();
            lock (_lock)
            {
                foreach (var msg in _messages)
                {
                    var parts = msg.Split('|');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out double ts) &&
                        int.TryParse(parts[1], out int weight))
                    {
                        for (int i = 0; i < weight; i++)
                            timestamps.Add(ts);
                    }
                }
            }

            timestamps.Sort();
            if (timestamps.Count == 0)
            {
                Console.WriteLine("[Serwer] Brak poprawnych danych.");
                return;
            }

            if (timestamps.Count > 4)
                timestamps = timestamps.Skip(1).Take(timestamps.Count - 2).ToList();

            double median = timestamps[timestamps.Count / 2];
            Console.WriteLine($"[Serwer] Mediana timestampów: {median:F2} sek.");
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
                        _running = false;
                        cts.Cancel();
                        Console.WriteLine("\n[Serwer] Wciśnięto klawisz 1 – zatrzymywanie serwera.");
                    }
                    else if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
                    {
                        if (_toggleIndex < _sendInvalidDataForClient.Length)
                        {
                            _sendInvalidDataForClient[_toggleIndex] = true;
                            Console.WriteLine($"[Serwer] Klient {_toggleIndex} będzie teraz otrzymywał niepoprawne dane.");
                            _toggleIndex++;
                        }
                        else
                        {
                            Console.WriteLine("[Serwer] Wszyscy klienci już oznaczeni.");
                        }
                    }
                    else if (key.Key == ConsoleKey.D3 || key.Key == ConsoleKey.NumPad3)
                    {
                        for (int i = 0; i < _sendInvalidDataForClient.Length; i++)
                            _sendInvalidDataForClient[i] = false;
                        _toggleIndex = 0;
                        Console.WriteLine("[Serwer] Reset: wszyscy klienci będą otrzymywać poprawne dane.");
                    }
                }

                Thread.Sleep(100);
            }
        }
    }
}
