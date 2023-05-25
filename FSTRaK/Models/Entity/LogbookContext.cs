


using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration.Conventions;


namespace FSTRaK.Models.Entity
{
    internal class LogbookContext : DbContext
    {

        public LogbookContext() : base("FSTrAkCompactDatabase")
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<LogbookContext, Migrations.Configuration>());
        }

        public DbSet<Flight> Flights { get; set; }
        public DbSet<FlightEvent> FlightEvents { get; set; }
        public DbSet<Aircraft> Aircraft { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

        }
    }
}
