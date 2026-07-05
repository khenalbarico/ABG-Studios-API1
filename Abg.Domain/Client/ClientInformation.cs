using System.ComponentModel.DataAnnotations;

namespace Abg.Domain.Client;

public sealed class ClientInformation
{
    public string ClientBookingId { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email           { get; set; } = "";

    [Required]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Use PH format: 09XXXXXXXXX")]
    public string ContactNumber   { get; set; } = "";

    [Required]
    public string FirstName       { get; set; } = "";

    [Required]
    public string LastName        { get; set; } = "";

    public DateTime BookingDate   { get; set; }
}
