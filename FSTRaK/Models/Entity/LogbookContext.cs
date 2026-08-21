


using Serilog;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;


namespace FSTRaK.Models.Entity
{
    [DbConfigurationType(typeof(SQLiteConfiguration))]
    public class LogbookContext : DbContext
    {
        public LogbookContext() : base("FSTrAkSqliteDatabase")
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<LogbookContext, Migrations.Configuration>(true));
            this.Database.Log = Log.Debug;
            this.Configuration.LazyLoadingEnabled = true;
            this.Configuration.ProxyCreationEnabled = true;
        }

        public DbSet<Flight> Flights { get; set; }
        public DbSet<BaseFlightEvent> FlightEvents { get; set; }
        public DbSet<Aircraft> Aircraft { get; set; }
        public DbSet<FlightPlan> FlightPlans { get; set; }
        public DbSet<FlightPlanPoint> FlightPlanPoints { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Entity<Flight>()
                .HasMany(e => e.FlightEvents)
                .WithRequired(e => e.Flight)
                .HasForeignKey(e => e.FlightId);
            modelBuilder.Entity<Flight>()
                .HasOptional(f => f.FlightPlan)
                .WithRequired(p => p.Flight)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<FlightPlan>()
                .HasMany(p => p.Points)
                .WithRequired(pt => pt.FlightPlan)
                .HasForeignKey(pt => pt.FlightPlanId)
                .WillCascadeOnDelete(true);
        }
    }
}
