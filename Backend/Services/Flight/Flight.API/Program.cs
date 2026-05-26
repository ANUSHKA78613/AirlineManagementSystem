using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Flight.Infrastructure.Persistence;
using Flight.Infrastructure.Repositories;
using Flight.Application.Interfaces;
using FluentValidation;
using Shared.Middleware;
using Shared.Application.Behaviors;
using MediatR;
using EventBus.Abstractions;
using EventBus.Events;
using EventBus.RabbitMq;
using Flight.API.ReadModel;
using Flight.API.Events;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Flight.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Flight | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Flight/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Flight Service Starting =================");

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
            
            // Debugging JWT Authentication failures
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Error(context.Exception, "JWT Authentication Failed. Token: {Token}", context.Request.Headers["Authorization"]);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Log.Information("JWT Token Validated Successfully for {User}", context.Principal?.Identity?.Name);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Log.Error("JWT Challenge Triggered. Error: {Error}, ErrorDescription: {ErrorDesc}", context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // Write-side DbContext
    builder.Services.AddDbContext<FlightDbContext>(options =>
    {
        Log.Information("Configuring Flight DbContext (Write)");
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
    });

    // Read-side DbContext (Eventual Consistency)
    builder.Services.AddDbContext<FlightReadDbContext>(options =>
    {
        Log.Information("Configuring Flight ReadDbContext (Read — Eventual Consistency)");
        var readConnStr = builder.Configuration.GetConnectionString("ReadConnection")
                          ?? builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(readConnStr, sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
    });

    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Flight.Application.Validators.AddFlightDtoValidator>();

    // Repository
    builder.Services.AddScoped<IFlightRepository, FlightRepository>();

    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "flight_";
    });

    builder.Services.AddHealthChecks();

    // RabbitMQ Event Bus
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitMqHost, sp));

    // Event Handlers
    builder.Services.AddTransient<SeatInventoryEventHandler>();
    builder.Services.AddTransient<CouponUsedEventHandler>();

    // Eventual Consistency: Projection handler (background service)
    builder.Services.AddHostedService<FlightProjectionHandler>();

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FlightDbContext>();
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
        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('Flights', 'U') IS NOT NULL
                   AND COL_LENGTH('Flights', 'GateNumber') IS NULL
                BEGIN
                    ALTER TABLE Flights ADD GateNumber nvarchar(max) NULL;
                END
            ");
        }
        catch { }

        // Safely create missing tables if EnsureCreated bypassed them
        try { 
            db.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('Airports', 'U') IS NULL CREATE TABLE Airports (AirportId int IDENTITY(1,1) PRIMARY KEY, Code nvarchar(450) NOT NULL UNIQUE, Name nvarchar(max) NOT NULL, City nvarchar(max) NOT NULL, Country nvarchar(max) NOT NULL, IsActive bit NOT NULL, CreatedAt datetime2 NOT NULL);
                IF OBJECT_ID('Routes', 'U') IS NULL CREATE TABLE Routes (RouteId int IDENTITY(1,1) PRIMARY KEY, Source nvarchar(450) NOT NULL, Destination nvarchar(450) NOT NULL, DistanceKm int NOT NULL, IsActive bit NOT NULL, CreatedAt datetime2 NOT NULL);
                IF OBJECT_ID('Coupons', 'U') IS NULL CREATE TABLE Coupons (CouponId int IDENTITY(1,1) PRIMARY KEY, Code nvarchar(450) NOT NULL UNIQUE, DiscountPercent decimal(5,2) NOT NULL, IsActive bit NOT NULL, ExpiresAt datetime2 NOT NULL, UsageLimit int NOT NULL, UsedCount int NOT NULL, CreatedAt datetime2 NOT NULL);
                IF OBJECT_ID('FlightPricings', 'U') IS NULL CREATE TABLE FlightPricings (Id int IDENTITY(1,1) PRIMARY KEY, FlightId int NOT NULL, Class nvarchar(max) NOT NULL DEFAULT 'Economy', BasePrice decimal(18,2) NOT NULL, Multiplier decimal(18,2) NOT NULL DEFAULT 1.0, IsActive bit NOT NULL DEFAULT 1);
                IF OBJECT_ID('SeatConfigurations', 'U') IS NULL CREATE TABLE SeatConfigurations (Id int IDENTITY(1,1) PRIMARY KEY, FlightId int NOT NULL, Class nvarchar(max) NOT NULL DEFAULT 'Economy', TotalCapacity int NOT NULL, TotalRows int NOT NULL, ColumnsLayout nvarchar(max) NOT NULL DEFAULT '3-3', AisleMarkup decimal(18,2) NOT NULL DEFAULT 0, WindowMarkup decimal(18,2) NOT NULL DEFAULT 0, MiddleMarkup decimal(18,2) NOT NULL DEFAULT 0);
                IF OBJECT_ID('CouponUsages', 'U') IS NULL 
                BEGIN
                    CREATE TABLE CouponUsages (CouponUsageId int IDENTITY(1,1) PRIMARY KEY, CouponCode nvarchar(450) NOT NULL, UserId int NOT NULL, PNR nvarchar(max) NOT NULL, UsedAt datetime2 NOT NULL);
                    CREATE INDEX IX_CouponUsages_Code_UserId ON CouponUsages (CouponCode, UserId);
                END
            ");
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Flights', 'U') IS NOT NULL AND COL_LENGTH('Flights', 'SourceTimeZone') IS NULL ALTER TABLE Flights ADD SourceTimeZone nvarchar(max) NOT NULL DEFAULT 'Asia/Kolkata';");
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Flights', 'U') IS NOT NULL AND COL_LENGTH('Flights', 'DestinationTimeZone') IS NULL ALTER TABLE Flights ADD DestinationTimeZone nvarchar(max) NOT NULL DEFAULT 'Asia/Kolkata';");
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Flights', 'U') IS NOT NULL UPDATE Flights SET SourceTimeZone = 'Asia/Kolkata' WHERE SourceTimeZone IS NULL;");
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Flights', 'U') IS NOT NULL UPDATE Flights SET DestinationTimeZone = 'Asia/Kolkata' WHERE DestinationTimeZone IS NULL;");
            // Add new columns to Seats table
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Seats', 'U') IS NOT NULL AND COL_LENGTH('Seats', 'SeatClass') IS NULL ALTER TABLE Seats ADD SeatClass nvarchar(max) NOT NULL DEFAULT 'Economy';");
            db.Database.ExecuteSqlRaw("IF OBJECT_ID('Seats', 'U') IS NOT NULL AND COL_LENGTH('Seats', 'Category') IS NULL ALTER TABLE Seats ADD Category nvarchar(max) NOT NULL DEFAULT 'Standard';");
        } catch { }

        // Ensure read-side database is ready
        var readDb = scope.ServiceProvider.GetRequiredService<FlightReadDbContext>();
        try { readDb.Database.EnsureCreated(); } catch { }
        try { readDb.Database.ExecuteSqlRaw("IF OBJECT_ID('FlightReadModels', 'U') IS NOT NULL AND COL_LENGTH('FlightReadModels', 'SourceTimeZone') IS NULL ALTER TABLE FlightReadModels ADD SourceTimeZone nvarchar(max) NOT NULL DEFAULT 'Asia/Kolkata';"); } catch { }
        try { readDb.Database.ExecuteSqlRaw("IF OBJECT_ID('FlightReadModels', 'U') IS NOT NULL AND COL_LENGTH('FlightReadModels', 'DestinationTimeZone') IS NULL ALTER TABLE FlightReadModels ADD DestinationTimeZone nvarchar(max) NOT NULL DEFAULT 'Asia/Kolkata';"); } catch { }
        try { readDb.Database.ExecuteSqlRaw("IF OBJECT_ID('FlightReadModels', 'U') IS NOT NULL UPDATE FlightReadModels SET SourceTimeZone = 'Asia/Kolkata' WHERE SourceTimeZone IS NULL;"); } catch { }
        try { readDb.Database.ExecuteSqlRaw("IF OBJECT_ID('FlightReadModels', 'U') IS NOT NULL UPDATE FlightReadModels SET DestinationTimeZone = 'Asia/Kolkata' WHERE DestinationTimeZone IS NULL;"); } catch { }
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
    eventBus.Subscribe<SeatInventoryChangedEvent, SeatInventoryEventHandler>();
    eventBus.Subscribe<TicketCancelledEvent, SeatInventoryEventHandler>();
    eventBus.Subscribe<CouponUsedEvent, CouponUsedEventHandler>();

    Log.Information("================= Flight Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Flight Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
