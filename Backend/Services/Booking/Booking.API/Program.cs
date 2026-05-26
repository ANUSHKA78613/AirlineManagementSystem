using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Booking.Infrastructure.Persistence;
using Booking.Infrastructure.Repositories;
using Booking.Application.Interfaces;
using FluentValidation;
using Shared.Middleware;
using Shared.EmailService;
using Shared.Application.Behaviors;
using MediatR;
using EventBus.Abstractions;
using EventBus.RabbitMq;
using EventBus.Events;
using Booking.API.Saga;
using Booking.API.ReadModel;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Booking.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Booking | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Booking/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Booking Service Starting =================");

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

    // Write-side DbContext
    builder.Services.AddDbContext<BookingDbContext>(options =>
    {
        Log.Information("Configuring Booking DbContext (Write)");
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
    });

    // Read-side DbContext (Eventual Consistency)
    builder.Services.AddDbContext<BookingReadDbContext>(options =>
    {
        Log.Information("Configuring Booking ReadDbContext (Read — Eventual Consistency)");
        var readConnStr = builder.Configuration.GetConnectionString("ReadConnection")
                          ?? builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(readConnStr, sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
    });

    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Booking.Application.Validators.CreateBookingDtoValidator>();

    // Repository
    builder.Services.AddScoped<IBookingRepository, BookingRepository>();
    builder.Services.AddScoped<IPassengerRepository, PassengerRepository>();

    // Email Service
    builder.Services.AddEmailService();

    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "booking_";
    });

    builder.Services.AddHealthChecks();
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitMqHost, sp));

    // Eventual Consistency: Projection handler (background service)
    builder.Services.AddHostedService<BookingProjectionHandler>();

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
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

        // Ensure read-side database is ready
        var readDb = scope.ServiceProvider.GetRequiredService<BookingReadDbContext>();
        readDb.Database.EnsureCreated();
        try {
            readDb.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BookingReadModels' and xtype='U')
                CREATE TABLE [BookingReadModels] (
                    [BookingId] int NOT NULL IDENTITY,
                    [PNR] nvarchar(450) NOT NULL,
                    [UserId] int NOT NULL,
                    [FlightId] int NOT NULL,
                    [FlightNumber] nvarchar(max) NULL,
                    [Source] nvarchar(max) NULL,
                    [Destination] nvarchar(max) NULL,
                    [DepartureTime] datetime2 NULL,
                    [Status] nvarchar(max) NOT NULL,
                    [TotalAmount] decimal(18,2) NOT NULL,
                    [PassengerCount] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [LastUpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_BookingReadModels] PRIMARY KEY ([PNR])
                );
            ");
        } catch(Exception e) { Serilog.Log.Error(e, "Migration error for BookingReadModels"); }
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

    var eventBus = app.Services.GetRequiredService<IEventBus>();
    var sagaHandler = new PaymentResultEventHandler(app.Services);
    eventBus.Subscribe<PaymentCompletedEvent>(sagaHandler.HandlePaymentCompleted);
    eventBus.Subscribe<PaymentFailedEvent>(sagaHandler.HandlePaymentFailed);
    eventBus.Subscribe<RefundCompletedEvent>(sagaHandler.HandleRefundCompleted);

    Log.Information("================= Booking Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Booking Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
