// See https://aka.ms/new-console-template for more information
using RedisSharp;
using Sample;

RedisSingleton.Initialize("host", port:00000, "password");

var model = await RedisRepository.LoadAsync<SampleModel>("test");
if (model == null)
{
    var result = await RedisRepository.CreateAsync<SampleModel>("test");
    if (result.Succeeded)
    {
        model = result.Data;
    }
    else 
    {
        throw new Exception("Couldn't create object.");
    }
}

model = await RedisRepository.Query<SampleModel>(s => s.Boolean == true).FirstOrDefaultAsync();
var results = await RedisRepository.Query<SampleModel>(s => s.Number == 5).ToListAsync();
results = await RedisRepository.Query<SampleModel>(s => s.Username == "James").ToListAsync();
results = await RedisRepository.Query<SampleModel>(s => (s.Username == "James" && s.Number > 3) || !s.Boolean).ToListAsync();

Thread.Sleep(-1);