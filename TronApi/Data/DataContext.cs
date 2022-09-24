using Microsoft.EntityFrameworkCore;

namespace TronApi.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base (options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<UserStats> UsersStats { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<MasterItemsTable> MasterItemsTables { get; set; }
        public DbSet<UserInventory> UserInventories { get; set; }

    }
}
