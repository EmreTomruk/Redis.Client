using RedLockNet;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Redis.Client
{
    public interface IRedisClient
    {
        ITransaction BeginTransaction();
        Task ExecuteTransactionAsync(ITransaction trans);
        bool IsLock(string key, string value, TimeSpan expireTime);
        Task RemoveLock(string key);
        Task<bool> TryAcquireLock(string resource);
        void ReleaseLock();
        Task<IRedLock> LockInstance(string key);
        Task<bool> DeleteAsync(string key);
        bool Exists(string key);
        Task<bool> ExistsAsync(string key);

        Task<bool> AddAsync<T>(string key, T value, int ttl = 24) where T : class;

        Task<bool> AddTimelessAsync<T>(string key, T value) where T : class;

        Task<bool> AddStringAsync<T>(string key, T value, int ttl) where T : class;

        Task<bool> AddAsync<T>(T value, int ttl = 24) where T : class;

        Task<T> GetAsync<T>(string key) where T : class;

        T Get<T>() where T : class;

        Task AddToListAsync<T>(string key, T objectToCache);

        Task<List<T>> GetListAsync<T>(string key);

        Task RemoveFromListAsync<T>(string key, T removeObjectToCache, int count = 0);
        Task<HashEntry[]> HashGetAllAsync(string key);
        Task<bool> HashExistsAsync(string key, string hashField);
        Task<string> GetHashAsync(string key, string hashField);
        Task HashSetAsync(string key, params KeyValuePair<object, object>[] hashValues);
        Task HashDeleteAsync(string key, string hashField);
        List<T> GetSortedSetByRange<T>(string key, double scoreRangeStart = double.NegativeInfinity,
            double scoreRangeFinish = double.PositiveInfinity);

        List<T> GetSortedSet<T>(string key);

        void RemoveSortedSet<T>(string key, T objectToCache);

        void AddToSortedSet<T>(string key, T objectToCache, double score);

        long SortedSetLength(string key);

        double GetScore(string value);
    }
}
