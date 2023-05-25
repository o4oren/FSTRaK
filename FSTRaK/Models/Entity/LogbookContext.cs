


using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Data.SQLite.EF6.Migrations;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.Models.Entity
{
    internal class LogbookContext : DbContext
    {
        static LogbookContext()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<LogbookContext, ContextMigrationConfiguration>(true));

        }
        public LogbookContext() : base("FSTrAkDatabase")
        {
            this.Configuration.ProxyCreationEnabled = true;
            this.Configuration.LazyLoadingEnabled = true;
        }

        public DbSet<Flight> Flights { get; set; }
        public DbSet<FlightEvent> FlightEvents { get; set; }
        public DbSet<Aircraft> Aircraft { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Entity<Flight>()
        .HasMany(f => f.FlightEvents);
        }
    }

    internal sealed class ContextMigrationConfiguration : DbMigrationsConfiguration<LogbookContext>
    {
        public ContextMigrationConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
            SetSqlGenerator("System.Data.SQLite", new SQLiteMigrationSqlGenerator());
        }
    }
}
