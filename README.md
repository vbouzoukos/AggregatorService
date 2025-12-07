# API Aggregation Service

A .NET 8 API aggregation service that consolidates data from multiple external APIs and provides a unified endpoint to access the aggregated information.

## Table of Contents

1. [Features](#features)
2. [Quick Start](#quick-start)
3. [Prerequisites](#prerequisites)
4. [Project Structure](#project-structure)
5. [Configuration](#configuration)
6. [API Endpoints](#api-endpoints)
7. [Fallback Mechanism](#fallback-mechanism)
8. [Running the Application](#running-the-application)
9. [Running Tests](#running-tests)
10. [External API Providers](#external-api-providers)
11. [Example Usage](#example-usage)

---

## Features

- Aggregates data from multiple external APIs simultaneously (Weather, News, Books, AIPrompt)
- **AI-powered prompt generation** for data analysis using OpenAI
- **Graceful fallback mechanism** - individual provider failures don't affect others
- Unified filtering and sorting across providers
- JWT Bearer authentication
- Distributed caching (in-memory, Redis-ready)
- Request statistics with performance buckets
- **Performance anomaly detection** with background monitoring
- Global error handling
- Parallel API calls for optimal performance
- Thread-safe statistics tracking
- Configuration-driven provider setup
- Logging both in stdout and in file
---

## Quick Start

Get up and running in 3 steps:

### 1. Get a JWT Token

```bash
curl -X POST http://localhost:5000/api/authorise/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-01-15T12:00:00Z"
}
```

### 2. Make Your First Aggregation Request

```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer <your-token>" \
  -H "Content-Type: application/json" \
  -d '{"query": "technology"}'
```

### 3. Explore the Results

You'll receive aggregated data from News, Books, and AIPrompt providers - all in a single response!

---

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code
- API keys for external services:
  - OpenWeatherMap (for Weather provider)
  - NewsAPI (for News provider)
  - OpenAI (for AIPrompt provider)
  - Open Library (no API key required)

## Project Structure

```
AggregatorService.sln
├── src/
│   └── AggregatorService/
│       ├── Controllers/
│       │   ├── AggregationController.cs
│       │   ├── AuthoriseController.cs
│       │   └── StatisticsController.cs
│       ├── Middleware/
│       │   └── GlobalErrorHandlerMiddleware.cs
│       ├── Models/
│       │   ├── Requests/
│       │   │   ├── AggregationRequest.cs
│       │   │   └── AuthenticationRequest.cs
│       │   └── Responses/
│       │       ├── AggregationResponse.cs
│       │       ├── ApiResponse.cs
│       │       ├── AuthenticationResponse.cs
│       │       ├── PerformanceAnomalyResponse.cs
│       │       ├── PerformanceBuckets.cs
│       │       ├── ProviderStatistics.cs
│       │       └── StatisticsResponse.cs
│       ├── Services/
│       │   ├── Aggregation/
│       │   │   ├── AggregationService.cs
│       │   │   └── IAggregationService.cs
│       │   ├── Authorise/
│       │   │   ├── IdentityProvider.cs
│       │   │   ├── IIdentityProvider.cs
│       │   │   ├── ITokenService.cs
│       │   │   └── TokenService.cs
│       │   ├── Caching/
│       │   │   ├── CacheService.cs
│       │   │   └── ICacheService.cs
│       │   ├── Monitoring/
│       │   │   └── PerformanceMonitorService.cs
│       │   ├── Provider/
│       │   │   ├── Base/
│       │   │   │   └── IExternalApiProvider.cs
│       │   │   ├── NewsProvider.cs
│       │   │   ├── OpenAIProvider.cs
│       │   │   ├── OpenLibraryProvider.cs
│       │   │   └── WeatherProvider.cs
│       │   └── Statistics/
│       │       ├── IStatisticsService.cs
│       │       └── StatisticsService.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json (git ignored)
│       ├── Program.cs
│       └── AggregatorService.csproj
└── tests/
    └── AggregatorService.Tests/
        ├── Controllers/
        │   ├── AggregationControllerTests.cs
        │   ├── AuthoriseControllerTests.cs
        │   └── StatisticsControllerTests.cs
        ├── Middleware/
        │   └── GlobalErrorHandlerMiddlewareTests.cs
        ├── Services/
        │   ├── AggregationServiceTests.cs
        │   ├── CacheServiceTests.cs
        │   ├── IdentityProviderTests.cs
        │   ├── StatisticsServiceTests.cs
        │   └── Providers/
        │       ├── NewsProviderTests.cs
        │       ├── OpenAIProviderTests.cs
        │       ├── OpenLibraryProviderTests.cs
        │       └── WeatherProviderTests.cs
        ├── appsettings.json
        ├── appsettings.Secret.json (git ignored)
        └── AggregatorService.Tests.csproj
```

---

## Configuration

### Step 1: Clone the Repository

```bash
git clone https://github.com/your-repo/AggregatorService.git
cd AggregatorService
```

### Step 2: Configure Application Settings

**appsettings.json** - Main configuration:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "EnableConsole": true,
    "EnableFile": true,
    "FilePath": "logs/aggregator-.log",
    "RetainedFileCountLimit": 30,
    "FileSizeLimitBytes": 104857600,
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.AspNetCore.Authentication": "Warning",
        "System": "Warning"
      }
    }
  },
  "AllowedHosts": "*",
  "JwtSettings": {
    "Issuer": "AggregatorService",
    "Audience": "AggregatorServiceAudience",
    "ExpirationInMinutes": 60
  },
  "ExternalApis": {
    "OpenWeatherMap": {
      "GeocodingUrl": "http://api.openweathermap.org/geo/1.0/direct",
      "WeatherUrl": "https://api.openweathermap.org/data/2.5/weather",
      "CacheGeoDays": 30,
      "CacheDataMinutes": 240,
      "Parameters": [ "exclude", "units" ],
      "Required": [ "city" ],
      "Filters": {
        "Country": "country",
        "Language": "lang"
      },
      "SortMappings": null
    },
    "NewsApi": {
      "Url": "https://newsapi.org/v2/everything",
      "CacheMinutes": 240,
      "Parameters": [ "searchIn", "from", "to" ],
      "Required": [ "q" ],
      "Filters": {
        "Query": "q",
        "Language": "language"
      },
      "SortParameter": "sortBy",
      "SortMappings": {
        "Newest": "publishedAt",
        "Oldest": "publishedAt",
        "Relevance": "relevancy",
        "Popularity": "popularity"
      }
    },
    "OpenLibrary": {
      "Url": "https://openlibrary.org/search.json",
      "CacheMinutes": 30,
      "Parameters": [ "sort" ],
      "Required": [ "q", "title", "author" ],
      "Filters": {
        "Query": "title",
        "Language": "lang"
      },
      "SortParameter": "sort",
      "SortMappings": {
        "Newest": "new",
        "Oldest": "old",
        "Relevance": null,
        "Popularity": "rating"
      }
    },
    "OpenAI": {
      "Url": "https://api.openai.com/v1/chat/completions",
      "Model": "gpt-4.1-nano",
      "CacheMinutes": 60,
      "MaxTokens": 500,
      "Temperature": 0.7
    }
  },
  "PerformanceMonitor": {
    "Enabled": true,
    "CheckIntervalSeconds": 30,
    "RecentWindowMinutes": 5,
    "AnomalyThresholdPercent": 50
  }
}
```

### Step 3: Configure Secrets for Development

Create `appsettings.Development.json` in `src/AggregatorService/` (git ignored):

```json
{
  "JwtSettings": {
    "SecretKey": "YourJwtSecretKey-MinLength32Characters!"
  },
  "ExternalApis": {
    "OpenWeatherMap": {
      "ApiKey": "YourOpenWeatherMapApiKey"
    },
    "NewsApi": {
      "ApiKey": "YourNewsApiKey"
    },
    "OpenAI": {
      "ApiKey": "YourOpenAIApiKey"
    }
  }
}
```

### Step 4: Configure Secrets for Tests

Create `appsettings.Secret.json` in `tests/AggregatorService.Tests/` (git ignored):

```json
{
  "ExternalApis": {
    "OpenWeatherMap": {
      "ApiKey": "YourOpenWeatherMapApiKey"
    },
    "NewsApi": {
      "ApiKey": "YourNewsApiKey"
    },
    "OpenAI": {
      "ApiKey": "YourOpenAIApiKey"
    }
  }
}
```

### Step 5: Obtain API Keys

| Provider | Registration URL | Required Plan |
|----------|------------------|---------------|
| OpenWeatherMap | https://openweathermap.org/api | Free tier available |
| NewsAPI | https://newsapi.org/ | Developer (free) |
| Open Library | https://openlibrary.org/developers/api | No API key required |
| OpenAI | https://platform.openai.com/ | API key required |

### Environment Variables (Production/CI-CD)

For production or CI/CD, use environment variables:

```bash
# Linux/Mac
export JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
export ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key"
export ExternalApis__NewsApi__ApiKey="your-news-api-key"
export ExternalApis__OpenAI__ApiKey="your-openai-api-key"

# Windows PowerShell
$env:JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
$env:ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key"
$env:ExternalApis__NewsApi__ApiKey="your-news-api-key"
$env:ExternalApis__OpenAI__ApiKey="your-openai-api-key"

# Docker
docker run \
  -e JwtSettings__SecretKey="your-jwt-key" \
  -e ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key" \
  -e ExternalApis__NewsApi__ApiKey="your-news-api-key" \
  -e ExternalApis__OpenAI__ApiKey="your-openai-api-key" \
  your-image
```

**GitHub Actions Example:**

```yaml
env:
  JwtSettings__SecretKey: ${{ secrets.JWT_SECRET_KEY }}
  ExternalApis__OpenWeatherMap__ApiKey: ${{ secrets.OPENWEATHERMAP_API_KEY }}
  ExternalApis__NewsApi__ApiKey: ${{ secrets.NEWSAPI_API_KEY }}
  ExternalApis__OpenAI__ApiKey: ${{ secrets.OPENAI_API_KEY }}
```
## API Endpoints

### Authentication

#### POST /api/authorise/login

Authenticates a user and returns a JWT token.

**Request:**
```json
{
  "username": "admin",
  "password": "admin123"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-01-15T12:00:00Z"
}
```

**Demo Users:**

| Username | Password |
|----------|----------|
| admin | admin123 |
| user | user123 |
| demo | demo123 |

---

### Aggregation

#### POST /api/aggregation

Aggregates data from multiple external API providers.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

**Request Fields:**

| Field | Type | Description |
|-------|------|-------------|
| sort | string | Sort option: `Newest`, `Oldest`, `Relevance`, `Popularity`. Default: `Relevance` |
| query | string | Search query for News and Books providers |
| country | string | ISO 3166 country code for Weather provider |
| language | string | ISO 639-1 language code for all providers |
| parameters | object | Additional provider-specific parameters |

**Provider Trigger Conditions:**

| Provider | Triggers When |
|----------|---------------|
| Weather | `city` parameter is present |
| News | `query` filter is present |
| Books | `query` filter OR `title`/`author` parameter is present |
| AIPrompt | Always (when OpenAI API key is configured) |

---

### Statistics

#### GET /api/statistics

Retrieves request statistics for all API providers.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Response (200 OK):**
```json
{
  "timestamp": "2025-01-15T10:35:00Z",
  "providers": [
    {
      "providerName": "Weather",
      "totalRequests": 150,
      "successfulRequests": 145,
      "failedRequests": 5,
      "averageResponseTimeMs": 125.50,
      "performanceBuckets": {
        "fast": 80,
        "average": 55,
        "slow": 15
      }
    }
  ]
}
```

**Performance Buckets:**

| Bucket | Response Time |
|--------|---------------|
| Fast | < 100ms |
| Average | 100-200ms |
| Slow | > 200ms |

---

#### GET /api/statistics/performance

Retrieves current performance status and anomaly detection results.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Response (200 OK):**
```json
{
  "timestamp": "2025-01-15T10:35:00Z",
  "recentWindowMinutes": 5,
  "anomalyThresholdPercent": 50,
  "providers": [
    {
      "providerName": "Weather",
      "overallAverageMs": 280.50,
      "overallRequestCount": 156,
      "recentAverageMs": 450.25,
      "recentRequestCount": 12,
      "degradationPercent": 60.5,
      "isAnomaly": true,
      "status": "Anomaly"
    }
  ]
}
```

**Status Values:**

| Status | Description |
|--------|-------------|
| Normal | Performance within threshold |
| Anomaly | Recent average exceeds threshold |
| Insufficient Data | Not enough requests to analyze |
| No Recent Data | No requests in recent time window |

---

#### DELETE /api/statistics

Resets all statistics data.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Response:** `204 No Content`

---

## Fallback Mechanism

The aggregation service implements a robust fallback mechanism to ensure maximum reliability.

### How It Works

1. **Parallel Execution**: All matching providers are called simultaneously using `Task.WhenAll()`, minimizing total response time.

2. **Isolated Failures**: Each provider runs independently - if one provider fails, others continue unaffected.

3. **Graceful Error Handling**: Failed providers return an error response with `IsSuccess = false` and an `ErrorMessage`, rather than throwing exceptions.

4. **Partial Results**: The aggregation response always includes results from all queried providers, whether successful or failed.

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     Aggregation Request                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                   AggregationService                            │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Task.WhenAll (Parallel)                     │   │
│  │                                                          │   │
│  │   ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌──────────┐   │   │
│  │   │ Weather │  │  News   │  │  Books  │  │ AIPrompt │   │   │
│  │   │Provider │  │Provider │  │Provider │  │ Provider │   │   │
│  │   └────┬────┘  └────┬────┘  └────┬────┘  └────┬─────┘   │   │
│  │        │            │            │            │          │   │
│  │        ▼            ▼            ▼            ▼          │   │
│  │   ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌──────────┐   │   │
│  │   │ Success │  │  Fail   │  │ Success │  │ Success  │   │   │
│  │   │  Data   │  │  Error  │  │  Data   │  │   Data   │   │   │
│  │   └─────────┘  └─────────┘  └─────────┘  └──────────┘   │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Aggregation Response                           │
│                                                                 │
│   providersQueried: 4                                           │
│   successfulResponses: 3                                        │
│   results: [Weather✓, News✗, Books✓, AIPrompt✓]                │
└─────────────────────────────────────────────────────────────────┘
```

### Example: Partial Failure Response

When the News API is unavailable but other providers succeed:

```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.450",
  "providersQueried": 4,
  "successfulResponses": 3,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": {
        "main": { "temp": 12.5 },
        "weather": [{ "description": "clear sky" }],
        "name": "London"
      },
      "responseTime": "00:00:00.320"
    },
    {
      "provider": "News",
      "isSuccess": false,
      "data": null,
      "errorMessage": "Response status code does not indicate success: 503 (Service Unavailable).",
      "responseTime": "00:00:00.150"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 89,
        "docs": [...]
      },
      "responseTime": "00:00:00.280"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "Analyze the weather and book data...",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.420"
    }
  ]
}
```

### Benefits

| Benefit | Description |
|---------|-------------|
| **No Single Point of Failure** | One slow or failed API doesn't block the entire response |
| **Transparency** | Clients can see exactly which providers succeeded or failed |
| **Graceful Degradation** | Applications can still function with partial data |
| **Performance** | Parallel execution means total time ≈ slowest provider, not sum of all |
| **Error Details** | Each failed provider includes specific error message for debugging |

## Running the Application

### Development

```bash
# From solution root
dotnet restore
dotnet run --project src/AggregatorService
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

### Production

```bash
dotnet publish src/AggregatorService -c Release -o ./publish
cd publish
dotnet AggregatorService.dll
```

---

## Running Tests

### Step 1: Configure Test Secrets

Create `appsettings.Secret.json` in `tests/AggregatorService.Tests/`:

```json
{
  "ExternalApis": {
    "OpenWeatherMap": {
      "ApiKey": "YourApiKey"
    },
    "NewsApi": {
      "ApiKey": "YourApiKey"
    },
    "OpenAI": {
      "ApiKey": "YourApiKey"
    }
  }
}
```

### Step 2: Run Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test tests/AggregatorService.Tests
```

### Test Categories

| Category | Description |
|----------|-------------|
| **Unit Tests** | Test individual components with mocked dependencies |
| **Integration Tests** | Test providers against real APIs (require API keys) |

> **Note**: Integration tests require valid API keys. Tests handle API rate limits gracefully.

---

## External API Providers

### Weather Provider (OpenWeatherMap)

**Endpoints Used:**
- Geocoding: `http://api.openweathermap.org/geo/1.0/direct`
- Weather: `https://api.openweathermap.org/data/2.5/weather`

**Triggers When:** `city` parameter is present

**Caching:**
- Geocoding results: 30 days
- Weather data: 4 hours

**Supported Parameters:**

| Parameter | Description | Example |
|-----------|-------------|---------|
| city | City name (required) | `London` |
| country | ISO 3166 country code | `GB` |
| state | State code (US only) | `NY` |
| units | Temperature units | `metric`, `imperial` |
| lang | Language for descriptions | `en`, `de`, `fr` |

**Sorting:** Not supported (returns single location result)

---

### News Provider (NewsAPI)

**Endpoint:** `https://newsapi.org/v2/everything`

**Triggers When:** `query` filter is present

**Caching:** 4 hours

**Supported Parameters:**

| Parameter | Description | Example |
|-----------|-------------|---------|
| query | Search keywords (required) | `artificial intelligence` |
| language | ISO 639-1 language code | `en` |
| from | Start date | `2025-01-01` |
| to | End date | `2025-01-15` |
| searchIn | Where to search | `title`, `description`, `content` |

**Sorting:**

| Sort Option | Maps To | Description |
|-------------|---------|-------------|
| Newest | `sortBy=publishedAt` | Most recent first |
| Oldest | `sortBy=publishedAt` | Oldest first |
| Relevance | `sortBy=relevancy` | Most relevant to query |
| Popularity | `sortBy=popularity` | Most popular sources first |

---

### Books Provider (Open Library)

**Endpoint:** `https://openlibrary.org/search.json`

**Triggers When:** `query` filter OR `title`/`author` parameter is present

**Caching:** 30 minutes

**Supported Parameters:**

| Parameter | Description | Example |
|-----------|-------------|---------|
| query | General search | `science fiction` |
| title | Search by title | `Foundation` |
| author | Search by author | `Isaac Asimov` |
| language | ISO 639-1 language code | `eng` |

**Sorting:**

| Sort Option | Maps To | Description |
|-------------|---------|-------------|
| Newest | `sort=new` | Newest publications first |
| Oldest | `sort=old` | Oldest publications first |
| Relevance | (default) | Most relevant to search |
| Popularity | `sort=rating` | Highest rated first |

---

### AIPrompt Provider (OpenAI)

**Endpoint:** `https://api.openai.com/v1/chat/completions`

**Triggers When:** Always runs (when API key is configured)

**Purpose:** Generates a tailored analysis prompt based on the search context. Users can use this prompt with any AI tool to analyze their aggregated data.

**Caching:** 60 minutes

**Configuration Options:**

| Setting | Description | Default |
|---------|-------------|---------|
| Model | OpenAI model to use | Configured in appsettings |
| MaxTokens | Maximum response tokens | 500 |
| Temperature | Creativity level (0-1) | 0.7 |

**How It Works:**

1. Collects all request parameters (query, language, country, sort, etc.)
2. Sends context to OpenAI with a system prompt
3. OpenAI generates a custom analysis prompt
4. Returns the prompt for user to use with aggregated data

**Response Format:**
```json
{
  "provider": "AIPrompt",
  "isSuccess": true,
  "data": {
    "prompt": "Based on your search for 'climate change' with weather data from London and news articles from January 2025, analyze: 1) Current weather patterns... 2) Key news themes... 3) Correlations between...",
    "model": "gpt-4.1-nano"
  },
  "responseTime": "00:00:00.520"
}
```

---

## Performance Monitoring

The service includes a background performance monitor that automatically detects anomalies.

### Configuration

```json
{
  "PerformanceMonitor": {
    "Enabled": true,
    "CheckIntervalSeconds": 30,
    "RecentWindowMinutes": 5,
    "AnomalyThresholdPercent": 50
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| Enabled | Enable/disable monitoring | true |
| CheckIntervalSeconds | How often to check | 30 |
| RecentWindowMinutes | Time window for recent stats | 5 |
| AnomalyThresholdPercent | Degradation threshold | 50 |

### Anomaly Detection

The monitor compares the **recent average response time** (last 5 minutes) against the **overall average**. If the recent average exceeds the threshold, it logs a warning:

```
warn: PerformanceMonitorService[0]
      PERFORMANCE ANOMALY DETECTED - Provider: Weather | 
      Recent avg: 450.25ms (12 requests) | 
      Overall avg: 280.50ms (156 requests) | 
      Degradation: 60.5% (threshold: 50%)
```

---

## Error Handling

All errors return a consistent JSON format:

```json
{
  "statusCode": 400,
  "message": "Error description"
}
```

| Status Code | Description |
|-------------|-------------|
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Missing or invalid JWT token |
| 500 | Internal Server Error |

## Example Usage

All examples assume you have obtained a JWT token. Set it as an environment variable:

```bash
# Get token and store it
TOKEN=$(curl -s -X POST http://localhost:5000/api/authorise/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}' | jq -r '.token')

# Verify token is set
echo $TOKEN
```

---

### Example 1: Weather Only

Get weather data for a specific city.

**curl:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "city": "London",
      "units": "metric"
    }
  }'
```

**JSON Request:**
```json
{
  "parameters": {
    "city": "London",
    "units": "metric"
  }
}
```

**Response:**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.520",
  "providersQueried": 2,
  "successfulResponses": 2,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": {
        "coord": { "lon": -0.1257, "lat": 51.5085 },
        "weather": [
          { "id": 804, "main": "Clouds", "description": "overcast clouds" }
        ],
        "main": {
          "temp": 12.5,
          "feels_like": 11.2,
          "humidity": 81
        },
        "wind": { "speed": 4.12 },
        "name": "London"
      },
      "responseTime": "00:00:00.320"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have received current weather data for London. Analyze the conditions and consider: 1) How does the temperature compare to seasonal averages? 2) What activities would be suitable for these conditions? 3) Any recommendations for travelers visiting London today?",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.480"
    }
  ]
}
```

---

### Example 2: News Only

Search for news articles on a specific topic.

**curl:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "artificial intelligence",
    "language": "en",
    "sort": "Newest"
  }'
```

**JSON Request:**
```json
{
  "query": "artificial intelligence",
  "language": "en",
  "sort": "Newest"
}
```

**Response:**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.780",
  "providersQueried": 3,
  "successfulResponses": 3,
  "results": [
    {
      "provider": "News",
      "isSuccess": true,
      "data": {
        "status": "ok",
        "totalResults": 12847,
        "articles": [
          {
            "source": { "id": "techcrunch", "name": "TechCrunch" },
            "title": "New AI Model Breaks Performance Records",
            "description": "Researchers announce breakthrough in...",
            "url": "https://techcrunch.com/2025/01/15/ai-breakthrough",
            "publishedAt": "2025-01-15T09:30:00Z"
          },
          {
            "source": { "id": "wired", "name": "Wired" },
            "title": "The Future of AI in Healthcare",
            "description": "How artificial intelligence is transforming...",
            "url": "https://wired.com/2025/01/15/ai-healthcare",
            "publishedAt": "2025-01-15T08:00:00Z"
          }
        ]
      },
      "responseTime": "00:00:00.450"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 5234,
        "docs": [
          {
            "title": "Artificial Intelligence: A Modern Approach",
            "author_name": ["Stuart Russell", "Peter Norvig"],
            "first_publish_year": 1995
          },
          {
            "title": "Deep Learning",
            "author_name": ["Ian Goodfellow", "Yoshua Bengio"],
            "first_publish_year": 2016
          }
        ]
      },
      "responseTime": "00:00:00.380"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have news articles and books about 'artificial intelligence' sorted by newest. Analyze: 1) What are the dominant themes in recent AI news? 2) Which foundational books are most relevant to current developments? 3) Identify emerging trends or concerns in the field.",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.520"
    }
  ]
}
```

---

### Example 3: Books Only

Search for books by a specific author.

**curl:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "parameters": {
      "author": "Isaac Asimov"
    },
    "sort": "Popularity"
  }'
```

