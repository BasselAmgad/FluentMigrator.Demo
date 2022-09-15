using HomeRecipes.EntityClasses;
using HomeRecipes.Migrations.Migrations;

namespace FluentMigrator.Demo.Migrations
{
    [Migration(4)]
    public class _004_RecipeCategoryTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.RecipeCategory);
        }
        public override void Up()
        {
            Create.Table(TableName.RecipeCategory)
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("recipe_id").AsInt32().NotNullable().ForeignKey(TableName.Recipes, "Id")
                .WithColumn("category_id").AsInt32().NotNullable().ForeignKey(TableName.Categories, "Id")
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }
}
