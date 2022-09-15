using HomeRecipes.EntityClasses;
using HomeRecipes.Migrations.Migrations;

namespace FluentMigrator.Demo.Migrations
{
    [Migration(3)]
    public class _003_CategoryTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.Categories);
        }
        public override void Up()
        {
            Create.Table(TableName.Categories)
                .WithColumn("name").AsString(30).PrimaryKey()
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true); ;
        }
    }
}