**JSON Request:**
```json
{
  "parameters": {
    "author": "Isaac Asimov"
  },
  "sort": "Popularity"
}
```

**Response:**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.450",
  "providersQueried": 2,
  "successfulResponses": 2,
  "results": [
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 1247,
        "docs": [
          {
            "title": "Foundation",
            "author_name": ["Isaac Asimov"],
            "first_publish_year": 1951,
            "number_of_pages_median": 255
          },
          {
            "title": "I, Robot",
            "author_name": ["Isaac Asimov"],
            "first_publish_year": 1950,
            "number_of_pages_median": 224
          },
          {
            "title": "The Caves of Steel",
            "author_name": ["Isaac Asimov"],
            "first_publish_year": 1954,
            "number_of_pages_median": 206
          }
        ]
      },
      "responseTime": "00:00:00.280"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have a collection of books by Isaac Asimov sorted by popularity. Consider: 1) What are the major themes across his works? 2) Which books are recommended for someone new to his writing? 3) How do his robot stories compare to his Foundation series?",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.410"
    }
  ]
}
```

---

### Example 4: Weather + News Combined

Get weather for a city and news about events in that location.

**curl:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Tokyo Olympics",
    "parameters": {
      "city": "Tokyo"
    },
    "language": "en"
  }'
```

