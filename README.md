# RedisSharp

**RedisSharp** is a powerful, asynchronous C# library designed to simplify interactions with Redis, providing a robust model-based abstraction layer. Built on top of `StackExchange.Redis`, it offers features like automatic indexing, model hydration, unique constraints, and flexible querying, making it ideal for managing complex data structures in Redis.

## Features

- **Asynchronous Operations**: Fully async API for non-blocking Redis interactions.
- **Model-Based Abstraction**: Define data models with `IAsyncModel` and manage them seamlessly.
- **Hydration**: Load model data lazily; use `HydrateAsync()` to populate models, including nested `IAsyncModel` instances with the `[Hydrate]` attribute.
- **Indexing**: Automatic index generation for efficient querying with customizable index types (`Text`, `Numeric`, `Tag`).
- **Unique Constraints**: Enforce uniqueness on properties with the `[Unique]` attribute.
- **Querying**: Flexible `RedisQuery` system for searching and filtering data.
- **Linked Data**: Manage relationships with `AsyncLink` and `AsyncLinks` components.
- **Batch Operations**: Efficiently push and pull multiple models in a single batch.

## Installation

Available on Nuget

```bash
dotnet add package Redis.Sharp
```

## Getting Started

### Initialization

Initialize the `RedisSingleton` with your Redis server details:

```csharp
RedisSingleton.Initialize("localhost", 6379, "yourpassword");
```

### Defining a Model

Implement `IAsyncModel` to create a Redis-backed model:

Note `Profile` or any other `IAsyncModel`s nested within a `IAsyncModel` will be instantiated at the time the model is created. 

```csharp
using RedisSharp;

public class User : IAsyncModel
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    [Unique]
    public string Email { get; set; }
    [Indexed(IndexType.Text)]
    public string Name { get; set; }

    [Hydrate]
    public Profile Profile { get; set; }

    public string IndexName() => "users";
}

public class Profile : IAsyncModel
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Bio { get; set; }

    public string IndexName() => "profiles";
}
```

### Creating and Pushing Models

Models are created and stored in Redis using `RedisRepository`:

```csharp
var user = new User { Id = "user1", Email = "user@example.com", Name = "John Doe", CreatedAt = DateTime.UtcNow };
var result = await RedisRepository.CreateAsync(user);
if (result.Succeeded)
{
    Console.WriteLine("User created successfully!");
}
```

### Hydration

Models are loaded **unhydrated** by default. Use `.HydrateAsync();` to populate them:

```csharp
var user = await RedisRepository.LoadAsync<User>("user1");

// Hydrate specific properties
await user.HydrateAsync(u => u.Email, u => u.Name);

// Hydrate the entire model, including nested [Hydrate]-marked IAsyncModels (e.g., Profile)
await user.HydrateAsync();
Console.WriteLine($"User: {user.Name}, Profile Bio: {user.Profile.GetAsync().Result.Bio}");
```

Use the `[Hydrate]` attribute to fully load all of a nested `IAsyncModel`s contents; however be warned that this is a recursive action that also loads it's own nested `IAsyncModels`

### Querying

Use `RedisQuery` to search and filter models:

```csharp
var query = new RedisQuery<User>(s => s.Name == "John" && s => s.Name != "Benny")
    .Where(s => s.Email == "user@example.com"));

var (users, totalCount, totalPages) = await query.ToPagedListAsync();
foreach (var u in users)
{
    Console.WriteLine($"Found: {u.Name}");
}
```
typical usage:
```csharp
var myModel = RedisRepository.Query<MyModel>(s => s.MyProperty == xyz).ToListAsync();
```

### Managing Relationships

Use `AsyncLink` for single references and `AsyncLinks` for collections:

```csharp
var profile = new Profile { Id = "profile1", Bio = "Software Engineer", CreatedAt = DateTime.UtcNow };
await RedisRepository.CreateAsync(profile);

await user.Profile.SetAsync(profile);
var loadedProfile = await user.Profile.GetAsync();
loadedProfile.HydrateAsync(s => s.Bio);
Console.WriteLine(loadedProfile.Bio); // "Software Engineer"
```

### Deleting Models

Delete models and their associated data:

```csharp
await user.DeleteAsync(); // Cleans up all nested models and links
```

## Key Components

- **RedisSingleton**: Centralized Redis connection management.
- **ModelPushHelper**: Handles model persistence with unique constraint checks.
- **ModelHydrationHelper**: Manages lazy loading and hydration.
- **RedisQuery**: Provides a fluent API for querying Redis.
- **AsyncLink/AsyncLinks**: Simplifies relationship management.
- **IndexDefinitionBuilder**: Generates Redis Search indexes automatically.

## Usage Notes

- **Hydration**: Loaded models are not hydrated by default. Explicitly call `HydrateAsync` to fetch data.
- **Indexing**: Use `[Indexed]` to mark searchable properties; specify `IndexType` for custom behavior.
- **Performance**: Leverage batch operations (`PushAsync`, `HydrateAsync` for collections) to minimize round-trips.
- **Error Handling**: Check `PushResult` or `ModelCreationResult` for operation success.

## Contributing

Feel free to submit issues or pull requests! Ensure all contributions align with the project's coding standards and include tests where applicable.
