using AggregatorService.Middleware;
using AggregatorService.Services.Aggregation;
using AggregatorService.Services.Authorise;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Monitoring;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Reflection;
using System.Text;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AggregatorService");

    var builder = WebApplication.CreateBuilder(args);

    //--------------------------------------------------------------------------------
    // Configuration
    //--------------------------------------------------------------------------------
    var env = builder.Environment.EnvironmentName;
    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{env}.json", optional: true)
        .AddEnvironmentVariables();

    //--------------------------------------------------------------------------------
    // Serilog Configuration - Console (stdout) + File logging
    //--------------------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var config = context.Configuration;

        configuration
            .ReadFrom.Configuration(config)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("EnvironmentName", context.HostingEnvironment.EnvironmentName);

        // Console/stdout logging (enabled by default)
        if (config.GetValue("Serilog:EnableConsole", true))
        {
            configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        // File logging (enabled by default for on-premises)
        if (config.GetValue("Serilog:EnableFile", true))
        {
            configuration.WriteTo.File(
                path: config["Serilog:FilePath"] ?? "logs/aggregator-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: config.GetValue("Serilog:RetainedFileCountLimit", 30),
                fileSizeLimitBytes: config.GetValue("Serilog:FileSizeLimitBytes", 104857600),
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
    });

    //--------------------------------------------------------------------------------
    // Add services to the container.
    //--------------------------------------------------------------------------------
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IIdentityProvider, IdentityProvider>();

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
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
        };
    });

    builder.Services.AddScoped<IAggregationService, AggregationService>();
    builder.Services.AddTransient<IExternalApiProvider, WeatherProvider>();
    builder.Services.AddTransient<IExternalApiProvider, NewsProvider>();
    builder.Services.AddTransient<IExternalApiProvider, OpenLibraryProvider>();
    builder.Services.AddTransient<IExternalApiProvider, OpenAIProvider>();
    builder.Services.AddSingleton<IStatisticsService, StatisticsService>();
    builder.Services.AddHostedService<PerformanceMonitorService>();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddTransient<ICacheService, CacheService>();

    builder.Services.AddHttpClient("", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(180);
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token"
        });
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.GetLevel = (ctx, elapsed, ex) =>
        {
            if (ex != null)
                return Serilog.Events.LogEventLevel.Error;

            if (ctx.Response.StatusCode > 499)
                return Serilog.Events.LogEventLevel.Error;

            return Serilog.Events.LogEventLevel.Information;
        };
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseMiddleware<GlobalErrorHandlerMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}