**JSON Request:**
```json
{
  "query": "Tokyo Olympics",
  "parameters": {
    "city": "Tokyo"
  },
  "language": "en"
}
```

**Response:**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.820",
  "providersQueried": 4,
  "successfulResponses": 4,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": {
        "main": { "temp": 8.2, "humidity": 45 },
        "weather": [{ "description": "clear sky" }],
        "name": "Tokyo"
      },
      "responseTime": "00:00:00.350"
    },
    {
      "provider": "News",
      "isSuccess": true,
      "data": {
        "status": "ok",
        "totalResults": 3421,
        "articles": [...]
      },
      "responseTime": "00:00:00.520"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 234,
        "docs": [...]
      },
      "responseTime": "00:00:00.310"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have weather data for Tokyo and information about 'Tokyo Olympics' from news and books. Analyze: 1) How does Tokyo's current weather compare to typical Olympic event conditions? 2) What are the key news stories about the Olympics? 3) What historical context do the books provide?",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.580"
    }
  ]
}
```

---

### Example 5: All Providers with Full Parameters

Comprehensive request with all filters and sorting.

**curl:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sort": "Newest",
    "query": "climate change",
    "country": "GB",
    "language": "en",
    "parameters": {
      "city": "London",
      "units": "metric",
      "from": "2025-01-01",
      "to": "2025-01-15"
    }
  }'
```

