using System.ComponentModel.DataAnnotations;

namespace LegacyApiProxy.Models;

public sealed class ChangePasswordRequest
{
    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}
