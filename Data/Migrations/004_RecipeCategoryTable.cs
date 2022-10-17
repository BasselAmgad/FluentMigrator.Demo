using FluentMigrator;

namespace HomeRecipes.Migrations.Migrations
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
                .WithColumn("recipe_id").AsGuid().NotNullable().ForeignKey(TableName.Recipes, "id")
                .WithColumn("category_id").AsGuid().NotNullable().ForeignKey(TableName.Categories, "id")
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }
}
