using Zsc.CommonLib;
using Zsc.CommonLib.ServiceDiscovery;
using Zsc.Interceptor.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var discoveryUrl = builder.Configuration["ServiceDiscovery:Url"] ?? "http://localhost:5300";
builder.Services.AddHttpClient<IServiceDiscoveryClient, HttpServiceDiscoveryClient>(client =>
{
    client.BaseAddress = new Uri(discoveryUrl);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5200, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

app.UseCorrelationId();

// Every inbound ZSC call funnels through here and gets forwarded based on
// ServiceDiscovery runtime registration. Correlation IDs are generated
// on inbound and propagated downstream.
app.MapMethods("/api/{service}/{**path}", new[] { "GET", "POST", "PUT", "DELETE" },
    async (string service, string path, HttpContext context, HttpRequest request, HttpResponse response,
           IServiceDiscoveryClient discoveryClient, IHttpClientFactory httpClientFactory, 
           ILogger<Program> logger) =>
    {
        var correlationId = context.GetCorrelationId();
        
        ServiceEntry? entry;
        try
        {
            entry = await discoveryClient.ResolveAsync(service);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("Service '{Service}' not found in discovery", service);
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync($"Interceptor has no route for '{service}'.");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resolving service '{Service}' from discovery", service);
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync($"Interceptor could not reach service discovery for '{service}'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.HttpBaseUrl))
        {
            logger.LogWarning("Service '{Service}' has no HTTP endpoint", service);
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync($"Service '{service}' does not expose an HTTP endpoint.");
            return;
        }

        logger.LogInformation("Forwarding {Method} /{Service}/{Path} with correlation ID {CorrelationId}", 
            request.Method, service, path, correlationId);

        var client = httpClientFactory.CreateClient();
        var forwardUri = new Uri(new Uri(entry.HttpBaseUrl), $"/{path}{request.QueryString}");
        var forwardRequest = new HttpRequestMessage(new HttpMethod(request.Method), forwardUri);

        // Propagate correlation ID
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            forwardRequest.Headers.Add(CorrelationIdConstants.HeaderName, correlationId);
        }

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
            if (upstream.Content.Headers.ContentType is { } contentType)
                response.ContentType = contentType.ToString();
            var body = await upstream.Content.ReadAsByteArrayAsync();
            await response.Body.WriteAsync(body);
            
            logger.LogInformation("Forwarded {Method} /{Service}/{Path} completed with status {StatusCode}",
                request.Method, service, path, (int)upstream.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reach {Service}", service);
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync($"Interceptor could not reach '{service}'.");
        }
    });

app.Run();

public partial class Program { }
