namespace AggregatorService.Models.Responses
{
    /// <summary>
    /// Standardized response from external API providers
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Name of the provider that returned this response
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the API call was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Response data from the API
        /// </summary>
        public dynamic? Data { get; set; }

        /// <summary>
        /// Error message if the call failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Time taken to get the response
        /// </summary>
        public TimeSpan ResponseTime { get; set; }
    }
}
