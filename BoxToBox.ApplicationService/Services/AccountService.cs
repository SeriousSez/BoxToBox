using BoxToBox.ApplicationService.JwtFeatures;
using BoxToBox.Domain.Entities;
using BoxToBox.Domain.Models;
using BoxToBox.Infrastructure.Repositories;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace BoxToBox.ApplicationService.Services;

public class AccountService : IAccountService
{
	private readonly IAccountRepository _accountRepository;
	private readonly JwtHandler _jwtHandler;

	public AccountService(IAccountRepository accountRepository, JwtHandler jwtHandler)
	{
		_accountRepository = accountRepository;
		_jwtHandler = jwtHandler;
	}

	public async Task<UserModel> RegisterAsync(string userName, string email, string password, string firstName, string lastName)
	{
		var existingUser = await _accountRepository.FindByUserNameAsync(userName);
		if (existingUser != null)
			throw new InvalidOperationException("Username already exists");

		var existingEmail = await _accountRepository.FindByEmailAsync(email);
		if (existingEmail != null)
			throw new InvalidOperationException("Email already exists");

		var userEntity = new UserEntity
		{
			Id = Guid.NewGuid(),
			UserName = userName,
			Email = email,
			FirstName = firstName,
			LastName = lastName,
			EmailConfirmed = false,
			Created = DateTime.UtcNow,
			Modified = DateTime.UtcNow
		};

		var result = await _accountRepository.CreateUserAsync(userEntity, password);
		if (!result.Succeeded)
		{
			var errors = string.Join(", ", result.Errors.Select(e => e.Description));
			throw new InvalidOperationException($"Registration failed: {errors}");
		}

		return MapToModel(userEntity);
	}

	public async Task<(UserModel user, string token)?> LoginAsync(string userNameOrEmail, string password, bool rememberMe = false)
	{
		var user = await _accountRepository.FindByUserNameAsync(userNameOrEmail)
				   ?? await _accountRepository.FindByEmailAsync(userNameOrEmail);

		if (user == null)
			return null;

		var valid = await _accountRepository.CheckPasswordAsync(user, password);
		if (!valid)
			return null;

		user.Modified = DateTime.UtcNow;
		await _accountRepository.UpdateAsync(user);

		// Generate JWT token
		var signingCredentials = _jwtHandler.GetSigningCredentials();
		var claims = await _jwtHandler.GetClaims(user);
		var tokenOptions = _jwtHandler.GenerateTokenOptions(signingCredentials, claims, rememberMe);
		var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

		return (MapToModel(user), token);
	}

	public async Task<UserModel?> GetByIdAsync(Guid id)
	{
		var user = await _accountRepository.FindByIdAsync(id);
		return user != null ? MapToModel(user) : null;
	}

	public async Task<bool> DeleteAsync(Guid id)
	{
		var user = await _accountRepository.FindByIdAsync(id);
		if (user == null)
			return false;

		var result = await _accountRepository.DeleteAsync(user);
		return result.Succeeded;
	}

	private static UserModel MapToModel(UserEntity entity)
	{
		return new UserModel
        {
			Id = entity.Id,
			UserName = entity.UserName ?? string.Empty,
			FirstName = entity.FirstName,
			LastName = entity.LastName,
			Email = entity.Email ?? string.Empty,
			Created = entity.Created,
			Modified = entity.Modified
		};
	}
}
