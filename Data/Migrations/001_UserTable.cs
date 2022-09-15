using HomeRecipes.Migrations.Migrations;

namespace FluentMigrator.Demo.Migrations
{
    [Migration(1)]
    public class _001_UserTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.Users);
        }
        public override void Up()
        {
            Create.Table(TableName.Users)
                .WithColumn("id").AsGuid().NotNullable().PrimaryKey()
                .WithColumn("username").AsString().NotNullable()
                .WithColumn("password").AsString().NotNullable()
                .WithColumn("refreshToken").AsString()
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true); ;
        }
    }
}
