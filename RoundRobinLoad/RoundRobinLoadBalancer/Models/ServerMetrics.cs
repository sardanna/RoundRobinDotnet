namespace RoundRobinLoadBalancer.Models
{
    public class ServerMetrics
    {
        public double CpuStat { get; set; } = new Random().NextDouble() * 100;
        public double MemoryStat { get; set; } = new Random().NextDouble() * 100;
        public double DiskStat { get; set; } = new Random().NextDouble() * 100;
    }
}
