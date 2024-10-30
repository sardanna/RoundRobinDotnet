using RoundRobinLoadBalancer.Models;

namespace RoundRobinLoadBalancer
{
    /// <summary>
    /// This class manages the dynamic weight calculations and handling of next server to be picked up by Load Balancer to route requests.
    /// It uses a Priority Queue based on server weights to choose the server with most weight first.
    /// </summary>
    public class ServerManager
    {
        private readonly List<ApiServer> _availableServers;
        private readonly List<ApiServer> _idleServers;
        private readonly PriorityQueue<ApiServer, double> _urlQueue;
        private readonly object _lockObj = new();
        private readonly Dictionary<string, (int count, DateTime lastFailure)> _failureData = [];
        private readonly int _weightThreshold;
        private readonly int _failureThreshold;
        private readonly int _failureResetLimitInMinutes;

        public ServerManager(List<ApiServer> availableServers, List<ApiServer> idleServers, ConfigurationManager configuration)
        {
            _availableServers = availableServers;
            _idleServers = idleServers;
            _urlQueue = new PriorityQueue<ApiServer, double>();
            _weightThreshold = Convert.ToInt32(configuration["LoadBalancer:WeightThreshold"]);
            _failureThreshold = Convert.ToInt32(configuration["LoadBalancer:FailureThreshold"]);
            _failureResetLimitInMinutes = Convert.ToInt32(configuration["LoadBalancer:FailureResetLimitInMinutes"]);
            InitializeQueue();
        }

        /// <summary>
        /// Initialize the queue with a list of available servers and equal weight when the Load Balancer first starts.
        /// </summary>
        private void InitializeQueue()
        {
            Extensions.LogMessage($"Initializing Queue");
            foreach (var server in _availableServers)
            {
                _urlQueue.Enqueue(server, -100); // Negative for max-priority
                Extensions.LogMessage($"{server.Url} added with weight -100");
            }
        }

        /// <summary>
        /// Returns the top most server from the queue, if there is only one element left in the queue, 
        /// it triggers weight update which rebuilds the queue.
        /// </summary>
        /// <returns></returns>
        public ApiServer GetNextServer()
        {
            lock (_lockObj)
            {
                var server =  _urlQueue.Peek();
                if (_urlQueue.Count == 1)
                {
                    UpdateWeights(server);
                }
                server = _urlQueue.Dequeue();
                Extensions.LogMessage($"Next server selected {server.Url}");
                return server;
            }
        }

        /// <summary>
        /// This function updates the weights of all available servers and rebuilds the queue once it is about to go empty.
        /// If a server is found with a weight less than required threshold, it is removed from the list of Available Servers.
        /// </summary>
        /// <param name="selectedServer">Current server for which requests will be routed.</param>
        public void UpdateWeights(ApiServer selectedServer)
        {
            Extensions.LogMessage("Update Weights Called");
            lock ( _lockObj)
            {
                foreach(var server in _availableServers)
                {
                    var weight = CalculateWeight();
                    if(weight < _weightThreshold)
                    {
                        Extensions.LogMessage($"Removing Server {server.Url} due to current weight {weight}.");
                        if(selectedServer == server) //Remove the server if its weight is below threshold to avoid failures after this functions completion.
                        {
                            _urlQueue.Dequeue();
                        }

                        _idleServers.Add(server);
                    }
                    else if(selectedServer != server)
                    {
                        _urlQueue.Enqueue(server, -weight);
                        Extensions.LogMessage($"Added Server {server.Url} with updated weight {weight}.");
                    }
                }
                _availableServers.RemoveAll(x => _idleServers.Contains(x));
            }
        }

        /// <summary>
        /// This function tracks failures from a server and if a server has more failures than the threshold it removes it from available servers.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="requestId"></param>
        public void TrackFailure(ApiServer server, int requestId)
        {
            Extensions.LogMessage($"Failure occured for Request Id {requestId} when routed to {server.Url}.");
            lock(_lockObj)
            {
                if (!_failureData.TryGetValue(server.Url, out var failureInfo))
                {
                    failureInfo = (0, DateTime.MinValue);
                }

                if ((DateTime.Now - failureInfo.lastFailure) > TimeSpan.FromMinutes(_failureResetLimitInMinutes)) //If last error was more than set time limit ago, reset its counter
                {
                    failureInfo = (0, DateTime.Now);
                }

                failureInfo.count++;
                failureInfo.lastFailure = DateTime.Now;
                _failureData[server.Url] = failureInfo;

                if (failureInfo.count > _failureThreshold)
                {
                    Extensions.LogMessage($"Removing Server {server.Url} due to failures more than threshold.");
                    _availableServers.Remove(server);
                    _idleServers.Add(server);
                }                
            }
        }

        /// <summary>
        /// This function calculates weight simulating Dynamic Ration calculation used by an F5 Load Traffic Manager
        /// </summary>
        /// <returns></returns>
        public virtual double CalculateWeight()
        {
            ServerMetrics metrics = Extensions.GetDummyServerMetrics();
            var cpuFactor = Math.Clamp(Constants.cpuCoeff * ((Constants.cpuThreshold - metrics.CpuStat)/Constants.cpuThreshold),0, 100);
            var memoryFactor = Math.Clamp(Constants.memoryCoeff * ((Constants.memoryThreshold - metrics.CpuStat) / Constants.memoryThreshold), 0, 100);
            var diskFactor = Math.Clamp(Constants.diskCoeff * ((Constants.diskThreshold - metrics.CpuStat) / Constants.diskThreshold), 0, 100);

            return Math.Clamp((cpuFactor + memoryFactor + diskFactor)/3 * 100, 0, 100);
        }

        /// <summary>
        /// This function is triggered periodically and checks if an idle server is healthy now, it adds it back to available servers
        /// </summary>
        public void HealthCheck()
        {
            Extensions.LogMessage("Health Check Called");
            lock (_lockObj)
            {
                foreach (var server in _idleServers)
                {
                    var weight = CalculateWeight();
                    if (weight > _weightThreshold) //checking if server has improved
                    {
                        Extensions.LogMessage($"Re-Adding Server {server.Url} due to increased Weight {weight}");
                        _availableServers.Add(server);
                    }
                }
            }
        }
    }
}
