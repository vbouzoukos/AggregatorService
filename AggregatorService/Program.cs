using AggregatorService.Middleware;
using AggregatorService.Services.Authorise;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Providers;
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
builder.Services.AddScoped<IExternalApiProvider, WeatherProvider>();
// Caching
// Currently using in-memory distributed cache for development
// To enable Redis, replace AddDistributedMemoryCache() with:
//   builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");
// Two-tier caching improvement: Use IMemoryCache (L1) for fast local access,
// fall back to Redis (L2) on miss, then populate L1 for subsequent requests
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
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

app.Run();
