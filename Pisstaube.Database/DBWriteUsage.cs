using System;
using System.Collections.Generic;

namespace Pisstaube.Database
{
    public class DBWriteUsage : IDisposable
    {
        public readonly PisstaubeDbContext Context;
        private readonly Action<DBWriteUsage> usageCompleted;

        public DBWriteUsage(PisstaubeDbContext context, Action<DBWriteUsage> onCompleted)
        {
            Context = context;
            usageCompleted = onCompleted;
        }

        public bool PerformedWrite { get; private set; }

        private bool isDisposed;
        public List<Exception> Errors = new List<Exception>();
        
        public bool IsTransactionLeader = false;

        protected void Dispose(bool disposing)
        {
            if (isDisposed) return;

            isDisposed = true;

            try
            {
                PerformedWrite |= Context.SaveChanges() > 0;
            }
            catch (Exception e)
            {
                Errors.Add(e);
                throw;
            }
            finally
            {
                usageCompleted?.Invoke(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DBWriteUsage()
        {
            Dispose(false);
        }
    }
}