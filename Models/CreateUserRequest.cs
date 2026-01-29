using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

public record CreateUserRequest(
    [property: Required, MaxLength(100)] string FirstName,
    [property: Required, MaxLength(100)] string LastName,
    [property: Required, EmailAddress] string Email,
    [property: Required, MaxLength(50)] string Role
);