using RedisSharp;
using Microsoft.AspNetCore.Identity;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace RedisSharpIdentity.Stores
{
    public class RedisRoleStore<TRole> : IRoleStore<TRole> where TRole : RedisIdentityRole, new()
    {
        public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
        {
            var creationResult = await RedisRepository.CreateAsync<TRole>(role);
            if (!creationResult.Succeeded)
            {
                return IdentityResult.Failed();
            }
            
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
        {
            await role.DeleteAsync();
            return IdentityResult.Success;
        }

        public void Dispose()
        {

        }

        public async Task<TRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            return new TRole { Id = roleId };
        }

        public async Task<TRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            var result = await RedisRepository.Query<TRole>(s => s.NameNormalized == normalizedRoleName).ToListAsync(0, 1);

            return result.FirstOrDefault();

        }

        public async Task<string?> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
        {
            await role.PullAsync(s => s.NameNormalized);
            return role.NameNormalized;
        }

        public async Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            return role.Id;
        }

        public async Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
        {
            await role.PullAsync(s => s.Name);

            return role.Name;
        }

        public async Task SetNormalizedRoleNameAsync(TRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.NameNormalized = normalizedName;
            await role.PushAsync(s => s.NameNormalized);
        }

        public async Task SetRoleNameAsync(TRole role, string? roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            await role.PushAsync(s => s.Name);
        }

        public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
        {
            // nothing to do here
            await Task.CompletedTask;

            return IdentityResult.Success;
        }
    }
}
