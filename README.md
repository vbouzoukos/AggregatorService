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

- Aggregates data from multiple external APIs simultaneously
- JWT Bearer authentication
- Distributed caching (in-memory, Redis-ready)
- Request statistics with performance buckets
- Global error handling
- Parallel API calls for optimal performance
- Thread-safe statistics tracking

---

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code
- API keys for external services (OpenWeatherMap)

---

## Project Structure

```
AggregatorService.sln
├── src/
│   └── AggregatorService/
│       ├── Controllers/
│       │   └── AuthoriseController.cs
│       ├── Middleware/
│       │   └── GlobalErrorHandlerMiddleware.cs
│       ├── Models/
│       │   ├── Requests/
│       │   │   ├── AuthenticationRequest.cs
│       │   │   └── AggregationRequest.cs
│       │   └── Responses/
│       │       ├── AuthenticationResponse.cs
│       │       ├── ApiResponse.cs
│       │       ├── AggregationResponse.cs
│       │       ├── StatisticsResponse.cs
│       │       ├── ProviderStatistics.cs
│       │       └── PerformanceBuckets.cs
│       ├── Services/
│       │   ├── Aggregation/
│       │   ├── Authorise/
│       │   ├── Caching/
│       │   ├── Providers/
│       │   └── Statistics/
│       ├── appsettings.json
│       ├── appsettings.Development.json (git ignored)
│       ├── Program.cs
│       └── AggregatorService.csproj
└── tests/
    └── AggregatorService.Tests/
        ├── Services/
        │   └── Providers/
        │       └── WeatherProviderTests.cs
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
  "JwtSettings": {
    "Issuer": "AggregatorService",
    "Audience": "AggregatorService-Clients",
    "ExpirationInMinutes": 60
  },
  "ExternalApis": {
    "OpenWeatherMap": {
      "GeocodingUrl": "https://api.openweathermap.org/geo/1.0/direct",
      "WeatherUrl": "https://api.openweathermap.org/data/3.0/onecall"
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
      "ApiKey": "YourApiKey"
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
      "ApiKey": "YourApiKey"
    }
  }
}
```

### Step 5: Obtain API Keys

| Provider | Registration URL | Required Plan |
|----------|------------------|---------------|
| OpenWeatherMap | https://openweathermap.org/api | One Call API 3.0 |

### Environment Variables (Production/CI-CD)

For production or CI/CD, use environment variables instead of secret files:

```bash
# Linux/Mac
export JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
export ExternalApis__OpenWeatherMap__ApiKey="your-api-key"

# Windows PowerShell
$env:JwtSettings__SecretKey="YourJwtSecretKey-MinLength32Characters!"
$env:ExternalApis__OpenWeatherMap__ApiKey="your-api-key"

# Docker
docker run \
  -e JwtSettings__SecretKey="your-jwt-key" \
  -e ExternalApis__OpenWeatherMap__ApiKey="your-api-key" \
  your-image
```

**GitHub Actions Example:**

```yaml
env:
  JwtSettings__SecretKey: ${{ secrets.JWT_SECRET_KEY }}
  ExternalApis__OpenWeatherMap__ApiKey: ${{ secrets.OPENWEATHERMAP_API_KEY }}
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

**Error Responses:**

| Status Code | Description |
|-------------|-------------|
| 400 | Invalid request parameters |
| 401 | Invalid credentials |

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
  "parameters": {
    "city": "London",
    "country": "GB",
    "units": "metric"
  }
}
```

**Response (200 OK):**

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "totalResponseTime": "00:00:00.450",
  "providersQueried": 1,
  "successfulResponses": 1,
  "results": [
    {
      "provider": "weather",
      "isSuccess": true,
      "data": {
        "lat": 51.5074,
        "lon": -0.1278,
        "current": {
          "temp": 12.5,
          "weather": [
            {
              "main": "Clouds",
              "description": "overcast clouds"
            }
          ]
        }
      },
      "errorMessage": null,
      "responseTime": "00:00:00.320"
    }
  ]
}
```

**Supported Parameters by Provider:**

| Provider | Parameter | Description | Required |
|----------|-----------|-------------|----------|
| weather | city | City name | Yes* |
| weather | state | State code (US only) | No |
| weather | country | ISO 3166 country code | No |
| weather | lat | Latitude | Yes* |
| weather | lon | Longitude | Yes* |
| weather | units | standard, metric, imperial | No |
| weather | lang | Language code | No |
| weather | exclude | Comma-separated: current, minutely, hourly, daily, alerts | No |

*Either `city` OR (`lat` and `lon`) is required.

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
      "providerName": "weather",
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
    }
  }
}
```

### Step 2: Run Tests

```bash
# From solution root
dotnet test
```

> **Important**: Tests will fail if `appsettings.Secrets.json` is missing or API key is not configured. This is intentional to prevent false positives in CI/CD.

---

## External API Providers

### Weather Provider (OpenWeatherMap)

Uses OpenWeatherMap One Call API 3.0.

**Endpoints Used:**
- Geocoding: `https://api.openweathermap.org/geo/1.0/direct`
- Weather: `https://api.openweathermap.org/data/3.0/onecall`

**Caching:**
- Geocoding results: 30 days
- Weather data: 10 minutes

**Example Request:**

```bash
curl -X POST https://localhost:5001/api/aggregate \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"parameters": {"city": "London", "units": "metric"}}'
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
| 401 | Unauthorized - Missing or invalid token |
| 500 | Internal Server Error |

