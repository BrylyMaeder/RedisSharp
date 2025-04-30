// See https://aka.ms/new-console-template for more information
using RedisSharp;
using RedisSharp.Factory;
using Sample;

//RedisSingleton.Initialize("host", port: 1234, "password");
RedisSingleton.Initialize("redis-13464.c81.us-east-1-2.ec2.redns.redis-cloud.com", 13464, "4TdQe8UepIdXwrGBGSJwTl5s1nsvYpgN", outputLogs: true);


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
model.Username = $"1-1";
await model.PushAsync(s => s.Username);

var testEnum = SampleModel.TestEnum.B;
var query = RedisRepository.Query<SampleModel>(s => s.MyEnum == testEnum);
var query2 = RedisRepository.Query<SampleModel>(s => s.Number >= 5);
model = await RedisRepository.Query<SampleModel>(s => s.MyEnum == SampleModel.TestEnum.B).FirstOrDefaultAsync();
var results = await query2.ToListAsync();

var query23 = RedisRepository.Query<SampleModel>(s => s.Number == 5 && s.Username.StartsWith("1"));
results = await query23.ToListAsync();
//results = await RedisRepository.Query<SampleModel>(s => (s.Username == "James" && s.Number > 3) || !s.Boolean).ToListAsync();
//var results = await RedisRepository.Query<SampleModel>().ToListAsync();


var testQuery = await RedisRepository.Query<SampleModel>(s => s.Number == 5).SortBy(sortFields: new SortField<SampleModel>(s => s.CreatedAt, true)).WithHydration().ToListAsync();


Thread.Sleep(-1);