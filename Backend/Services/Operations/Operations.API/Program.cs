using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Operations.Infrastructure.Persistence;
using Operations.Infrastructure.Repositories;
using Operations.Application.Interfaces;
using FluentValidation;
using Shared.Middleware;
using Shared.EmailService;
using Shared.Application.Behaviors;
using MediatR;
using Serilog;
using Serilog.Events;
using EventBus.Abstractions;
using EventBus.RabbitMq;
using EventBus.Events;
using Operations.API.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Operations.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] Operations | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/Operations/.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("================= Operations Service Starting =================");

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

    builder.Services.AddDbContext<OperationsDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)));

    // MediatR + FluentValidation pipeline
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddValidatorsFromAssemblyContaining<Operations.Application.Validators.AddEmployeeDtoValidator>();

    // RabbitMQ EventBus
    var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    builder.Services.AddSingleton<IEventBus>(sp => new RabbitMqEventBus(rabbitMqHost, sp));
    
    // Event Handlers
    builder.Services.AddTransient<BookingStatusChangedEventHandler>();
    builder.Services.AddTransient<PaymentCompletedEventHandler>();

    // Repositories
    builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
    builder.Services.AddScoped<IBoardingPassRepository, BoardingPassRepository>();
    builder.Services.AddScoped<IBaggageRepository, BaggageRepository>();
    builder.Services.AddScoped<IIssueRepository, IssueRepository>();

    // Email Service
    builder.Services.AddEmailService();

    // Redis Cache for Analytics
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "operations_";
    });

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Auto-migrate database on startup with retry logic for SQL Server readiness
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
        var maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Log.Information("Attempting database connection (attempt {Attempt}/{Max})...", i + 1, maxRetries);
                db.Database.EnsureCreated();
                try 
                {
                    db.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'PassengerEmail' AND Object_ID = Object_ID(N'Issues'))
                        BEGIN
                            ALTER TABLE Issues ADD PassengerEmail NVARCHAR(MAX) NULL;
                        END
                        IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'StaffReply' AND Object_ID = Object_ID(N'Issues'))
                        BEGIN
                            ALTER TABLE Issues ADD StaffReply NVARCHAR(MAX) NULL;
                        END
                    ");

                } 
                catch (Exception sqlEx) 
                {
                    Log.Warning(sqlEx, "Failed to apply Customer Care schema updates (might not be SQL Server).");
                }
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
    eventBus.Subscribe<BookingStatusChangedEvent, BookingStatusChangedEventHandler>();
    eventBus.Subscribe<PaymentCompletedEvent, PaymentCompletedEventHandler>();

    Log.Information("================= Operations Service Started Successfully =================");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Operations Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
