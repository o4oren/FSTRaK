using FSTRaK.Models.Entity;
using FSTRaK.Models;
using System;
using System.Data.Entity.Migrations;

namespace FSTRaK.Migrations;

public partial class AddLandingFpmToFlights : DbMigration
{
    public override void Up()
    {
        using (var context = new LogbookContext())
        {
            AddColumn("Flights", "LandingFpm", c => c.Double(nullable: false, defaultValue: -1));
            var flights = context.Flights;
            foreach (var f in flights)
            {
                if (Math.Abs(f.LandingFpm - (-1)) < 0.1)
                {
                    foreach (var e in f.FlightEvents)
                    {
                        if (e is LandingEvent @event)
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