using Microsoft.EntityFrameworkCore;

namespace NOF.Test;

public static class TaskExtensions
{
    extension(Task task)
    {
        public async Task ThenSaveChanges(DbContext dbContext)
        {
            await task;
            await dbContext.SaveChangesAsync();
        }
    }

    extension<T>(Task<T> task)
    {
        public async Task<T> ThenSaveChanges(DbContext dbContext)
        {
            var result = await task;
            await dbContext.SaveChangesAsync();
            return result;
        }
    }
}
