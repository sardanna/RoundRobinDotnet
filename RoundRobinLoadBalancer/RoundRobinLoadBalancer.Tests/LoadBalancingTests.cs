using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using RoundRobinLoadBalancer.Models;
using System.Net;
using Xunit;

namespace RoundRobinLoadBalancer.Tests
{
    /// <summary>
    /// This class contains test cases which test Load Balancer capabilities under different scenarios.
    /// </summary>
    public class LoadBalancingTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private Mock<ServerManager> _serverManagerMock;
        private readonly List<ApiServer> _availableServers;
        private readonly List<ApiServer> _idlServers;
        private WebApplicationFactory<Program> _factory;
        private readonly ConfigurationManager configuration;

        public LoadBalancingTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;

            //Setting up 3 servers based on which scnearios will be created.
            _availableServers =
            [
                new("https://localhost:7060", 3),
                new("https://localhost:7061", 3),
                new("https://localhost:7062", 3)
            ];
            _idlServers = [];

            //Mock for http requests
            _handlerMock = new Mock<HttpMessageHandler>();
            _handlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("Mock Response")
                    });

            //Mock COnfigurations
            configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new List<KeyValuePair<string, string?>> // Specify string? for nullability
            {
                new("Apis:0:url", "https://localhost:7060"),
                new("Apis:0:maxConnections", "3"),
                new("Apis:1:url", "https://localhost:7061"),
                new("Apis:1:maxConnections", "3"),
                new("Apis:2:url", "https://localhost:7062"),
                new("Apis:2:maxConnections", "3"),
                new("LoadBalancer:HealthCheckIntervalInMinutes", "10"),
                new("LoadBalancer:MaxFailureCountBeforeNextServer", "2"),
                new("LoadBalancer:WeightThreshold", "30"),
                new("LoadBalancer:FailureThreshold", "3"),
                new("LoadBalancer:FailureResetLimitInMinutes", "30")
            });
        }

        /// <summary>
        /// This is a private function which injects Load Balancer with custom services
        /// </summary>
        /// <param name="serverManager">Mock object for Server Manager Class</param>
        /// <param name="handler">Mock object for Http Handler</param>
        private void AddDynamicServices(Mock<ServerManager> serverManager, Mock<HttpMessageHandler>? handler)
        {
            if(serverManager != null)
            {
                // Configure WebApplicationFactory with custom configuration
                _factory = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        // Add custom ConfigurationManager to the configuration sources
                        config.AddConfiguration(configuration);
                    });

                    builder.ConfigureServices(services =>
                    {
                        // Register the mock handler with the HttpClient
                        services.AddHttpClient("LoadBalancedClient")
                                .ConfigurePrimaryHttpMessageHandler(() => handler != null ? handler.Object : _handlerMock.Object);

                        // Remove existing ServerManager registration
                        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ServerManager));
                        if (descriptor != null)
                        {
                            services.Remove(descriptor);
                        }

                        // Add mock ServerManager service
                        services.AddSingleton(serverManager.Object);
                    });
                });
            }
        }

        /// <summary>
        /// This is a private method which mocks Http Handler with a few failure scenarios.
        /// </summary>
        /// <param name="handlerMock"></param>
        /// <returns></returns>
        private static Mock<HttpMessageHandler> SetupHttpMessageHandler()
        {
            Mock<HttpMessageHandler> handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                // Returns OK for the first 3 calls
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                // Returns Internal Server Error for the 4th and 5th calls
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Error Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Error Response")
                })
                // Returns OK for the next 3 calls
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                // Returns Internal Server Error for the 9th and 10th calls
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Error Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("Error Response")
                })
                //Return Ok for remaining calls
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Mock Response")
                })
                .ReturnsAsync(new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.OK,
                     Content = new StringContent("Mock Response")
                 })
                .ReturnsAsync(new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.OK,
                     Content = new StringContent("Mock Response")
                 })
                .ReturnsAsync(new HttpResponseMessage
                 {
                     StatusCode = HttpStatusCode.OK,
                     Content = new StringContent("Mock Response")
                 });
            return handlerMock;
        }

        /// <summary>
        /// This test case covers ideal scenario where a request is routed and recieves a response
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldProcessRequests_WhenApisAreAvailable()
        {
            //Araange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            AddDynamicServices(_serverManagerMock, null);
            var client = _factory.CreateClient();

            //Act
            var response = await client.GetAsync("/");

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// This test case checks that Load Balancer returns Service Unavailable when no Apis are available to route request
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldRespondServiceUnavailable_WhenApisAreNotAvailable()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(new List<ApiServer>() , _idlServers, configuration);
            AddDynamicServices(_serverManagerMock, null);
            var client = _factory.CreateClient();

            //Act
            var response = await client.GetAsync("/");

            //Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        /// <summary>
        /// This test case checks server with highest weights are selected for routing.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldRouteToHighestWeightServer_WhenNewRequestComes()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            _serverManagerMock.SetupSequence(x => x.CalculateWeight())
                .Returns(88)
                .Returns(98) 
                .Returns(98); //setting highest weight for server with port 7062

            AddDynamicServices(_serverManagerMock, null);
            var client = _factory.CreateClient();
            var routedUrls = new List<string>();

            //Act
            for (int i = 0; i < 10; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            //Assert
            Assert.Contains("7062", routedUrls[9]); //checking 10th request as first 9 requests (3 servers x 3 max connections) work with equeal weights
        }

        /// <summary>
        /// This test case checks if a server's weight is below threshold then it is removed from the list of available servers
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldRemoveFromAvailableServers_WhenServerWeightIsBelowThreshold()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            _serverManagerMock.SetupSequence(x => x.CalculateWeight())
                .Returns(88)
                .Returns(98)
                .Returns(15) //setting lowest weight for server with port 7062
                .Returns(75)
                .Returns(85);

            AddDynamicServices(_serverManagerMock, null);
            var client = _factory.CreateClient();
            var routedUrls = new List<string>();

            //Act
            for (int i = 0; i < 15; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            //Assert
            Assert.Equal(3, routedUrls.Count(x => x.Contains("7062"))); //only 3 requests should be routed as per initial equal weights
        }

        /// <summary>
        /// This test case checks if a server is down/not-responding then it is removed from the list of available servers
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ShouldRemoveFromAvailableServers_WhenServerIsDown()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            _serverManagerMock.SetupSequence(x => x.CalculateWeight())
                .Returns(88)
                .Returns(68)
                .Returns(95) 
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(85);

            Mock<HttpMessageHandler> _handler = SetupHttpMessageHandler();

            AddDynamicServices(_serverManagerMock, _handler);
            var client = _factory.CreateClient();
            var routedUrls = new List<string>();

            //Act
            for (int i = 0; i < 12; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            //Assert
            Assert.Equal(0, routedUrls.Count(x => x.Contains("7062"))); 
        }

        /// <summary>
        /// This test case checks if a server was removed because weight was below threshold 
        /// then it periodic health check will attempt to check and add it again
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task PeriodicCheckShouldReAddServer_WhenRemovedDueToLowWeight()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            _serverManagerMock.SetupSequence(x => x.CalculateWeight())
                .Returns(88)
                .Returns(68)
                .Returns(15) //assigning low weight to server with port 7062
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(95);

            AddDynamicServices(_serverManagerMock, null);
            var client = _factory.CreateClient();
            var routedUrls = new List<string>();

            //Act & Assert
            for (int i = 0; i < 12; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            Assert.Equal(3, routedUrls.Count(x => x.Contains("7062"))); //3 requests from initial weights then removed
            routedUrls = [];

            _serverManagerMock.Object.HealthCheck(); //health check will attempt to add again

            for (int i = 0; i < 6; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            Assert.Equal(3, routedUrls.Count(x => x.Contains("7062"))); //getting requests again
        }

        /// <summary>
        /// This test case checks if a server was removed because of failures
        /// then periodic health check will attempt to check and add it again
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task PeriodicCheckShouldReAddServer_WhenRemovedDueToFailure()
        {
            //Arrange
            _serverManagerMock = new Mock<ServerManager>(_availableServers, _idlServers, configuration);
            _serverManagerMock.SetupSequence(x => x.CalculateWeight())
                .Returns(58)
                .Returns(68)
                .Returns(85)
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(85)
                .Returns(75)
                .Returns(95);

            Mock<HttpMessageHandler> _handler = SetupHttpMessageHandler();

            AddDynamicServices(_serverManagerMock, _handler);
            var client = _factory.CreateClient();
            var routedUrls = new List<string>();

            //Act & Assert
            for (int i = 0; i < 12; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            Assert.Equal(0, routedUrls.Count(x => x.Contains("7062"))); //no requests as service is down
            routedUrls = [];

            _serverManagerMock.Object.HealthCheck(); //health check will attempt to add again

            for (int i = 0; i < 3; i++)
            {
                var response = await client.GetAsync("/");
                if (response.Headers.TryGetValues("Routed-Url", out var headerValues))
                {
                    routedUrls.Add(headerValues.FirstOrDefault() ?? string.Empty);
                }
            }

            Assert.Equal(3, routedUrls.Count(x => x.Contains("7062"))); //getting requests again
        }
    }
}