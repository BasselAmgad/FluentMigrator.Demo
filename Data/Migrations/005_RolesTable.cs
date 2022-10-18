using FluentMigrator;

namespace HomeRecipes.Migrations.Migrations
{
    [Migration(5)]
    public class _005_RolesTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.Roles);
        }
        public override void Up()
        {
            Create.Table(TableName.Roles)
                .WithColumn("id").AsGuid().NotNullable().PrimaryKey()
                .WithColumn("role").AsString().NotNullable()
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }
}
