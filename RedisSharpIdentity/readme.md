# Redis Sharp Identity
A better; faster identity implementation for Redis, built on top of Redis Sharp.

## Features    
- All user and role stores are implemented.
- Built upon RedisSharp (https://github.com/BrylyMaeder/RedisSharp)
- Works out of the box
        

## 🛠️ Install Dependencies    
```bash
nuget install Redis.Sharp.Identity
```

**This will only work for a Redis Database that has Redis Search activated.**

## Important Information
Please review [RedisSharp](https://github.com/BrylyMaeder/RedisSharp) to ensure you're familiar with how the ApplicationUser and ApplicationRole is expected to work. They both implement `IAsyncModel`.

## 🧑🏻‍💻 Setup and Installation
First and and most importantly; initialize your redis singleton.

```csharp
RedisSingleton.Initialize("host", port, "password");
```
Make sure your `ApplicationUser` enherits from `RedisIdentityUser` and your ApplicationRole inherits from `RedisIdentityRole`

```csharp
    public class ApplicationUser : RedisIdentityUser
```
```csharp
    public class ApplicationRole : RedisIdentityRole
```

Next up, add your stores. 

```csharp
builder.Services.AddRedisIdentityStores<ApplicationUser, ApplicationRole>();
```

If you need to use a custom user manager for your project, please ensure that you enherit from 
`RedisUserManager<TUser>` Our manager is necessary and is automatically included with `AddRedisIdentityStores()`


Setup your identity how you like.
```csharp
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;

}).AddDefaultTokenProviders() 
    .AddSignInManager(); 
```

Everything else forwards is pretty standard, include your authentication scheme and you should be all set.
```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.BearerScheme;
    options.DefaultSignInScheme = IdentityConstants.BearerScheme;
}).AddBearerToken();
```


##  Author
#### Bryly Maeder
- Github: https://github.com/BrylyMaeder
        