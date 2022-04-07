using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pisstaube.Core.Database.Models;

// https://stackoverflow.com/questions/36208580/what-happened-to-addorupdate-in-ef-7-core
namespace Pisstaube.Core.Database
{
    public static class DbSetExtension
    {
        public static void AddOrUpdate(this DbSet<BeatmapSet> dbSet, BeatmapSet data)
        {
            var context = dbSet.GetContext();

            var entities = dbSet.AsNoTracking().Where(set => set.SetId == data.SetId);
   
            var dbVal = entities.FirstOrDefault();
            if (dbVal != null)
            {
                context.Entry(dbVal).CurrentValues.SetValues(data);
                context.Entry(dbVal).State = EntityState.Modified;
                return;
            }

            dbSet.Add(data);
        }
    }
    
    
    public static class HackyDbSetGetContextTrick
    {
        public static DbContext GetContext<TEntity>(this DbSet<TEntity> dbSet)
            where TEntity : class
        {
            return (DbContext) dbSet
                .GetType().GetTypeInfo()
                .GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(dbSet);
        }
    }
}
