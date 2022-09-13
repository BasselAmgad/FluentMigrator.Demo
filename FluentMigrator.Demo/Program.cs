using FluentMigrator.Demo.Context;
using FluentMigrator.Demo.Extensions;
using FluentMigrator.Demo.Migrations;
using FluentMigrator.Runner;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddSingleton<Database>();
builder.Services.AddLogging(c => c.AddFluentMigratorConsole())
        .AddFluentMigratorCore()
        .ConfigureRunner(c => c.AddSqlServer2012()
            .WithGlobalConnectionString(builder.Configuration.GetConnectionString("SqlConnection"))
            .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations());

var app = builder.Build();

app.MigrateDatabase();
app.Run();
