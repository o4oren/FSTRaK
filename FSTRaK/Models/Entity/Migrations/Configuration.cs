namespace FSTRaK.Migrations
{
    using FSTRaK.Models.Entity;
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
            context.Database.ExecuteSqlCommand("DROP INDEX IF EXISTS 'Aircraft_Title'");
        }
    }
}
