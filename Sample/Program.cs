// See https://aka.ms/new-console-template for more information
using RedisSharp;
using RedisSharp.Factory;
using Sample;

RedisSingleton.Initialize("host", port:1234, "password");

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


await model.HydrateAsync();
model = await RedisRepository.Query<SampleModel>(s => s.Boolean == true).FirstOrDefaultAsync();
var results = await RedisRepository.Query<SampleModel>(s => s.Number == 5).ToListAsync();
results = await RedisRepository.Query<SampleModel>(s => s.Username == "James").ToListAsync();
results = await RedisRepository.Query<SampleModel>(s => (s.Username == "James" && s.Number > 3) || !s.Boolean).ToListAsync();

Thread.Sleep(-1);