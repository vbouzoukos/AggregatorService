namespace AggregatorService.Models.Responses
{
    public class PerformanceBuckets
    {
        /// <summary>
        ///  Count requests that responded into a fast threshold
        /// </summary>
        public int Fast { get; set; }
        /// <summary>
        ///  Count requests that responded into a normal time frame
        /// </summary>
        public int Average { get; set; }
        /// <summary>
        ///  Count requests that responded slower than expected
        /// </summary>
        public int Slow { get; set; }
    }
}