**JSON Request:**
```json
{
  "sort": "Newest",
  "query": "climate change",
  "country": "GB",
  "language": "en",
  "parameters": {
    "city": "London",
    "units": "metric",
    "from": "2025-01-01",
    "to": "2025-01-15"
  }
}
```

**Response:**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.920",
  "providersQueried": 4,
  "successfulResponses": 4,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": {
        "main": { "temp": 11.3, "humidity": 78 },
        "weather": [{ "description": "light rain" }],
        "name": "London"
      },
      "responseTime": "00:00:00.340"
    },
    {
      "provider": "News",
      "isSuccess": true,
      "data": {
        "status": "ok",
        "totalResults": 8934,
        "articles": [
          {
            "title": "UK Announces New Climate Targets",
            "publishedAt": "2025-01-14T18:00:00Z"
          },
          {
            "title": "London's Green Initiative Expands",
            "publishedAt": "2025-01-13T12:30:00Z"
          }
        ]
      },
      "responseTime": "00:00:00.580"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 4521,
        "docs": [
          {
            "title": "The Uninhabitable Earth",
            "author_name": ["David Wallace-Wells"],
            "first_publish_year": 2019
          }
        ]
      },
      "responseTime": "00:00:00.390"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have comprehensive data about 'climate change' including: London weather (metric), UK news from Jan 1-15 2025, and related books. Analyze: 1) How does current London weather reflect climate patterns? 2) What policy developments appear in recent UK news? 3) Which books provide scientific vs policy perspectives? 4) Identify correlations between current events and scientific literature.",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.650"
    }
  ]
}
```

---

### Example 6: Different Sorting Options

**Newest First:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "technology", "sort": "Newest"}'
```

