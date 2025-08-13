using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Redis.Client
{
    public class RedisClient : IRedisClient
    {
        private readonly IDatabase _db;
        private readonly RedLockFactory _redisLock;

        private readonly TimeSpan _expiryTime = TimeSpan.FromMilliseconds(3000);
        private readonly TimeSpan _waitTime = TimeSpan.FromMilliseconds(10000);
        private readonly TimeSpan _retryTime = TimeSpan.FromMilliseconds(25);

        #region ctor

        public RedisClient(IConfiguration configuration)
        {
            const string connectionString = "{0},abortConnect=false,defaultDatabase={1},ssl=false,ConnectTimeout={2},allowAdmin=true,connectRetry={3}";

            var redis = ConnectionMultiplexer.Connect(
                string.Format(connectionString,
                    configuration[RedisConfigurationNames.Url],
                    configuration[RedisConfigurationNames.DefaultDatabase],
                    configuration[RedisConfigurationNames.ConnectTimeout],
                    configuration[RedisConfigurationNames.ConnectRetry]));

            _redisLock = RedLockFactory.Create(new List<RedLockMultiplexer> { redis });

            _db = redis.GetDatabase();
        }

        #endregion

        #region Public Methods

        #region Transaction

        public ITransaction BeginTransaction()
        {
            return _db.CreateTransaction();
        }

        public async Task ExecuteTransactionAsync(ITransaction trans)
        {
            await trans.ExecuteAsync();
        }

        #endregion

        #region Lock

        public bool IsLock(string key, string value, TimeSpan expireTime)
        {
            if (_db.KeyExists(key.ToLower())) return true;

            var isAdded = _db.StringSet(key, value, expireTime, When.NotExists);

            return !isAdded;
        }

        public async Task RemoveLock(string key)
        {
            await _db.KeyDeleteAsync(key.ToLower());
        }

        public async Task<bool> TryAcquireLock(string resource)
        {
            var redLock = await _redisLock.CreateLockAsync(resource, _expiryTime, _waitTime, _retryTime);

            return redLock.IsAcquired;
        }

        public void ReleaseLock()
        {
            _redisLock.Dispose();
        }

        public async Task<IRedLock> LockInstance(string key)
        {
            return await _redisLock.CreateLockAsync(key, _expiryTime, _waitTime, _retryTime);
        }

        #endregion       

        public async Task<bool> DeleteAsync(string key)
        {
            return await _db.KeyDeleteAsync(key.ToLower());
        }

        public bool Exists(string key)
        {
            return _db.KeyExists(key.ToLower());
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _db.KeyExistsAsync(key.ToLower());
        }

        public async Task<bool> AddAsync<T>(string key, T value, int ttl = 24) where T : class
        {
            var stringContent = SerializeContent(value);

            return await _db.StringSetAsync(key.ToLower(), stringContent, TimeSpan.FromHours(ttl));
        }

        public async Task<bool> AddTimelessAsync<T>(string key, T value) where T : class
        {
            var stringContent = SerializeContent(value);

            return await _db.StringSetAsync(key.ToLower(), stringContent);
        }

        public async Task<bool> AddStringAsync<T>(string key, T value, int ttl) where T : class
        {
            var stringContent = SerializeContent(value);

            return await _db.StringSetAsync(key.ToLower(), stringContent, TimeSpan.FromHours(ttl));
        }

        public async Task<bool> AddAsync<T>(T value, int ttl = 120) where T : class
        {
            var stringContent = SerializeContent(value);

            return await _db.StringSetAsync(RedisKeyGenerator.GetKey(typeof(T)), stringContent, TimeSpan.FromMinutes(ttl));
        }

        public T Get<T>() where T : class
        {
            try
            {
                var value = _db.StringGet(RedisKeyGenerator.GetKey(typeof(T)));
                if (value.HasValue && !value.IsNullOrEmpty)
                    return DeserializeContent<T>(value);

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _db.StringGetAsync(key.ToLower());

                if (value.HasValue && !value.IsNullOrEmpty) 
                    return DeserializeContent<T>(value);

                return null;
            }
            catch
            {
                return null;
            }
        }

        #region List

        public async Task<List<T>> GetListAsync<T>(string key)
        {
            var itemCount = await _db.ListLengthAsync(key.ToLower());
            var items = await _db.ListRangeAsync(key.ToLower(), 0, itemCount);

            var result = new List<T>();

            foreach (var item in items)
            {
                try
                {
                    result.Add(JsonConvert.DeserializeObject<T>(item.ToString()));
                }
                catch
                {
                    // ignored
                }
            }

            return result;
        }

        public async Task AddToListAsync<T>(string key, T objectToCache)
        {
            await _db.ListRightPushAsync(key.ToLower(), JsonConvert.SerializeObject(objectToCache));
        }

        public async Task RemoveFromListAsync<T>(string key, T removeObjectToCache, int count = 0)
        {
            await _db.ListRemoveAsync(key.ToLower(), JsonConvert.SerializeObject(removeObjectToCache), count);
        }

        #endregion

        #region Hash

        public async Task<HashEntry[]> HashGetAllAsync(string key)
        {
            return await _db.HashGetAllAsync(key.ToLower());
        }

        public async Task<bool> HashExistsAsync(string key, string hashField)
        {
            return await _db.HashExistsAsync(key.ToLower(), hashField);
        }

        public async Task<string> GetHashAsync(string key, string hashField)
        {
            return await _db.HashGetAsync(key.ToLower(), hashField);
        }

        public async Task HashSetAsync(string key, params KeyValuePair<object, object>[] hashValues)
        {
            var hashEntries = new HashEntry[hashValues.Length];

            for (var i = 0; i < hashValues.Length; i++)
            {
                hashEntries[i] = new HashEntry(hashValues[i].Key.ToString(), hashValues[i].Value.ToString());
            }

            await _db.HashSetAsync(key.ToLower(), hashEntries);
        }

        public async Task HashDeleteAsync(string key, string hashField)
        {
            await _db.HashDeleteAsync(key.ToLower(), hashField);
        }

        #endregion

        #region Sorted Set

        public long SortedSetLength(string key)
        {
            return _db.SortedSetLength(key.ToLower());
        }

        public void AddToSortedSet<T>(string key, T objectToCache, double score)
        {
            _db.SortedSetRemoveRangeByScore(key.ToLower(), score, score);
            _db.SortedSetAdd(key.ToLower(), JsonConvert.SerializeObject(objectToCache), score);
        }

        public void RemoveSortedSet<T>(string key, T objectToCache)
        {
            _db.SortedSetRemove(key.ToLower(), JsonConvert.SerializeObject(objectToCache));
        }

        public List<T> GetSortedSetByRange<T>(string key, double scoreRangeStart = double.NegativeInfinity,
            double scoreRangeFinish = double.PositiveInfinity)
        {
            var items = _db.SortedSetRangeByScore(key.ToLower(), scoreRangeStart, scoreRangeFinish);

            var result = new List<T>();

            foreach (var item in items)
            {
                try
                {
                    result.Add(JsonConvert.DeserializeObject<T>(item.ToString()));
                }
                catch
                {
                    // ignored
                }
            }

            return result;
        }

        public List<T> GetSortedSet<T>(string key)
        {
            var items = _db.SortedSetRangeByRank(key.ToLower());

            var result = new List<T>();

            foreach (var item in items)
            {
                try
                {
                    result.Add(JsonConvert.DeserializeObject<T>(item.ToString()));
                }
                catch
                {
                    // ignored
                }
            }

            return result;
        }

        #endregion     

        public double GetScore(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0
                : value.GetDeterministicHashCode();
        }

        #endregion

        #region Private Methods

        private static string SerializeContent(object value)
        {
            return JsonConvert.SerializeObject(value);
        }

        private static T DeserializeContent<T>(RedisValue value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }

        #endregion
    }
}
