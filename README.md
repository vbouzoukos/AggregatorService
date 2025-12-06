# API Aggregation Service

A .NET 8 API aggregation service that consolidates data from multiple external APIs and provides a unified endpoint to access the aggregated information.

## Table of Contents

1. [Features](#features)
2. [Prerequisites](#prerequisites)
3. [Project Structure](#project-structure)
4. [Configuration](#configuration)
5. [API Endpoints](#api-endpoints)
6. [Running the Application](#running-the-application)
7. [Running Tests](#running-tests)
8. [External API Providers](#external-api-providers)

---

## Features

- Aggregates data from multiple external APIs simultaneously (Weather, News, Books)
- Unified filtering and sorting across providers
- JWT Bearer authentication
- Distributed caching (in-memory, Redis-ready)
- Request statistics with performance buckets
- **Performance anomaly detection** with background monitoring
- Global error handling
- Parallel API calls for optimal performance
- Thread-safe statistics tracking
- Configuration-driven provider setup

---

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code
- API keys for external services (OpenWeatherMap, NewsAPI)

---

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
│       │   │   ├── AuthenticationRequest.cs
│       │   │   └── SortOption.cs
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
│       │   ├── Authorise/
│       │   ├── Caching/
│       │   ├── Monitoring/
│       │   │   └── PerformanceMonitorService.cs
│       │   ├── Providers/
│       │   │   ├── WeatherProvider.cs
│       │   │   ├── NewsProvider.cs
│       │   │   └── OpenLibraryProvider.cs
│       │   └── Statistics/
│       ├── appsettings.json
│       ├── appsettings.Development.json (git ignored)
│       ├── Program.cs
│       └── AggregatorService.csproj
└── tests/
    └── AggregatorService.Tests/
        ├── Controllers/
        │   └── StatisticsControllerTests.cs
        ├── Services/
        │   └── Providers/
        │       ├── WeatherProviderTests.cs
        │       ├── NewsProviderTests.cs
        │       └── OpenLibraryProviderTests.cs
        ├── appsettings.json
        ├── appsettings.Secrets.json (git ignored)
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

The application uses multiple configuration files:

**appsettings.json** (tracked in git - no secrets)

```json
{
  "PerformanceMonitor": {
    "Enabled": true,
    "CheckIntervalSeconds": 30,
    "RecentWindowMinutes": 5,
    "AnomalyThresholdPercent": 50
  },
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
        "Language": "lang"
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
    }
  }
}
```

### Step 3: Configure Secrets for Development

Create `appsettings.Development.json` in `src/AggregatorService/` (this file is git ignored):

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
    }
  }
}
```

### Step 4: Configure Secrets for Tests

Create `appsettings.Secrets.json` in `tests/AggregatorService.Tests/` (this file is git ignored):

```json
{
  "ExternalApis": {
    "OpenWeatherMap": {
      "ApiKey": "YourOpenWeatherMapApiKey"
    },
    "NewsApi": {
      "ApiKey": "YourNewsApiKey"
    }
  }
}
```

### Step 5: Obtain API Keys

| Provider | Registration URL | Required Plan |
|----------|------------------|---------------|
| OpenWeatherMap | https://openweathermap.org/api | Free tier or One Call API 3.0 |
| NewsAPI | https://newsapi.org/ | Developer (free) |
| Open Library | https://openlibrary.org/developers/api | No API key required |

### Environment Variables (Production/CI-CD)

For production or CI/CD, use environment variables instead of secret files:

```bash
# Linux/Mac
export JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
export ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key"
export ExternalApis__NewsApi__ApiKey="your-news-api-key"

# Windows PowerShell
$env:JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
$env:ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key"
$env:ExternalApis__NewsApi__ApiKey="your-news-api-key"

# Docker
docker run \
  -e JwtSettings__SecretKey="your-jwt-key" \
  -e ExternalApis__OpenWeatherMap__ApiKey="your-weather-api-key" \
  -e ExternalApis__NewsApi__ApiKey="your-news-api-key" \
  your-image
```

**GitHub Actions Example:**

```yaml
env:
  JwtSettings__SecretKey: ${{ secrets.JWT_SECRET_KEY }}
  ExternalApis__OpenWeatherMap__ApiKey: ${{ secrets.OPENWEATHERMAP_API_KEY }}
  ExternalApis__NewsApi__ApiKey: ${{ secrets.NEWSAPI_API_KEY }}
```

---

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
  "expiresAt": "2024-01-15T12:00:00Z"
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

#### POST /api/aggregate

Aggregates data from multiple external API providers.

**Headers:**

```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

**Request:**

```json
{
  "sort": "newest",
  "query": "technology",
  "country": "GB",
  "language": "en",
  "parameters": {
    "city": "London",
    "from": "2025-01-01",
    "to": "2025-01-15"
  }
}
```

**Request Fields:**

| Field | Type | Description |
|-------|------|-------------|
| sort | string | Sort option: `newest`, `oldest`, `relevance`, `popularity`. Default: `relevance` |
| query | string | Search query for News and Books providers |
| country | string | ISO 3166 country code for Weather provider |
| language | string | ISO 639-1 language code for all providers |
| parameters | object | Additional provider-specific parameters |

**Response (200 OK):**

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.850",
  "providersQueried": 3,
  "successfulResponses": 3,
  "results": [
    {
      "provider": "Weather",
      "isSuccess": true,
      "data": { "temp": 12.5, "description": "overcast clouds" },
      "responseTime": "00:00:00.320"
    },
    {
      "provider": "News",
      "isSuccess": true,
      "data": { "totalResults": 150, "articles": [...] },
      "responseTime": "00:00:00.450"
    },
    {
      "provider": "Books",
      "isSuccess": true,
      "data": { "numFound": 89, "docs": [...] },
      "responseTime": "00:00:00.280"
    }
  ]
}
```

**Provider Requirements:**

| Provider | Required Parameters | Filters Used |
|----------|---------------------|--------------|
| Weather | `city` in parameters | `country`, `language` → `lang` |
| News | `query` filter | `query` → `q`, `language` |
| Books | `query` filter OR `title`/`author` in parameters | `query` → `q`, `language` |

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
  "timestamp": "2024-01-15T10:35:00Z",
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
  "timestamp": "2024-01-15T10:35:00Z",
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
    },
    {
      "providerName": "News",
      "overallAverageMs": 320.00,
      "overallRequestCount": 89,
      "recentAverageMs": 310.50,
      "recentRequestCount": 8,
      "degradationPercent": -3.0,
      "isAnomaly": false,
      "status": "Normal"
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

**Response:** `204 No Content`

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

The monitor compares the **recent average response time** (last 5 minutes) against the **overall average**. If the recent average is more than 50% higher, it logs a warning:

```
warn: PerformanceMonitorService[0]
      PERFORMANCE ANOMALY DETECTED - Provider: Weather | 
      Recent avg: 450.25ms (12 requests) | 
      Overall avg: 280.50ms (156 requests) | 
      Degradation: 60.5% (threshold: 50%)
```

---

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

Create `appsettings.Secrets.json` in `tests/AggregatorService.Tests/`:

```json
{
  "ExternalApis": {
    "OpenWeatherMap": {
      "ApiKey": "YourApiKey"
    },
    "NewsApi": {
      "ApiKey": "YourApiKey"
    }
  }
}
```

### Step 2: Run Tests

```bash
# From solution root
dotnet test
```

> **Important**: Some integration tests require valid API keys. Tests are designed to handle API rate limits gracefully.

---

## External API Providers

### Weather Provider (OpenWeatherMap)

**Endpoints Used:**
- Geocoding: `http://api.openweathermap.org/geo/1.0/direct`
- Weather: `https://api.openweathermap.org/data/2.5/weather`

**Caching:**
- Geocoding results: 30 days
- Weather data: 4 hours

**Sorting:** Not supported (single result per location)

---

### News Provider (NewsAPI)

**Endpoint:** `https://newsapi.org/v2/everything`

**Caching:** 4 hours

**Sorting:**

| Sort Option | Maps To |
|-------------|---------|
| newest | `sortBy=publishedAt` |
| oldest | `sortBy=publishedAt` |
| relevance | `sortBy=relevancy` |
| popularity | `sortBy=popularity` |

---

### Books Provider (Open Library)

**Endpoint:** `https://openlibrary.org/search.json`

**Caching:** 30 minutes

**Sorting:**

| Sort Option | Maps To |
|-------------|---------|
| newest | `sort=new` |
| oldest | `sort=old` |
| relevance | (default) |
| popularity | `sort=rating` |

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
| 401 | Unauthorized - Missing or invalid token |
| 500 | Internal Server Error |

---

## Example Usage

### Complete Aggregation Request

```bash
# Get JWT token
TOKEN=$(curl -s -X POST http://localhost:5000/api/authorise/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "admin123"}' | jq -r '.token')

# Aggregate data from all providers
curl -X POST http://localhost:5000/api/aggregate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sort": "newest",
    "query": "artificial intelligence",
    "language": "en",
    "parameters": {
      "city": "London",
      "from": "2025-01-01"
    }
  }'

# Check performance status
curl http://localhost:5000/api/statistics/performance \
  -H "Authorization: Bearer $TOKEN"
```