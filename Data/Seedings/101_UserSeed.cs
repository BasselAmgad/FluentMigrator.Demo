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
                Id = Guid.NewGuid(),
                Username = "Bassel",
                Password = hasher.HashPassword(new User(), "p@ssword"),
                IsActive = true,
                RefreshToken = null,
            },
            new User
            {
                Id = Guid.NewGuid(),
                Username = "Omar",
                Password = hasher.HashPassword(new User(), "p@ssword"),
                IsActive = true,
                RefreshToken = null,
            },
            new User
            {
                Id = Guid.NewGuid(),
                Username = "Walid",
                Password = hasher.HashPassword(new User(), "p@ssword"),
                IsActive = true,
                RefreshToken = null,
            }
        };

        public override void Up()
        {
            foreach (var u in users)
            {
                Insert.IntoTable(TableName.Users)
                    .Row(new
                    {
                        username = u.Username,
                        password = u.Password,
                        is_active = u.IsActive,
                        refresh_token = u.RefreshToken,
                    });
            }
        }

        public override void Down()
        {
        }
    }
}
