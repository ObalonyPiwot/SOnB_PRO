using System.Text.Json;

namespace SonB
{
    public class Config
    {
        public int TimestampMin { get; set; }
        public int TimestampMax { get; set; }
        public int DurationSeconds { get; set; }
        public int ServerPort { get; set; }
        public int ExpectedClients { get; set; }

        public static Config Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json);
        }
    }
}
