using RedisSharp.Index.Generation;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedisSharp
{
    public class RedisSingleton
    {
        private static RedisSingleton _instance;
        private static readonly object _lock = new object();

        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly IServer _server;

        // Cached IDatabase instance
        private static IDatabase _cachedDatabase;

        private RedisSingleton(string host, int port, string password)
        {
            _connectionMultiplexer = ConnectionMultiplexer.Connect($"{host}:{port},password={password}");
            _server = _connectionMultiplexer.GetServer(host, port);

            // Cache the IDatabase instance directly in the constructor
            _cachedDatabase = _connectionMultiplexer.GetDatabase();

            IndexBuilder.InitializeIndexes();
        }

        public static void Initialize(string host, int port, string password)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new RedisSingleton(host, port, password);
                    }
                }
            }
        }

        public static RedisSingleton Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("RedisSingleton is not initialized. Call Initialize() first.");
                return _instance;
            }
        }

        // Directly expose the cached IDatabase
        public static IDatabase Database => _cachedDatabase;

        public static IServer GetServer() => Instance._server;
    }
}
