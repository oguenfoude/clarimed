using FocusMed.Data.Models;

namespace FocusMed.Data.Services;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<IReadOnlyList<User>> GetAllAsync();
    Task<User> AddAsync(User user, string password);
    Task UpdateAsync(User user);
    Task DeleteAsync(int userId);
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task<int> GetCountAsync();
}
