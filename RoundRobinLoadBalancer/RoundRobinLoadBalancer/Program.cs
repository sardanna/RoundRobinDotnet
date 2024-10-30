using RoundRobinLoadBalancer;
using RoundRobinLoadBalancer.Models;

/// <summary>
/// This is the main program which holds the Load Balancer
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //Get Configuration object to read from App Settings
        var configuration = builder.Configuration;

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHttpClient("LoadBalancedClient");

        //Read initial list of available servers from configuration
        var apiSettings = configuration?.GetSection("Apis")
        .Get<List<Dictionary<string, object>>>();

        // Map to ApiServer objects
        var servers = apiSettings?.Select(api => new ApiServer(
            (string)api["url"],
            Convert.ToInt32(api["maxConnections"])))
            .ToList();

        //Add Server Manager with initial list of servers
        builder.Services.AddSingleton(sp => new ServerManager(servers ?? [], [], configuration ?? new ConfigurationManager()));

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();


        /**************************************************************/
        // Loaf Balancer Begins //
        /**************************************************************/

        //Intialize control variables
        int currentRequest = 0; //to keep track of connections made to current server
        int currentFailures = 0; 
        ApiServer? server = null; //current server where requests are being routed to
        int requestId = 0;
        int maxFailureCountBeforeNextServer = Convert.ToInt32(configuration?["LoadBalancer:MaxFailureCountBeforeNextServer"]);
        var healthCheckInterval = TimeSpan.FromMinutes(Convert.ToInt32(configuration?["LoadBalancer:HealthCheckIntervalInMinutes"]));
        var healthCheckCancellationToken = new CancellationTokenSource();
        var serverManager = app.Services.GetRequiredService<ServerManager>();


        // Start periodic health checks
        Task.Run(() => PeriodicHealthCheck(healthCheckInterval, healthCheckCancellationToken.Token, serverManager));

        //Load Balancer Endpoint
        app.Run(async context =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); //set timeout token
            context.RequestAborted = cts.Token;
            var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient("LoadBalancedClient");

            //Read incoming reuqest
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, context.Request.ContentType ?? "application/json");
            var payloadSize = content.Headers.ContentLength ?? requestBody.Length;

            //Initializing unique request id for better logging
            requestId++;
            Extensions.LogMessage($"Execution Started for request Id {requestId}");

            bool success = false;
            while (!success)
            {
                try
                {
                    //Get next Url to route request
                    var url = GetNextUrl();

                    Extensions.LogMessage($"Request Id {requestId} routed to {url}");
                    var response = await client.PostAsync($"{url}{context.Request.Path}", content); //Post to server
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (server != null && response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        serverManager.TrackFailure(server, requestId); //Track Failure if the server fails
                        currentFailures++;
                        continue;
                    }

                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.Headers["Routed-Url"] = url; //Adding custom header for better visibility and unti testing
                    await context.Response.WriteAsync(responseContent);
                    success = true;
                }
                catch (InvalidOperationException)
                {
                    context.Response.StatusCode = 503; // Service Unavailable
                    await context.Response.WriteAsync($"No Services Available to handle request.");
                    success = true;
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    context.Response.StatusCode = StatusCodes.Status408RequestTimeout; //Timeout
                    await context.Response.WriteAsync("Request timed out.");
                }
            }

        });
                
        //This function returns next url to route request
        string GetNextUrl()
        {
            //Conditions to fetch next server from queue
            if (server == null || currentFailures == maxFailureCountBeforeNextServer || currentRequest >= server.MaxConnections)
            {
                server = serverManager.GetNextServer();
                currentRequest = 0;
                currentFailures = 0;
            }

            //Else return current server's url
            currentRequest++;
            return server.Url;
        }

        app.Run();
    }

    /// <summary>
    /// This function will run in background and trigger Health Check of Idle Servers at configured interval.
    /// </summary>
    /// <param name="interval">Interval to trigger Health Check</param>
    /// <param name="token">Token to trigger cancellation of this periodic health check</param>
    /// <param name="serverManager">Required to call Health Check</param>
    /// <returns></returns>
    public static async Task PeriodicHealthCheck(TimeSpan interval, CancellationToken token, ServerManager serverManager)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(interval, token);
            serverManager.HealthCheck();
        }
    }
}

