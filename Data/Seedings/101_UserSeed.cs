using FluentMigrator;
using HomeRecipes.Migrations.Migrations;
using Microsoft.AspNetCore.Identity;

namespace HomeRecipes.Migrations.Seedings
{
    [Migration(101)]
    public class _101_UserSeed : Migration
    {
        public record User
        {
            public Guid Id { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string RefreshToken { get; set; }
            public bool IsActive { get; set; }
        }

        public static PasswordHasher<User> hasher = new();

        public static List<User> users = new()
        {
            new User
            {
                Id = new Guid("3d50a2e1-b8da-4616-a295-911798894905"),
                Username = "Bassel",
                Password = hasher.HashPassword(new User(), "Password123"),
                IsActive = true,
                RefreshToken = "",
            },
            new User
            {
                Id =  new Guid("deb81ad5-e200-4acf-af11-96691522e944"),
                Username = "Omar",
                Password = hasher.HashPassword(new User(), "Password123"),
                IsActive = true,
                RefreshToken = "",
            },
            new User
            {
                Id =  new Guid("695b29cd-007b-4998-8b83-b63578d8f473"),
                Username = "Walid",
                Password = hasher.HashPassword(new User(), "Password123"),
                IsActive = true,
                RefreshToken = "",
            },
            new User
            {
                Id = new Guid("6ad70b13-dfbd-4293-b1cb-60f046096068"),
                Username = "admin@admin.com",
                Password = hasher.HashPassword(new User(), "Aa12345!"),
                IsActive = true,
                RefreshToken = "",
            }
        };

        public override void Up()
        {
            foreach (var u in users)
            {
                Insert.IntoTable(TableName.Users)
                    .Row(new
                    {
                        id = u.Id,
                        username = u.Username,
                        password = u.Password,
                        is_active = u.IsActive,
                        refreshToken = u.RefreshToken,
                    });
            }
        }

        public override void Down()
        {
        }
    }
}
