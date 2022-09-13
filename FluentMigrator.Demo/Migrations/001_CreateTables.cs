namespace FluentMigrator.Demo.Migrations
{
    [Migration(1)]
    public class _001_CreateTables : Migration
    {
        public override void Down()
        {
            Delete.Table("Recipes");
            Delete.Table("Categories");
            Delete.Table("Users");
        }
        public override void Up()
        {
            Create.Table("Recipe")
                .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
                .WithColumn("Title").AsString().NotNullable()
                .WithColumn("Instructions").AsString().NotNullable();
            Create.Table("Categories")
                .WithColumn("Name").AsString(30).PrimaryKey();
            Create.Table("Users")
                .WithColumn("Username").AsString().PrimaryKey()
                .WithColumn("Password").AsString().NotNullable()
                .WithColumn("RefreshToken").AsString();
            Create.Table("RecipeCategoryDictionary")
                .WithColumn("RecipeId").AsGuid().PrimaryKey().ForeignKey("Recipes", "Id")
                .WithColumn("CategoryName").AsString(30).PrimaryKey().ForeignKey("Categories", "Name");
        }
    }
}
