using Microsoft.EntityFrameworkCore;

namespace NoteEz_Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) 
        { 
        }



    }
}
