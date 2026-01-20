using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace BoxToBox.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
	private readonly UserManager<UserEntity> _userManager;

	public AccountRepository(UserManager<UserEntity> userManager)
	{
		_userManager = userManager;
	}

	public Task<IdentityResult> CreateUserAsync(UserEntity user, string password)
	{
		return _userManager.CreateAsync(user, password);
	}

	public Task<UserEntity?> FindByUserNameAsync(string userName)
	{
		return _userManager.FindByNameAsync(userName);
	}

	public Task<UserEntity?> FindByEmailAsync(string email)
	{
		return _userManager.FindByEmailAsync(email);
	}

	public Task<UserEntity?> FindByIdAsync(Guid id)
	{
		return _userManager.FindByIdAsync(id.ToString());
	}

	public Task<bool> CheckPasswordAsync(UserEntity user, string password)
	{
		return _userManager.CheckPasswordAsync(user, password);
	}

	public async Task UpdateAsync(UserEntity user)
	{
		user.Modified = DateTime.UtcNow;
		await _userManager.UpdateAsync(user);
	}

	public Task<IdentityResult> DeleteAsync(UserEntity user)
	{
		return _userManager.DeleteAsync(user);
	}
}
