namespace SonB
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string baseDir = AppContext.BaseDirectory;
            string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var path = Path.Combine(projectDir, "config.json");
            var config = Config.Load(path);

            Console.WriteLine("Wybierz tryb: 1 - Serwer, 2 - Klient");
            var choice = Console.ReadLine();

            if (choice == "1")
            {
                var server = new Server(config);
                await server.StartAsync();
            }
            else if (choice == "2")
            {
                string serverAddress = "localhost";
                Console.Write("Podaj wagę klienta: ");
                int weight = int.Parse(Console.ReadLine());

                var client = new Client(config, serverAddress, weight);
                await client.StartAsync();
            }
            else
            {
                Console.WriteLine("Nieprawidłowy wybór.");
            }
        }
    }
}
