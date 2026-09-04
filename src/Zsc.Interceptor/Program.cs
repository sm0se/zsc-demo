using Zsc.CommonLib.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

// Configure the discovery client to point to the service discovery service
var discoveryBaseUrl = builder.Configuration["ServiceDiscovery:BaseUrl"] ?? "http://localhost:5300";
builder.Services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
{
    client.BaseAddress = new Uri(discoveryBaseUrl);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5200, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

// Every inbound ZSC call funnels through here and gets forwarded to the
// appropriate service resolved from the service discovery registry.
// Correlation IDs are generated here and propagated downstream.
app.MapMethods("/api/{service}/{**path}", new[] { "GET", "POST", "PUT", "DELETE" },
    async (string service, string path, HttpRequest request, HttpResponse response,
           IHttpClientFactory httpClientFactory, IServiceDiscoveryClient discoveryClient, ILogger<Program> logger) =>
    {
        // Generate or extract correlation ID
        var correlationId = request.Headers.ContainsKey("X-Correlation-Id")
            ? request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        logger.LogInformation("Forwarding {Method} /{Service}/{Path} [CorrelationId={CorrelationId}]", 
            request.Method, service, path, correlationId);

        var serviceInfo = await discoveryClient.ResolveAsync(service);
        if (serviceInfo?.HttpBaseUrl is null)
        {
            logger.LogWarning("Service discovery could not resolve '{Service}' [CorrelationId={CorrelationId}]", 
                service, correlationId);
            response.StatusCode = StatusCodes.Status502BadGateway;
            response.Headers.Add("X-Correlation-Id", correlationId);
            await response.WriteAsync($"Interceptor could not discover '{service}'.");
            return;
        }

        var client = httpClientFactory.CreateClient();
        var forwardUri = new Uri(new Uri(serviceInfo.HttpBaseUrl), $"/{path}{request.QueryString}");
        var forwardRequest = new HttpRequestMessage(new HttpMethod(request.Method), forwardUri);
        
        // Propagate correlation ID
        forwardRequest.Headers.Add("X-Correlation-Id", correlationId);

        if (request.ContentLength is > 0)
        {
            forwardRequest.Content = new StreamContent(request.Body);
            if (!string.IsNullOrEmpty(request.ContentType))
                forwardRequest.Content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
        }

        try
        {
            using var upstream = await client.SendAsync(forwardRequest);
            response.StatusCode = (int)upstream.StatusCode;
            response.Headers.Add("X-Correlation-Id", correlationId);
            if (upstream.Content.Headers.ContentType is { } contentType)
                response.ContentType = contentType.ToString();
            var body = await upstream.Content.ReadAsByteArrayAsync();
            await response.Body.WriteAsync(body);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reach {Service} [CorrelationId={CorrelationId}]", service, correlationId);
            response.StatusCode = StatusCodes.Status502BadGateway;
            response.Headers.Add("X-Correlation-Id", correlationId);
            await response.WriteAsync($"Interceptor could not reach '{service}'.");
        }
    });

app.Run();

public partial class Program { }
