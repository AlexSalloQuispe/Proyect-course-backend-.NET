using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Models;
using UserManagementAPI.Services;
using Microsoft.AspNetCore.Http;
using UserManagementAPI.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor.
// Helpers de OpenAPI y minimal API
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
// NOTA: CORS se eliminó temporalmente para evitar errores de DI al iniciar durante las pruebas locales.

// Servicios de la aplicación
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var app = builder.Build();

// Configurar la tubería HTTP para desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();

    // Endpoint solo para desarrollo para simular excepciones no manejadas (usado por pruebas)
    app.MapGet("/api/debug/throw", () => { throw new InvalidOperationException("Simulated exception for tests"); })
        .WithName("DebugThrow");
}

// Tubería de middlewares personalizada: manejo de excepciones -> autenticación por token -> registro de petición/respuesta
// El manejador de excepciones se registra primero para capturar excepciones no manejadas de middlewares o endpoints internos.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.UseHttpsRedirection();
// Middleware CORS eliminado temporalmente; volver a añadir si es necesario con el registro correcto del servicio.

// ---- Endpoints de gestión de usuarios (CRUD) ----

var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HR", "IT", "Admin" };

var usersGroup = app.MapGroup("/api/users").WithTags("Users");

usersGroup.MapGet("/", async (IUserRepository repo, ILogger<Program> logger) =>
{
    try
    {
        var users = await repo.GetAllAsync();
        return Results.Ok(users);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get users");
        return Results.Problem("Unable to retrieve users", statusCode: StatusCodes.Status500InternalServerError);
    }
})
    .WithName("GetUsers")
    .Produces<IEnumerable<User>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

usersGroup.MapGet("/{id:guid}", async (Guid id, IUserRepository repo, ILogger<Program> logger) =>
{
    try
    {
        var user = await repo.GetByIdAsync(id);
        return user is not null ? Results.Ok(user) : Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get user by id {UserId}", id);
        return Results.Problem("Unable to retrieve user", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GetUserById")
.Produces<User>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status500InternalServerError);

usersGroup.MapPost("/", async (CreateUserRequest request, IUserRepository repo, ILogger<Program> logger) =>
{
    try
    {
        if (!Validate(request, out var errors))
            return Results.ValidationProblem(errors!);

        if (!allowedRoles.Contains(request.Role))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = new[] { $"Invalid role. Allowed: {string.Join(", ", allowedRoles)}" } });

        if (await repo.GetByEmailAsync(request.Email) is not null)
            return Results.Conflict(new { error = "Email already in use" });

        var user = new User(Guid.NewGuid(), request.FirstName, request.LastName, request.Email, request.Role, DateTime.UtcNow);
        await repo.CreateAsync(user);
        return Results.Created($"/api/users/{user.Id}", user);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Validation / business rule failure creating user");
        return Results.Conflict(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create user");
        return Results.Problem("Unable to create user", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("CreateUser")
.Produces<User>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status409Conflict)
.ProducesProblem(StatusCodes.Status500InternalServerError);

usersGroup.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserRepository repo, ILogger<Program> logger) =>
{
    try
    {
        if (!Validate(request, out var errors))
            return Results.ValidationProblem(errors!);

        if (!allowedRoles.Contains(request.Role))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = new[] { $"Invalid role. Allowed: {string.Join(", ", allowedRoles)}" } });

        var existing = await repo.GetByIdAsync(id);
        if (existing is null)
            return Results.NotFound();

        var other = await repo.GetByEmailAsync(request.Email);
        if (other is not null && other.Id != id)
            return Results.Conflict(new { error = "Email already in use" });

        var updated = existing with { FirstName = request.FirstName, LastName = request.LastName, Email = request.Email, Role = request.Role };
        await repo.UpdateAsync(updated);
        return Results.Ok(updated);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Validation / business rule failure updating user {UserId}", id);
        return Results.Conflict(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning(ex, "Update failed: user not found {UserId}", id);
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to update user {UserId}", id);
        return Results.Problem("Unable to update user", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("UpdateUser")
.Produces<User>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status409Conflict)
.ProducesProblem(StatusCodes.Status500InternalServerError);

usersGroup.MapDelete("/{id:guid}", async (Guid id, IUserRepository repo, ILogger<Program> logger) =>
{
    try
    {
        var deleted = await repo.DeleteAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to delete user {UserId}", id);
        return Results.Problem("Unable to delete user", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("DeleteUser")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status500InternalServerError);


// Pequeña función auxiliar para validar objetos con DataAnnotations
static bool Validate<T>(T model, out IDictionary<string, string[]>? errors)
{
    var context = new ValidationContext(model!);
    var results = new List<ValidationResult>();
    if (Validator.TryValidateObject(model!, context, results, true))
    {
        errors = null;
        return true;
    }

    errors = results
        .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty).Select(m => (Member: m, Error: r.ErrorMessage ?? string.Empty)))
        .GroupBy(x => x.Member)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Error).ToArray());

    return false;
}

app.Run();
