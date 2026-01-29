namespace UserManagementAPI.Models;

public record User(Guid Id, string FirstName, string LastName, string Email, string Role, DateTime CreatedAt);