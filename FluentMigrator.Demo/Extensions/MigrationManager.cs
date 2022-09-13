using FluentMigrator.Demo.Migrations;
using FluentMigrator.Runner;

namespace FluentMigrator.Demo.Extensions
{
    public static class MigrationManager
    {
        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetRequiredService<Database>();
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                try
                {
                    databaseService.CreateDatabase("HomeRecipes");
                    migrationService.ListMigrations();
                    migrationService.MigrateUp();
                }
                catch
                {
                    //log errors or ...
                    throw;
                }
            }
            return host;
        }
    }
}
