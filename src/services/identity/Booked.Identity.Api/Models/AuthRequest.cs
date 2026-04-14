using System.ComponentModel.DataAnnotations;

namespace Booked.Identity.Api.Models;

public class CustomerRegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required, MinLength(8)]
    public required string Password { get; set; }

    [Required, MinLength(2)]
    public required string FullName { get; set; }
}

public class CustomerLoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }
}

public class OrganizationRegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required, MinLength(8)]
    public required string Password { get; set; }

    [Required, MinLength(2)]
    public required string OrganizationName { get; set; }

    [Required]
    public required string SubscriptionType { get; set; }
}

public class OrganizationLoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }
}

public class AdminLoginRequest
{
    [Required]
    public required string AdminKey { get; set; }

    [Required]
    public required string Password { get; set; }
}