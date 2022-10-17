using FluentMigrator;

namespace HomeRecipes.Migrations.Migrations
{
    [Migration(6)]
    public class _006_UserRolesTable : Migration
    {
        public override void Down()
        {
            Delete.Table(TableName.UserRoles);
        }
        public override void Up()
        {
            Create.Table(TableName.Roles)
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("user_id").AsGuid().NotNullable().ForeignKey(TableName.Users, "id")
                .WithColumn("role_id").AsGuid().NotNullable().ForeignKey(TableName.Roles, "id")
                .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true);
        }

    }
}
