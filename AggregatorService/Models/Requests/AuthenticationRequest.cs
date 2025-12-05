using System.ComponentModel.DataAnnotations;

namespace AggregatorService.Models.Requests
{
    public class AuthenticationRequest
    {
        [Required(ErrorMessage = "Username is missing")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 60 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is missing")]
        [StringLength(80, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 80 characters")]
        public string Password { get; set; } = string.Empty;
    }
}