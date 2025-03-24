using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace RedisSharpIdentity.Services
{
    public class UserService<TUser> where TUser : RedisIdentityUser
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly UserManager<TUser> _userManager;

        public UserService(AuthenticationStateProvider authenticationStateProvider, UserManager<TUser> userManager)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _userManager = userManager;
        }

        private async Task<ClaimsPrincipal> GetCurrentUserClaimsAsync()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            return authState.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        }

        private async Task<string?> GetCurrentUserIdAsync()
        {
            var user = await GetCurrentUserClaimsAsync();
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                // Return null to indicate an invalid user ID
                return null;
            }

            return userId;
        }

        private TUser _cachedUser { get; set; }
        public async Task<TUser?> GetCurrentUserAsync()
        {
            if (_cachedUser != null)
                return _cachedUser;

            var userId = await GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var user = await LoadUserByIdAsync(userId);

            _cachedUser = user;

            return user;
        }

        public async Task<TUser?> GetUserByIdAsync(string userId)
        {
            return await LoadUserByIdAsync(userId);
        }


        private async Task<TUser?> LoadUserByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }
    }
}
