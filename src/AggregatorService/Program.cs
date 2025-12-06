using AggregatorService.Middleware;
using AggregatorService.Services.Aggregation;
using AggregatorService.Services.Authorise;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Monitoring;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Providers;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//--------------------------------------------------------------------------------
//Configuration
//--------------------------------------------------------------------------------
var env = builder.Environment.EnvironmentName;
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
// Optional settings since dockerize the service will use environmental variables
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables();

//--------------------------------------------------------------------------------
// Add services to the container.
//--------------------------------------------------------------------------------
// Dependency injection
//--------------------------------------------------------------------------------
// Authentication with JWT services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IIdentityProvider, IdentityProvider>();

// Authentication
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
//--------------------------------------------------------------------------------
// Aggregation
//--------------------------------------------------------------------------------
builder.Services.AddScoped<IAggregationService, AggregationService>();
//--------------------------------------------------------------------------------
// Providers
//--------------------------------------------------------------------------------
builder.Services.AddTransient<IExternalApiProvider, WeatherProvider>();
builder.Services.AddTransient<IExternalApiProvider, NewsProvider>();
builder.Services.AddTransient<IExternalApiProvider, OpenLibraryProvider>();
// Statistics service (singleton to maintain state across requests)
builder.Services.AddSingleton<IStatisticsService, StatisticsService>();

// Performance monitor background service
builder.Services.AddHostedService<PerformanceMonitorService>();

// Caching
// Currently using in-memory distributed cache for development
// To enable Redis, replace AddDistributedMemoryCache() with:
//  Redis "builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");"
// Two-tier caching improvement: Use IMemoryCache (L1) for fast local access,
// fall back to Redis (L2) on miss, then populate L1 for subsequent requests
builder.Services.AddDistributedMemoryCache();
builder.Services.AddTransient<ICacheService, CacheService>();

//--------------------------------------------------------------------------------
// HttpClient Factory
//--------------------------------------------------------------------------------
builder.Services.AddHttpClient("", client =>
{
    client.Timeout = TimeSpan.FromSeconds(180); // Set your timeout here
});
//--------------------------------------------------------------------------------
// Controllers
//--------------------------------------------------------------------------------
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
    // Add JWT Authentication support to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
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

// Global Exception Handler
app.UseMiddleware<GlobalErrorHandlerMiddleware>();

// Configure the HTTP request pipeline.
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
