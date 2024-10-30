# RoundRobinDotnet
This is an example of Round Robin Load Balancing created in .Net 8

## Project Structure
 - **RoundRobinLoadBalancerApi**  
  A dummy API with one endpoint that returns the JSON body passed to it. This API is configured to run on 3 ports simultaneously when run locally.

- **RoundRobinLoadBalancer**
  - **RoundRobinLoadBalancer**  
    This is the main Load Balancer. It reads initial configurations from `appsettings.json`, including a list of available servers.

  - **RoundRobinLoadBalancer.Tests**  
    This is the unit test project for the Load Balancer, using xUnit to test multiple scenarios.

## Basic Functioning
This application assumes a Dynamic Weighted Round Robin Algorithm. For example if we have three available servers A,B and C, the one with highest weight is selected and requests are routed to this server until its MaxConnections Limit before selecting next highest weight server. The Balancer starts by giving initial equal weights to all available servers and utilizes a PriorityQueue to fetch next server where requests should be routed based on its weights. Once the queue is near empty weights are dynamically recalculated by a method mimicing fetching server metrics like CPU, Memory and Disk status and calculating a weight based on F5 Server's Dynamic Ratio Load Traafic Management system. As new weights are calculated the server is either added back to the Priority Queue with new weight or is removed from the list of available servers if its weight falls below allowed threshold.

In case a server is healthy in terms of weight but is not responding to API calls, there is a failure tracking system which removes a server from available servers based on 'n' number of failures in 'm' amount of time (both configurable). 

There is a Periodic Task which runs every few minutes (configurable) which does a health check on servers which have been removed from available servers due to failure or weight falling below threshold. If a server becomes healthy again it is added back to the list of available servers.

Hence, in this algorithm requests are routed in a Round Robin fashion based on Dynamic Weights.
Slowness of a server is handled by assigning it low weights so lesser number of requests are routed towards it.
If a server goes down, no more requests are routed towards it until it is healthy again.

Unit Test Project for this Load Balancer covers following scenarios : 
Given we start with 3 Servers having Max Connections limit of 3 each - 
Test if highest weighted server is returned from the queue.
Queue regenerates with newly updated weights when near completion.
If a server goes down it is removed and added back when it becomes healthy as part of Periodic Health Check
If a server goes down due to new weight being less than threshold, it is removed and added back when it becomes healthy as part of Periodic Health Check

The Load Balancer adds a header called "Routed-Url" to the response which indicates which server fulfilled the request and same is being used in unit testing as well.

