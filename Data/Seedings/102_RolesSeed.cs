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
                id = new Guid("a0798c89-aa12-455b-8998-b7032dac2fb1"),
                role = "Admin",
                is_active = true
            });
            Insert.IntoTable(TableName.Roles).Row(new
            {
                id = new Guid("8f4fd2df-c7b6-48e1-a8a7-6f87ce74d86c"),
                role = "Guest",
                is_active = true
            });
        }
        public override void Down()
        {
            throw new NotImplementedException();
        }
    }
}
