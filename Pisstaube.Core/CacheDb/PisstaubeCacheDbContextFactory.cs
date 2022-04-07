using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore.Storage;
using osu.Framework.Platform;

namespace Pisstaube.CacheDb
{
    // Copy paste of https://github.com/ppy/osu/blob/master/osu.Game/Database/DatabaseContextFactory.cs
    public sealed class PisstaubeCacheDbContextFactory
    {
        private readonly Storage _storage;

        private const string DatabaseName = @"cache";

        private ThreadLocal<PisstaubeCacheDbContext> _threadContexts;

        private readonly object _writeLock = new();

        private bool _currentWriteDidWrite;
        private bool _currentWriteDidError;

        private int _currentWriteUsages;

        private IDbContextTransaction _currentWriteTransaction;

        public PisstaubeCacheDbContextFactory(Storage storage)
        {
            _storage = storage;
            RecycleThreadContexts();
        }

        public PisstaubeCacheDbContext Get() => _threadContexts.Value;

        public DbWriteUsage GetForWrite(bool withTransaction = true)
        {
            Monitor.Enter(_writeLock);
            PisstaubeCacheDbContext context;

            try
            {
                if (_currentWriteTransaction == null && withTransaction)
                {
                    if (_threadContexts.IsValueCreated)
                        RecycleThreadContexts();

                    context = _threadContexts.Value;
                    _currentWriteTransaction = context.Database.BeginTransaction();
                }
                else
                {
                    context = _threadContexts.Value;
                }
            }
            catch
            {
                Monitor.Exit(_writeLock);
                throw;
            }

            Interlocked.Increment(ref _currentWriteUsages);

            return new DbWriteUsage(context, UsageCompleted)
                {IsTransactionLeader = _currentWriteTransaction != null && _currentWriteUsages == 1};
        }

        private void UsageCompleted(DbWriteUsage usage)
        {
            var usages = Interlocked.Decrement(ref _currentWriteUsages);

            try
            {
                _currentWriteDidWrite |= usage.PerformedWrite;
                _currentWriteDidError |= usage.Errors.Any();

                if (usages == 0)
                {
                    if (_currentWriteDidError)
                        _currentWriteTransaction?.Rollback();
                    else
                        _currentWriteTransaction?.Commit();

                    if (_currentWriteDidWrite || _currentWriteDidError)
                    {
                        usage.Context.Dispose();
                        RecycleThreadContexts();
                    }

                    _currentWriteTransaction = null;
                    _currentWriteDidWrite = false;
                    _currentWriteDidError = false;
                }
            }
            finally
            {
                Monitor.Exit(_writeLock);
            }
        }

        private void RecycleThreadContexts()
        {
            _threadContexts?.Value.Dispose();
            _threadContexts = new ThreadLocal<PisstaubeCacheDbContext>(CreateContext, true);
        }

        private PisstaubeCacheDbContext CreateContext() =>
            new(_storage.GetDatabaseConnectionString(DatabaseName))
            {
                Database = {AutoTransactionsEnabled = false}
            };

        public void ResetDatabase()
        {
            lock (_writeLock)
            {
                RecycleThreadContexts();
                _storage.DeleteDatabase(DatabaseName);
            }
        }
    }
}