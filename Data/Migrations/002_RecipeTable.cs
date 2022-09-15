using HomeRecipes.EntityClasses;
using HomeRecipes.Migrations.Migrations;

namespace FluentMigrator.Demo.Migrations
{
    [Migration(2)]
    public class _002_RecipeTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.Recipes);
        }
        public override void Up()
        {
            Create.Table(TableName.Recipes)
                .WithColumn("id").AsGuid().NotNullable().PrimaryKey()
                .WithColumn("title").AsString().NotNullable()
                .WithColumn("ingredients").AsString().NotNullable()
                .WithColumn("instructions").AsString().NotNullable()
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true); ;
        }
    }
}
