using RoundRobinLoadBalancer.Models;

namespace RoundRobinLoadBalancer
{
    /// <summary>
    /// Class containing helper methods.
    /// </summary>
    public static class Extensions
    {
        public static ServerMetrics GetDummyServerMetrics()
        {
            //logic to fetch server metrics can be added here.
            return new ServerMetrics();
        }

        public static void LogMessage(string  message)
        {
            //Logging to Console for ease of viewing and no configured store.
            Console.WriteLine(message);
        }
    }
}
