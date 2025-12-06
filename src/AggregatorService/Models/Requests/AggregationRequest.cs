using System.ComponentModel.DataAnnotations;

namespace AggregatorService.Models.Requests
{
    /// <summary>
    /// Request model for aggregating data from multiple external APIs.
    /// Each provider will use the parameters it supports and ignore the rest.
    /// Not all providers will use all parameters.
    /// </summary>
    public class AggregationRequest
    {
        /// <summary>
        /// Key-value pairs of search parameters
        /// Each provider will use the parameters it supports
        /// Examples: city=London, query=technology, hashtag=news
        /// </summary>
        [Required(ErrorMessage = "Parameters are required")]
        [MinLength(1, ErrorMessage = "At least one parameter is required")]
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}