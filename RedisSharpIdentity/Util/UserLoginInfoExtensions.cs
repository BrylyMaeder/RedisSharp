using Microsoft.AspNetCore.Identity;

namespace RedisSharpIdentity.Util
{
    public static class UserLoginInfoExtensions
    {
        public static string ToKey(this UserLoginInfo loginInfo)
        {
            return $"{loginInfo.LoginProvider}.{loginInfo.ProviderKey}";
        }
    }
}
