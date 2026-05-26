using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load Ocelot configuration
var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" || Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Contains("+:8080") == true;
builder.Configuration.AddJsonFile(isDocker ? "ocelot.Docker.json" : "ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Airline Management System — API Gateway",
        Version = "v1",
        Description = "Unified gateway for all Airline microservices"
    });
});

// JWT Authentication — Single Audience
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_1234567890_must_be_long_enuff";
var audience = builder.Configuration["Jwt:Audience"] ?? "SkyHorizon";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AirlineBookingApp",
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(o => o.AddPolicy("All", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Ocelot with Polly QoS resilience
builder.Services.AddOcelot().AddPolly();

var app = builder.Build();

// Correlation ID Middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..12];
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId);
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");

    // Downstream service Swagger endpoints (proxied through SwaggerProxyController)
    c.SwaggerEndpoint("/swagger-proxy/identity/swagger.json", "Identity API");
    c.SwaggerEndpoint("/swagger-proxy/flight/swagger.json", "Flight API (Includes Inventory, Pricing)");
    c.SwaggerEndpoint("/swagger-proxy/booking/swagger.json", "Booking API");
    c.SwaggerEndpoint("/swagger-proxy/payment/swagger.json", "Payment API");
    c.SwaggerEndpoint("/swagger-proxy/operations/swagger.json", "Operations API (Includes Notifications)");
    c.SwaggerEndpoint("/swagger-proxy/dealer/swagger.json", "Dealer API (Includes Rewards, Commission, Analytics)");
});

app.UseRouting();

app.UseCors("All");
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();

    // Gateway health / info endpoint
    endpoints.MapGet("/", () => Results.Ok(new
    {
        Service = "Airline Management System — API Gateway",
        Status = "Running",
        Timestamp = DateTime.UtcNow,
        Routes = new[]
        {
            "/identity/api/**", "/flight/api/**", "/booking/api/**",
            "/payment/api/**", "/notification/api/**", "/operations/api/**",
            "/inventory/api/**", "/pricing/api/**", "/reward/api/**",
            "/dealer/api/**", "/commission/api/**", "/analytics/api/**"
        }
    }));
});

await app.UseOcelot();
app.Run();
