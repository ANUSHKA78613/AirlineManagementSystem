using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Payment.Infrastructure.Persistence;
using Payment.Infrastructure.Repositories;
using Payment.Application.Interfaces;
using FluentValidation;
using Shared.Middleware;
using Shared.Application.Behaviors;
using MediatR;
using EventBus.Abstractions;
using EventBus.RabbitMq;
using EventBus.Events;
using Payment.API.Saga;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Payment.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Payment | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Payment/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Payment Service Starting =================");

    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen();
    builder.Services.AddCors(o => o.AddPolicy("All", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
    builder.Services.AddHttpClient();

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

    builder.Services.AddDbContext<PaymentDbContext>(options =>
    {
        Log.Information("Configuring Payment DbContext");
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
    });

    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Payment.Application.Validators.ProcessPaymentDtoValidator>();

    // Repository + Services
    builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
    builder.Services.AddScoped<Payment.API.Services.IRazorpayService, Payment.API.Services.RazorpayService>();

    // Refund Event Handler and Worker
    builder.Services.AddTransient<Payment.API.Saga.RefundEventHandler>();
    builder.Services.AddHostedService<Payment.API.Saga.RefundProcessingWorker>();

    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "payment_";
    });

    builder.Services.AddHealthChecks();
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitMqHost, sp));

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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

        // Add refund tracking columns if they don't exist yet
        try
        {
            db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'RefundId')
                    ALTER TABLE Payments ADD RefundId NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'RefundAmount')
                    ALTER TABLE Payments ADD RefundAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'RefundedAt')
                    ALTER TABLE Payments ADD RefundedAt DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'RefundStatus')
                    ALTER TABLE Payments ADD RefundStatus NVARCHAR(50) NOT NULL DEFAULT 'None';
            ");
            Log.Information("Refund tracking columns ensured on Payments table.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not add refund columns (may already exist)");
        }
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
    var sagaHandler = new BookingCreatedEventHandler(app.Services, eventBus);
    eventBus.Subscribe<BookingCreatedEvent>(sagaHandler.Handle);

    // Subscribe to BookingCancelledEvent for refund processing
    eventBus.Subscribe<BookingCancelledEvent, Payment.API.Saga.RefundEventHandler>();

    Log.Information("================= Payment Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Payment Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
