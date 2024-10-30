namespace RoundRobinLoadBalancer.Models
{
    public class ApiServer(string url, int maxConnections)
    {
        public string Url { get; set; } = url;
        public int MaxConnections { get; set; } = maxConnections;
    }
}
