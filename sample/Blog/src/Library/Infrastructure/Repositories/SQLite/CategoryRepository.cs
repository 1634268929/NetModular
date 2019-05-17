using Nm.Lib.Data.Abstractions;

namespace Nm.Module.Blog.Infrastructure.Repositories.SQLite
{
    public class CategoryRepository : SqlServer.CategoryRepository
    {
        public CategoryRepository(IDbContext dbContext) : base(dbContext)
        {
        }
    }
}
