using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using FluentValidation;
using Shared.Middleware;
using MassTransit;
using Shared.EmailService;
using Shared.ImageService;
using Shared.Application.Behaviors;
using MediatR;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);


// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Identity.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Identity | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Identity/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Identity Service Starting =================");

    builder.Services.AddControllers();

    // Swagger with JWT
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_1234567890_must_be_long_enuff";
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AirlineBookingApp",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SkyHorizon",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        })
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClientId = builder.Configuration["Google:ClientId"] ?? "placeholder-google-client-id";
            options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "placeholder-google-client-secret";
            options.CallbackPath = "/signin-google";
            options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnRedirectToAuthorizationEndpoint = context =>
                {
                    context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddCors(o => o.AddPolicy("All", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
    
    builder.Services.AddDbContext<IdentityDbContext>(options =>
    {
        Log.Information("Configuring Identity DbContext");
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddScoped<Identity.Application.Interfaces.IIdentityDbContext>(provider => provider.GetRequiredService<IdentityDbContext>());
    
    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Identity.Application.CQRS.Commands.RegisterUserCommand).Assembly);
    });
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Identity.Application.Validators.RegisterDtoValidator>();

    // Repository
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Email Service
    builder.Services.AddEmailService();
    builder.Services.AddScoped<Identity.Application.Interfaces.IEmailService, Identity.Infrastructure.Services.EmailServiceAdapter>();

    builder.Services.AddScoped<Identity.Application.Interfaces.IJwtTokenService, Identity.Infrastructure.Services.JwtTokenService>();
    builder.Services.AddScoped<Identity.Application.Interfaces.IOtpService, Identity.Infrastructure.Services.OtpService>();
    builder.Services.AddScoped<Identity.Application.Interfaces.IRefreshTokenService, Identity.Application.Interfaces.RefreshTokenService>();

    // Image Service
    builder.Services.AddImageService();

    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "identity_";
    });

    // HttpClient for cross-service communication (Dealer service)
    var dealerServiceUrl = builder.Configuration["ServiceUrls:DealerApi"] ?? "http://dealer-api:8080/";
    builder.Services.AddHttpClient("DealerService", client =>
    {
        client.BaseAddress = new Uri(dealerServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(5); // Short timeout to avoid blocking login
    });

    // MassTransit & RabbitMQ Configuration
    builder.Services.AddMassTransit(x =>
    {
        // Add consumers here if Identity service subscribes to events
        
        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            cfg.Host(rabbitMqHost, "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });
            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();
        var maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Log.Information("Attempting database connection (attempt {Attempt}/{Max})...", i + 1, maxRetries);
                db.Database.Migrate();
                Log.Information("Database connection established successfully.");
                
                // Seed database
                await Identity.Infrastructure.Persistence.IdentityDataSeeder.SeedAsync(app.Services);
                Log.Information("Database seeding completed.");
                
                break;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw;
                Log.Warning("Database not ready (attempt {Attempt}/{Max}): {Message}. Retrying in 5s...", i + 1, maxRetries, ex.Message);
                Thread.Sleep(5000);
            }
        }
    }

    // Middleware pipeline
    app.UseSharedMiddleware();
    app.UseSerilogRequestLogging();
    app.MapHealthChecks("/health");
    app.UseCors("All");
    app.UseStaticFiles(); // Serve uploaded images from wwwroot/uploads/
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("================= Identity Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}


