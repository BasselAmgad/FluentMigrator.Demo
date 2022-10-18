using FluentMigrator;
using HomeRecipes.Migrations.Migrations;

namespace HomeRecipes.Migrations.Seedings
{
    [Migration(102)]
    public class _102_RolesSeed : Migration
    {
        public override void Up()
        {
            Insert.IntoTable(TableName.Roles).Row(new
            {
                role = "Admin",
                isActive = true
            });
            Insert.IntoTable(TableName.Roles).Row(new
            {
                role = "Guest",
                isActive = true
            });
        }
        public override void Down()
        {
            throw new NotImplementedException();
        }
    }
}
