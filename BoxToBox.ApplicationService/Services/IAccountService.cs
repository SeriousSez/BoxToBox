using BoxToBox.Domain.Models;

namespace BoxToBox.ApplicationService.Services;

public interface IAccountService
{
	Task<UserModel> RegisterAsync(string userName, string email, string password, string firstName, string lastName);
	Task<(UserModel user, string token)?> LoginAsync(string userNameOrEmail, string password, bool rememberMe = false);
	Task<UserModel?> GetByIdAsync(Guid id);
	Task<bool> DeleteAsync(Guid id);
}
