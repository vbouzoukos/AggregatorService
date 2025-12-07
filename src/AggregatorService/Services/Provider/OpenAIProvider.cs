using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Statistics;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Services.Provider
{
    /// <summary>
    /// Provider for OpenAI API - Generates analysis prompts based on search context
    /// </summary>
    public class OpenAIProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        IStatisticsService statisticsService,
        ILogger<OpenAIProvider> logger) : IExternalApiProvider
    {
        private const string CacheKeyPrefix = "openai:";
        private const string ConfigKey = "ExternalApis:OpenAI";

        private const string SystemPrompt = """
            You are a data analysis assistant. Based on the user's search context and parameters, 
            generate a helpful prompt they can use to analyze the aggregated data they will receive.
            The prompt should guide them to find insights, trends, and correlations in the data.
            Keep the prompt concise but comprehensive.
            Return only the prompt text, no additional explanation.
            """;

        public string Name => "AIPrompt";

        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue($"{ConfigKey}:CacheMinutes", 60));

        /// <summary>
        /// OpenAI provider triggers on any request if configured
        /// </summary>
        public bool CanHandle(AggregationRequest request)
        {
            var apiKey = configuration[$"{ConfigKey}:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogDebug("OpenAI provider skipped - API key not configured");
                return false;
            }

            return true;
        }

        public async Task<ApiResponse> FetchAsync(AggregationRequest request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var userMessage = BuildUserMessage(request);
                var cacheKey = BuildCacheKey(request);

                // Check cache first
                var cached = await cacheService.GetAsync<JsonElement>(cacheKey, cancellationToken);
                if (cached.ValueKind != JsonValueKind.Undefined)
                {
                    logger.LogDebug("OpenAI cache hit for request context");
                    stopwatch.Stop();
                    statisticsService.RecordRequest(Name, stopwatch.Elapsed, true);

                    return new ApiResponse
                    {
                        Provider = Name,
                        IsSuccess = true,
                        Data = cached,
                        ResponseTime = stopwatch.Elapsed
                    };
                }

                // Call OpenAI API
                var generatedPrompt = await CallOpenAIAsync(userMessage, cancellationToken);

                var responseData = new
                {
                    Prompt = generatedPrompt,
                    Model = configuration[$"{ConfigKey}:Model"]!
                };

                // Cache the result as JsonElement
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(responseData));
                await cacheService.SetAsync(cacheKey, jsonElement, _cacheExpiration, cancellationToken);

                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, true);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = true,
                    Data = responseData,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Respect cancellation - rethrow to let caller handle
                throw;
            }
            catch (HttpRequestException ex)
            {
                var statusInfo = ex.StatusCode.HasValue ? $"{(int)ex.StatusCode} {ex.StatusCode}" : "Unknown";
                logger.LogWarning(ex, "HTTP error fetching {Name} data. Code: {StatusInfo}", Name, statusInfo);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, false);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = false,
                    ErrorMessage = $"HTTP error ({statusInfo}): {ex.Message}",
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching from {Name}", Name);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, false);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ResponseTime = stopwatch.Elapsed
                };
            }
        }

        private static string BuildUserMessage(AggregationRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Search Context:");

            if (!string.IsNullOrEmpty(request.Query))
                sb.AppendLine($"- Query: {request.Query}");

            if (!string.IsNullOrEmpty(request.Language))
                sb.AppendLine($"- Language: {request.Language}");

            if (!string.IsNullOrEmpty(request.Country))
                sb.AppendLine($"- Country: {request.Country}");

            if (request.Sort != SortOption.Relevance)
                sb.AppendLine($"- Sort: {request.Sort}");

            if (request.Parameters.Count > 0)
            {
                sb.AppendLine("- Parameters:");
                foreach (var param in request.Parameters)
                {
                    sb.AppendLine($"  - {param.Key}: {param.Value}");
                }
            }

            return sb.ToString();
        }

        private static string BuildCacheKey(AggregationRequest request)
        {
            var sb = new StringBuilder(CacheKeyPrefix);

            if (!string.IsNullOrEmpty(request.Query))
                sb.Append($"q={request.Query}:");

            if (!string.IsNullOrEmpty(request.Language))
                sb.Append($"lang={request.Language}:");

            if (!string.IsNullOrEmpty(request.Country))
                sb.Append($"country={request.Country}:");

            if (request.Sort != SortOption.Relevance)
                sb.Append($"sort={request.Sort}:");

            foreach (var param in request.Parameters.OrderBy(p => p.Key))
            {
                sb.Append($"{param.Key}={param.Value}:");
            }

            return sb.ToString().TrimEnd(':').ToLowerInvariant();
        }

        private async Task<string> CallOpenAIAsync(string userMessage, CancellationToken cancellationToken)
        {
            var apiKey = configuration[$"{ConfigKey}:ApiKey"];
            var model = configuration[$"{ConfigKey}:Model"];
            var baseUrl = configuration[$"{ConfigKey}:Url"];

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = configuration.GetValue($"{ConfigKey}:MaxTokens", 500),
                temperature = configuration.GetValue($"{ConfigKey}:Temperature", 0.7)
            };

            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(baseUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var generatedPrompt = jsonResponse
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrEmpty(generatedPrompt))
            {
                throw new InvalidOperationException("OpenAI returned empty response");
            }

            return generatedPrompt;
        }
    }
}