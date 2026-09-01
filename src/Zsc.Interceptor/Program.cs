using Zsc.CommonLib.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5200, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

// Every inbound ZSC call funnels through here and gets forwarded based on
// Zsc.CommonLib's hardcoded route map. Notice there's no correlation id
// generated or propagated on the way through - every hop logs in isolation,
// which is exactly why tracing a request across services is so hard today.
app.MapMethods("/api/{service}/{**path}", new[] { "GET", "POST", "PUT", "DELETE" },
    async (string service, string path, HttpRequest request, HttpResponse response,
           IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
    {
        ServiceRouteEntry entry;
        try
        {
            entry = ServiceRouteMap.Resolve(service);
        }
        catch (KeyNotFoundException)
        {
            response.StatusCode = StatusCodes.Status502BadGateway;
            await response.WriteAsync($"Interceptor has no route for '{service}'.");
            return;
        }

        logger.LogInformation("Forwarding {Method} /{Service}/{Path}", request.Method, service, path);

        var client = httpClientFactory.CreateClient();
        var forwardUri = new Uri(new Uri(entry.HttpBaseUrl), $"/{path}{request.QueryString}");
        var forwardRequest = new HttpRequestMessage(new HttpMethod(request.Method), forwardUri);

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
