using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Polly;
using Polly.CircuitBreaker;

var services = new ServiceCollection();

// Configure console logging
services.AddLogging(builder =>
{
    builder.AddSimpleConsole(configure =>
    {
        configure.ColorBehavior = LoggerColorBehavior.Enabled;
        configure.SingleLine = true;
    });
});

// Create a named http client
var httpClientBuilder = services.AddHttpClient("MyHttpClient", httpClient =>
{ 
    httpClient.BaseAddress = new("https://jsonplaceholder.typicode.com");
});

httpClientBuilder.AddResilienceHandler("CustomPipeline", builder =>
{
    // See: https://www.pollydocs.org/strategies/retry.html
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        // Customize and configure the retry logic.
        BackoffType = DelayBackoffType.Constant,
        Delay = TimeSpan.FromSeconds(1),
        MaxRetryAttempts = 3,
        UseJitter = true,
        ShouldHandle = args =>
        {
            return ValueTask.FromResult(args is { Outcome.Result.StatusCode: HttpStatusCode.RequestTimeout });
        }
    });

    // See: https://www.pollydocs.org/strategies/circuit-breaker.html
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        // Customize and configure the circuit breaker logic.
        BreakDuration = TimeSpan.FromSeconds(15),
        SamplingDuration = TimeSpan.FromSeconds(30),
        FailureRatio = 0.2,
        MinimumThroughput = 3,
        ShouldHandle = args =>
        {
            return ValueTask.FromResult(args is { Outcome.Result.StatusCode: HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests });
        }
    });

    // See: https://www.pollydocs.org/strategies/timeout.html
    builder.AddTimeout(TimeSpan.FromSeconds(5));
});

// Add message handler to simulate http errors
httpClientBuilder.AddHttpMessageHandler(x => new FailingDelegatingHandler());

var serviceProvider = services.BuildServiceProvider();

var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("Default");

try
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("MyHttpClient");
    var response = await httpClient.GetAsync("todos/1");
    logger.LogInformation("Http response status code: {statusCode}", response.StatusCode);
}
catch (BrokenCircuitException e)
{
    logger.LogError("Http response error: {statusCode}", e.Message);
}
