using BoxToBox.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IAccountRepository
{
	Task<IdentityResult> CreateUserAsync(UserEntity user, string password);
	Task<UserEntity?> FindByUserNameAsync(string userName);
	Task<UserEntity?> FindByEmailAsync(string email);
	Task<UserEntity?> FindByIdAsync(Guid id);
	Task<bool> CheckPasswordAsync(UserEntity user, string password);
	Task UpdateAsync(UserEntity user);
	Task<IdentityResult> DeleteAsync(UserEntity user);
}