**Most Popular:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "technology", "sort": "Popularity"}'
```

**Most Relevant (default):**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "technology", "sort": "Relevance"}'
```

**Oldest First:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "technology", "sort": "Oldest"}'
```

**Sort Option Mapping:**

| Sort | News API | Open Library |
|------|----------|--------------|
| Newest | `publishedAt` | `new` |
| Oldest | `publishedAt` | `old` |
| Relevance | `relevancy` | (default) |
| Popularity | `popularity` | `rating` |

---

### Example 7: Partial Failure Response

When one provider fails, others continue normally.

**Request:**
```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "space exploration",
    "parameters": {
      "city": "Houston"
    }
  }'
```

**Response (News API unavailable):**
```json
{
  "timestamp": "2025-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.650",
  "providersQueried": 4,
  "successfulResponses": 3,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": {
        "main": { "temp": 22.5, "humidity": 65 },
        "weather": [{ "description": "partly cloudy" }],
        "name": "Houston"
      },
      "responseTime": "00:00:00.310"
    },
    {
      "provider": "News",
      "isSuccess": false,
      "data": null,
      "errorMessage": "Response status code does not indicate success: 503 (Service Unavailable).",
      "responseTime": "00:00:00.180"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": {
        "numFound": 8923,
        "docs": [
          {
            "title": "The Right Stuff",
            "author_name": ["Tom Wolfe"]
          }
        ]
      },
      "responseTime": "00:00:00.420"
    },
    {
      "provider": "AIPrompt",
      "isSuccess": true,
      "data": {
        "prompt": "You have weather data for Houston and books about 'space exploration'. Note: News data was temporarily unavailable. Analyze the available data, considering Houston's significance as home to NASA's Johnson Space Center.",
        "model": "gpt-4.1-nano"
      },
      "responseTime": "00:00:00.510"
    }
  ]
}
```

**Key Observations:**
- `providersQueried: 4` - All providers were attempted
- `successfulResponses: 3` - Three succeeded
- News has `isSuccess: false` with detailed error
- Other providers returned data normally
- AIPrompt acknowledges the missing data

---

### Example 8: Using the AIPrompt

The AIPrompt provider generates a tailored prompt you can use with any AI tool.

**Step 1: Make Aggregation Request**

```bash
curl -X POST http://localhost:5000/api/aggregation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "renewable energy",
    "parameters": {
      "city": "Berlin"
    },
    "sort": "Newest"
  }'
```

**Step 2: Extract the Generated Prompt**

From the response, locate the AIPrompt result:

```json
{
  "provider": "AIPrompt",
  "isSuccess": true,
  "data": {
    "prompt": "You have current weather data for Berlin and recent information about 'renewable energy' from news articles and books. Analyze this data to: 1) Assess how Berlin's weather conditions affect solar and wind energy potential today. 2) Identify the key themes and developments in recent renewable energy news. 3) Compare perspectives from recent publications vs established books. 4) Provide actionable insights for someone interested in renewable energy in Germany.",
    "model": "gpt-4.1-nano"
  }
}
```

**Step 3: Use with Your Preferred AI Tool**

Copy the generated prompt and the data from Weather, News, and Books results. Paste them into ChatGPT, Claude, or any AI assistant or simply feed into your agent api with the data:

```
[Paste the generated prompt]

Here is the data:

WEATHER DATA:
[Paste Weather provider data]

NEWS DATA:
[Paste News provider data]

BOOKS DATA:
[Paste Books provider data]
```

**Step 4: Get Comprehensive Analysis**

The AI tool will provide a structured analysis based on all your aggregated data, following the guidance in the generated prompt.