using System.Linq;
using FSTRaK.Models;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.Migrations
{
    using FSTRaK.Models;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Data.SQLite.EF6.Migrations;

    internal sealed class Configuration : DbMigrationsConfiguration<FSTRaK.Models.Entity.LogbookContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
            SetSqlGenerator("System.Data.SQLite", new SQLiteMigrationSqlGenerator());
        }

        protected override void Seed(FSTRaK.Models.Entity.LogbookContext context)
        {
            //  This method will be called after migrating to the latest version.

            // this is for 1.6.5 - to be removed in a future update. Once it does, versions earlier than 1.6.5 may have flights without updated landingfpm
            // update landing fpm for flights that don't have it
            var flights = context.Flights.Include(f => f.FlightEvents).ToList();
            foreach (var f in flights)
            {
                if (f.LandingFpm == null)
                {
                    foreach (var e in f.FlightEvents)
                    {
                        if (e is LandingEvent @event && ((LandingEvent)e).VerticalSpeed < 0)
                        {
                            f.LandingFpm = @event.VerticalSpeed;
                        }
                    }
                    context.SaveChanges();
                }
            }

        }
    }
}
