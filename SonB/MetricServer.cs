using Prometheus;

public class MetricsServer
{
    private static readonly Gauge ConnectedClients = Metrics.CreateGauge("connected_clients_total", "Liczba aktualnie połączonych klientów");
    private static readonly Counter DisconnectedClients = Metrics.CreateCounter("disconnected_clients_total", "Liczba rozłączonych klientów");
    private static readonly Histogram TimestampsHistogram = Metrics.CreateHistogram("timestamp_median_value", "Histogram wartości median timestampów");
    private static readonly Gauge MedianGauge = Metrics.CreateGauge("actual_timestamp_median_value", "Histogram wartości median timestampów");

    public static void Start()
    {
        var metricServer = new KestrelMetricServer(port: 9100);
        metricServer.Start();
    }

    public static void SetConnectedClients(int count) => ConnectedClients.Set(count);
    public static void SetMedain(double median) => MedianGauge.Set(median);

    public static void IncrementDisconnected() => DisconnectedClients.Inc();

    public static void ObserveMedian(double median) => TimestampsHistogram.Observe(median);
}
