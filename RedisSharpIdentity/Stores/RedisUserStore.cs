using Microsoft.AspNetCore.Identity;
using RedisSharpIdentity.Data;
using RedisSharpIdentity.Util;
using System.Security.Claims;
using RedisSharp;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace RedisSharpIdentity.Stores
{
    public class RedisUserStore<TUser, TRole> : IUserStore<TUser>, IUserEmailStore<TUser>, IUserClaimStore<TUser>, IUserLockoutStore<TUser>, IUserTwoFactorStore<TUser>,
        IUserLoginStore<TUser>, IUserPasswordStore<TUser>, IUserPhoneNumberStore<TUser>, IUserRoleStore<TUser>, IUserSecurityStampStore<TUser>,
        IUserAuthenticatorKeyStore<TUser>, IUserAuthenticationTokenStore<TUser>, IUserTwoFactorRecoveryCodeStore<TUser>
        where TUser : RedisIdentityUser, new() where TRole : RedisIdentityRole, new()
    {
        private readonly RedisRoleStore<TRole> _roleStore;

        public RedisUserStore(RedisRoleStore<TRole> roleStore)
        {
            _roleStore = roleStore ?? throw new ArgumentNullException(nameof(roleStore));
        }

        #region User
        public async Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return user.Id;
        }

        public async Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.UserName);
            return user.UserName;
        }

        public async Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName ?? string.Empty;
            await user.PushAsync(s => s.UserName);
        }

        public async Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.UserName);
            return user.UserName;
        }

        public async Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(normalizedName))
                normalizedName = string.Empty;

            user.NormalizedUsername = normalizedName;
            await user.PushAsync(s => s.NormalizedUsername);
        }

        public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
        {
            //fail to create a user if the username is not set.
            if (string.IsNullOrEmpty(user.UserName))
            {
                // Clean up passwords/emails or any other set information before this point.
                await user.DeleteAsync();

                return IdentityResult.Failed();
            }
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
        {
            user.LastUpdate = DateTime.Now;
            await user.PushAsync(s => s.LastUpdate);

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.DeleteAsync();
            return IdentityResult.Success;
        }

        public async Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be null, empty, or consist only of whitespace.", nameof(userId));

            return await RedisRepository.LoadAsync<TUser>(userId);
        }

        public async Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(normalizedUserName))
                throw new ArgumentException("User name cannot be null, empty, or consist only of whitespace.", nameof(normalizedUserName));

            var query = RedisRepository.Query<TUser>(s => s.NormalizedUsername == normalizedUserName);
            var results = await query.ToListAsync(0, 1);

            return results.FirstOrDefault();
        }

        public void Dispose()
        {

        }
        #endregion

        #region Email

        public async Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            ArgumentNullException.ThrowIfNull(email);

            user.Email = email;
            await user.PushAsync(s => s.Email);
        }

        public async Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.Email);
            return user.Email;
        }

        public async Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.EmailConfirmed);
            return user.EmailConfirmed;
        }

        public async Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            user.EmailConfirmed = confirmed;
            await user.PushAsync(s => s.EmailConfirmed);
        }

        public async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            var result = await RedisRepository.Query<TUser>(s => s.NormalizedEmail == normalizedEmail).ToListAsync(0, 1);
            return result.FirstOrDefault();
        }

        public async Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.NormalizedEmail);
            return user.NormalizedEmail;
        }

        public async Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail ?? throw new Exception("Email cannot be empty!");
            await user.PushAsync(s => s.NormalizedEmail);
        }

        #endregion

        #region Claims
        public async Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            ArgumentNullException.ThrowIfNull(claims);

            foreach (var claim in claims)
            {
                var claimDocument = IdentityClaim.Create(claim);

                await claimDocument.Users.AddOrUpdateAsync(user);
                await user.Claims.AddOrUpdateAsync(claimDocument);
            }
        }


        public async Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");

            List<Claim> claims = new List<Claim>();

            var claimDocuments = await user.Claims.GetAllAsync();
            foreach (var claimDoc in claimDocuments)
            {
                await claimDoc.PullAsync(s => s.Claim);
                claims.Add(claimDoc.Claim);
            }

            return claims;
        }

        public async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
        {
            var claimDocument = IdentityClaim.Create(claim.ToKey());
            var users = await claimDocument.Users.GetAllAsync();

            return (IList<TUser>)users;
        }


        public async Task RemoveClaimAsync(TUser user, Claim claim)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            ArgumentNullException.ThrowIfNull(claim);

            var targetClaim = await user.Claims.GetAsync(claim.ToKey());

            if (targetClaim != null)
            {
                await user.Claims.RemoveAsync(targetClaim.Id);
                await targetClaim.Users.RemoveAsync(claim.ToKey());

                var totalUsers = await targetClaim.Users.CountAsync();
                if (totalUsers == 0)
                    await targetClaim.DeleteAsync();
            }
        }

        public async Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            ArgumentNullException.ThrowIfNull(claims);

            foreach (var claim in claims)
            {
                await RemoveClaimAsync(user, claim);
            }
        }

        public async Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            ArgumentNullException.ThrowIfNull(claim);
            ArgumentNullException.ThrowIfNull(newClaim);

            var claimModel = IdentityClaim.Create(claim);
            claimModel.Claim = newClaim;

            await claimModel.PushAsync(s => s.Claim);

            await user.Claims.AddOrUpdateAsync(claimModel);
        }
        #endregion

        #region Lockout
        public async Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");

            await user.PullAsync(s => s.AccessFailedCount);
            user.AccessFailedCount++;
            await user.PushAsync(s => s.AccessFailedCount);
            return user.AccessFailedCount;
        }

        public async Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            user.AccessFailedCount = 0;
            await user.PushAsync(s => s.AccessFailedCount);
        }

        public async Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            await user.PullAsync(s => s.AccessFailedCount);
            return user.AccessFailedCount;
        }

        public async Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            await user.PullAsync(s => s.LockoutEnabled);
            return user.LockoutEnabled;
        }

        public async Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            user.LockoutEnabled = enabled;
            await user.PushAsync(s => s.LockoutEnabled);
        }

        public async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            await user.PullAsync(s => s.LockoutEnd);
            return user.LockoutEnd;
        }

        public async Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            user.LockoutEnd = (DateTimeOffset)lockoutEnd;
            await user.PushAsync(s => s.LockoutEnd);
        }
        #endregion

        #region Login
        public async Task<TUser> FindAsync(UserLoginInfo login)
        {
            ArgumentNullException.ThrowIfNull(login);

            var externalLogin = ExternalLogin.Create(login);

            return (TUser)await externalLogin.User.GetAsync();
        }

        public async Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            var externalLogin = ExternalLogin.Create(login);
            externalLogin.LoginInfo = login;

            await Task.WhenAll(user.Logins.AddOrUpdateAsync(externalLogin),
                externalLogin.User.SetAsync(user), 
                externalLogin.PushAsync(s => s.LoginInfo));

        }

        public async Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            var key = $"{loginProvider}.{providerKey}";
            var login = ExternalLogin.Create(key);

            await Task.WhenAll(login.DeleteAsync(), user.Logins.RemoveAsync(login.Id));
        }

        public async Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
        {
            var loginDocuments = await user.Logins.GetAllAsync();

            var loginInfos = new List<UserLoginInfo>();

            foreach (var loginDocument in loginDocuments)
            {
                loginInfos.Add(loginDocument.LoginInfo);
            }

            return loginInfos;
        }

        public async Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            var key = $"{loginProvider}.{providerKey}";
            var login = ExternalLogin.Create(key);

            return (TUser?)await login.User.GetAsync();
        }
        #endregion

        #region Password
        public async Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.PasswordHash);
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            return user.PasswordHash;
        }

        public async Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            await user.PullAsync(s => s.PasswordHash);
            var hash = user.PasswordHash;
            return !string.IsNullOrEmpty(hash);
        }

        public async Task SetPasswordHashAsync(TUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            user.PasswordHash = passwordHash ?? string.Empty;
            await user.PushAsync(s => s.PasswordHash);
        }
        #endregion

        #region PhoneNumber
        public async Task<string> GetPhoneNumberAsync(TUser user, CancellationToken token)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            }

            await user.PullAsync(s => s.PhoneNumber);
            return user.PhoneNumber;
        }

        public async Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.PhoneNumberConfirmed);

            return user.PhoneNumberConfirmed;
        }


        public async Task SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            }

            if (string.IsNullOrEmpty(phoneNumber)) throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));

            user.PhoneNumber = phoneNumber;
            await user.PushAsync(s => s.PhoneNumber);
        }

        public async Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "The user parameter cannot be null.");
            }

            user.PhoneNumberConfirmed = true;
            await user.PushAsync(s => s.PhoneNumberConfirmed);

        }
        #endregion

        #region Roles
        public async Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be null, empty, or whitespace.", nameof(roleName));

            var normalizedRoleName = roleName.ToUpperInvariant();
            var role = await _roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            await Task.WhenAll(user.Roles.AddOrUpdateAsync(role), role.Users.AddOrUpdateAsync(user));
        }

        public async Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be null, empty, or whitespace.", nameof(roleName));

            var normalizedRoleName = roleName.ToUpperInvariant();
            var role = await _roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            await Task.WhenAll(user.Roles.RemoveAsync(role.Id), role.Users.RemoveAsync(user.Id));
        }

        public async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
        {
            var roles = await user.Roles.GetAllAsync();
            return roles.Select(role => role.Id).ToList();
        }

        public async Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be null, empty, or whitespace.", nameof(roleName));

            var normalizedRoleName = roleName.ToUpperInvariant();
            var role = await _roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);
            if (role == null) return false;

            return await user.Roles.ContainsAsync(role.Id);
        }

        public async Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name cannot be null, empty, or whitespace.", nameof(roleName));

            var normalizedRoleName = roleName.ToUpperInvariant();
            var role = await _roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);
            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' does not exist.");


            return (IList<TUser>)await role.Users.GetAllAsync();
        }
        #endregion

        #region Security Stamp
        public async Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken token)
        {
            user.SecurityStamp = stamp;
            await user.PushAsync(s => s.SecurityStamp);
        }

        public async Task<string> GetSecurityStampAsync(TUser user, CancellationToken token)
        {
            await user.PullAsync(s => s.SecurityStamp);

            return user.SecurityStamp;
        }
        #endregion

        #region Two Factor
        public async Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken token)
        {
            user.TwoFactorEnabled = enabled;
            await user.PushAsync(s => s.TwoFactorEnabled);
        }

        public async Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken token)
        {
            await user.PullAsync(s => s.TwoFactorEnabled);

            return user.TwoFactorEnabled;
        }
        #endregion

        #region Authenticator Key
        public async Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken)
        {
            user.AuthenticatorKey = key;
            await user.PushAsync(s => s.AuthenticatorKey);
        }

        public async Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
        {
            await user.PullAsync(s => s.AuthenticatorKey);
            return user.AuthenticatorKey;
        }
        #endregion

        #region Authenticator Token Store
        public async Task SetTokenAsync(TUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
        {
            // Compose the key
            var tokenKey = $"{loginProvider}:{name}";

            // Set or remove token
            if (!string.IsNullOrEmpty(value))
            {
                await user.AuthenticatorTokens.SetAsync(tokenKey, value);
            }
            else
            {
                await user.AuthenticatorTokens.RemoveAsync(tokenKey);
            }
        }

        public async Task RemoveTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            // Compose the key
            var tokenKey = $"{loginProvider}:{name}";

            // Remove token
            await user.AuthenticatorTokens.RemoveAsync(tokenKey);
        }

        public async Task<string?> GetTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            // Compose the key
            var tokenKey = $"{loginProvider}:{name}";

            // Retrieve token
            return await user.AuthenticatorTokens.GetByKeyAsync(tokenKey);
        }
        #endregion

        #region Two Factor Recovery Code Store

        public async Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
        {
            // Clear existing recovery codes and add new ones
            user.AuthenticatorRecoveryCodes = recoveryCodes.ToList();
            await user.PushAsync(s => s.AuthenticatorRecoveryCodes);
        }

        public async Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
        {
            return user.AuthenticatorRecoveryCodes.Remove(code);
        }

        public async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
        {
            return user.AuthenticatorRecoveryCodes.Count;
        }

        #endregion

    }
}
