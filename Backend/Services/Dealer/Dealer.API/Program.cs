using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Dealer.Infrastructure.Persistence;
using Dealer.Infrastructure.Repositories;
using Dealer.Application.Interfaces;
using FluentValidation;
using Shared.Middleware;
using Shared.Application.Behaviors;
using MediatR;
using Serilog;
using Serilog.Events;
using EventBus.Abstractions;
using EventBus.RabbitMq;
using EventBus.Events;
using Dealer.API.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Dealer.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Dealer | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Dealer/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Dealer Service Starting =================");

    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen();
    builder.Services.AddCors(o => o.AddPolicy("All", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_1234567890_must_be_long_enuff";
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
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SkyHorizon",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<DealerDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)));

    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Dealer.Application.Validators.RegisterDealerDtoValidator>();

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["Redis:Host"] ?? "localhost:16379";
        options.InstanceName = "Dealer_";
    });

    // RabbitMQ EventBus
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitMqHost, sp));
    
    // Event Handlers
    builder.Services.AddTransient<DealerPaymentEventHandler>();
    builder.Services.AddTransient<DealerCancellationEventHandler>();

    // Repositories
    builder.Services.AddScoped<IDealerRepository, DealerRepository>();
    builder.Services.AddScoped<IWalletRepository, WalletRepository>();
    builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();

    // HttpClient for cross-service communication (Identity service)
    var identityServiceUrl = builder.Configuration["ServiceUrls:IdentityApi"] ?? "http://identity-api:8080/";
    builder.Services.AddHttpClient("IdentityService", client =>
    {
        client.BaseAddress = new Uri(identityServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DealerDbContext>();
        var maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Log.Information("Attempting database connection (attempt {Attempt}/{Max})...", i + 1, maxRetries);
                db.Database.EnsureCreated();
                Log.Information("Database connection established successfully.");
                break;
            }
            catch (Exception ex)
            {
                if (i == maxRetries - 1) throw;
                Log.Warning("Database not ready (attempt {Attempt}/{Max}): {Message}. Retrying in 5s...", i + 1, maxRetries, ex.Message);
                Thread.Sleep(5000);
            }
        }
        try {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RewardAccounts' and xtype='U')
                CREATE TABLE [RewardAccounts] (
                    [RewardId] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [MembershipTier] nvarchar(max) NOT NULL,
                    [TotalPoints] decimal(18,2) NOT NULL,
                    [EnrollmentDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_RewardAccounts] PRIMARY KEY ([RewardId])
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RewardTransactions' and xtype='U')
                CREATE TABLE [RewardTransactions] (
                    [TxnId] int NOT NULL IDENTITY,
                    [RewardId] int NOT NULL,
                    [Points] decimal(18,2) NOT NULL,
                    [Type] nvarchar(max) NOT NULL,
                    [Description] nvarchar(max) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_RewardTransactions] PRIMARY KEY ([TxnId])
                );
            ");
        } catch(Exception e) { Serilog.Log.Error(e, "Migration error"); }
    }

    // Middleware pipeline
    app.UseSharedMiddleware();
    app.UseSerilogRequestLogging();
    app.MapHealthChecks("/health");
    app.UseCors("All");
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Subscribe to EventBus
    var eventBus = app.Services.GetRequiredService<IEventBus>();
    eventBus.Subscribe<PaymentCompletedEvent, DealerPaymentEventHandler>();
    eventBus.Subscribe<BookingCancelledEvent, DealerCancellationEventHandler>();

    Log.Information("================= Dealer Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Dealer Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
