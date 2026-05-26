using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>
/// Proxies swagger.json from downstream microservices so that the Gateway
/// Swagger UI can display them in the dropdown without CORS issues.
/// </summary>
[ApiController]
[Route("swagger-proxy")]
[ApiExplorerSettings(IgnoreApi = true)]          // hide from the gateway's own swagger doc
public class SwaggerProxyController : ControllerBase
{
    private static string GetServiceUrl(string service)
    {
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Contains("+:8080") == true;
        if (isDocker)
        {
            return $"http://{service}-api:8080";
        }
        
        return service switch
        {
            "identity"   => "http://localhost:7001",
            "flight"     => "http://localhost:8003",
            "booking"    => "http://localhost:7002",
            "payment"    => "http://localhost:9004",
            "operations" => "http://localhost:9003",
            "dealer"     => "http://localhost:8002",
            _ => null
        };
    }

    private readonly IHttpClientFactory _httpClientFactory;

    public SwaggerProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("{service}/swagger.json")]
    public async Task<IActionResult> GetSwaggerDoc(string service)
    {
        var baseUrl = GetServiceUrl(service.ToLowerInvariant());
        if (baseUrl == null)
            return NotFound($"Unknown service: {service}");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var json = await client.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");

            // For identity, we don't prefix with "/identity" because Auth routes are mapped to root in Ocelot
            string serverPrefix = service.ToLowerInvariant() == "identity" ? "" : $"/{service.ToLowerInvariant()}";
            
            // Inject the servers configuration so that requests from Swagger go through the correct Gateway path
            var serversBlock = $"\"servers\": [ {{ \"url\": \"{serverPrefix}\" }} ],";
            json = json.Insert(json.IndexOf('{') + 1, serversBlock);

            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(502, $"Could not reach {service} swagger: {ex.Message}");
        }
    }
}
