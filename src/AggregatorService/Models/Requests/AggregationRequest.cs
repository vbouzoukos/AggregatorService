using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AggregatorService.Models.Requests
{
    /// <summary>
    /// Unified sort options supported across providers.
    /// Each provider maps these to their native sort parameters.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SortOption
    {
        Relevance,
        Newest,
        Oldest,
        Popularity
    }
    /// <summary>
    /// Request model for aggregating data from multiple external APIs.
    /// Each provider will use the parameters it supports and ignore the rest.
    /// Not all providers will use all parameters.
    /// </summary>

    public class AggregationRequest
    {
        /// <summary>
        /// Unified sorting parameter applied across providers where applicable.
        /// Each provider maps this to its native sort parameter.
        /// Accepted values: Relevance, Newest, Oldest, Popularity
        /// Default: Relevance
        /// </summary>
        public SortOption Sort { get; set; } = SortOption.Relevance;

        /// <summary>
        /// Search query filter
        /// Used by: News, Books
        /// </summary>
        public string? Query { get; set; }

        /// <summary>
        /// Country filter - ISO 3166 country code
        /// Used by: Weather (geocoding)
        /// Examples: GB, US, DE, FR
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// Language filter - ISO 639-1 language code
        /// Used by: News, Books
        /// Examples: en, de, fr, es
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Additional key-value pairs of provider-specific parameters.
        /// Examples: city=London, author=Tolkien, from=2025-01-01
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}