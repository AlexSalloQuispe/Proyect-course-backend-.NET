using System.Collections.Concurrent;
using System.Linq;
using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _store = new();
    private readonly ConcurrentDictionary<string, Guid> _emailIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public InMemoryUserRepository()
    {
        // inicializar algunos usuarios de ejemplo
        var u1 = new User(Guid.NewGuid(), "Alice", "Rogers", "alice.rogers@techhive.local", "HR", DateTime.UtcNow);
        var u2 = new User(Guid.NewGuid(), "Bob", "Nguyen", "bob.nguyen@techhive.local", "IT", DateTime.UtcNow);
        _store[u1.Id] = u1;
        _store[u2.Id] = u2;
        _emailIndex[u1.Email] = u1.Id;
        _emailIndex[u2.Email] = u2.Id;
    }

    // Devolver una instantánea por seguridad de hilos y para evitar problemas con LINQ diferido
    public Task<IEnumerable<User>> GetAllAsync() => Task.FromResult(_store.Values.ToList().AsEnumerable());

    public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(_store.TryGetValue(id, out var user) ? user : null);

    public Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult<User?>(null);

        if (_emailIndex.TryGetValue(email, out var id) && _store.TryGetValue(id, out var user))
            return Task.FromResult<User?>(user);

        return Task.FromResult<User?>(null);
    }

    public Task CreateAsync(User user)
    {
        lock (_sync)
        {
            if (_emailIndex.ContainsKey(user.Email))
                throw new InvalidOperationException("Email already in use");

            _store[user.Id] = user;
            _emailIndex[user.Email] = user.Id;
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        lock (_sync)
        {
            if (!_store.ContainsKey(user.Id))
                throw new KeyNotFoundException("User not found");

            // Asegurar unicidad del email: si el email está mapeado a otro id, conflicto
            if (_emailIndex.TryGetValue(user.Email, out var existingId) && existingId != user.Id)
                throw new InvalidOperationException("Email already in use");

            // Actualizar índice si cambió el email
            var current = _store[user.Id];
            if (!string.Equals(current.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                _emailIndex.TryRemove(current.Email, out _);
                _emailIndex[user.Email] = user.Id;
            }

            _store[user.Id] = user;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        lock (_sync)
        {
            if (!_store.TryRemove(id, out var removed))
                return Task.FromResult(false);

            _emailIndex.TryRemove(removed.Email, out _);
            return Task.FromResult(true);
        }
    }
} 