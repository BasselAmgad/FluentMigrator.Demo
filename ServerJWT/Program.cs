using HomeRecipesCode.DatabaseSpecific;
using HomeRecipesCode.EntityClasses;
using HomeRecipesCode.FactoryClasses;
using HomeRecipesCode.HelperClasses;
using HomeRecipesCode.Linq;
using HomeRecipesCode.RelationClasses;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SD.LLBLGen.Pro.DQE.SqlServer;
using SD.LLBLGen.Pro.LinqSupportClasses;
using SD.LLBLGen.Pro.ORMSupportClasses;
using Server.Models;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure the DQE
RuntimeConfiguration.ConfigureDQE<SQLServerDQEConfiguration>(
                                c => c.SetTraceLevel(TraceLevel.Verbose)
                                        .AddDbProviderFactory(typeof(System.Data.SqlClient.SqlClientFactory))
                                        .SetDefaultCompatibilityLevel(SqlServerCompatibilityLevel.SqlServer2012));
// Configure tracers
RuntimeConfiguration.Tracing
                        .SetTraceLevel("ORMPersistenceExecution", TraceLevel.Info)
                        .SetTraceLevel("ORMPlainSQLQueryExecution", TraceLevel.Info);
// Configure entity related settings
RuntimeConfiguration.Entity
                        .SetMarkSavedEntitiesAsFetched(true);

var sqlConnectionString = builder.Configuration.GetConnectionString("SqlConnection");
var securityScheme = new OpenApiSecurityScheme()
{
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Home Recipes API secured with JWT",
};

var securityReq = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        new string[] {}
    }
};

var contact = new OpenApiContact()
{
    Name = "Bassel Amgad",
    Email = "bamgad7@gmail.com",
    Url = new Uri("https://github.com/BasselAmgad")
};

var info = new OpenApiInfo()
{
    Version = "v1",
    Title = "Home Recipes API secured with JWT",
    Description = "Implementing JWT Authentication in Minimal API",
    Contact = contact,
};

// Add services to the container.
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", info);
    o.AddSecurityDefinition("Bearer", securityScheme);
    o.AddSecurityRequirement(securityReq);
});

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey
            (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "Client",
                      policy =>
                      {
                          policy.WithOrigins(builder.Configuration["ClientUrl"], builder.Configuration["DeployedClient"])
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                      });
});

builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddSingleton<Data>();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("Client");

app.MapPost("/register", async ([FromBody] User newUser) =>
{
    using (DataAccessAdapter adapter = new(sqlConnectionString))
    {
        var metaData = new LinqMetaData(adapter);
        var user = await metaData.User.FirstOrDefaultAsync(u => u.Username == newUser.UserName);
        if (user != null)
            return Results.BadRequest("username already exists");
        var hasher = new PasswordHasher<User>();
        var userEntity = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = newUser.UserName,
            Password = hasher.HashPassword(newUser, newUser.Password),
            RefreshToken = "",
            IsActive = true,
        };
        await adapter.SaveEntityAsync(userEntity);
        return Results.Ok(userEntity);
    };
});

app.MapPost("/login", [AllowAnonymous] async (User user) =>
{
    using (DataAccessAdapter adapter = new(sqlConnectionString))
    {
        var passwordHasher = new PasswordHasher<UserEntity>();
        var metaData = new LinqMetaData(adapter);
        var usersList = await metaData.User.ToListAsync();
        if (usersList is null)
            throw new Exception("Could not deserialize users list");
        var userData = usersList.FirstOrDefault(u => u.Username == user.UserName);
        if (userData is null)
            return Results.NotFound("User does not exist");
        var verifyPassword = passwordHasher.VerifyHashedPassword(userData, userData.Password, user.Password);
        if (verifyPassword == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jwtTokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", userData.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, userData.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            // the life span of the token needs to be shorter and utilise refresh token to keep the user signedin
            // but since this is a demo app we can extend it to fit our current need
            Expires = DateTime.UtcNow.AddHours(6),
            Audience = audience,
            Issuer = issuer,
            // here we are adding the encryption alogorithim information which will be used to decrypt our token
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
        };
        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);
        return Results.Ok(new AuthenticatedResponse { RefreshToken = "", Token = jwtToken, UserName = userData.Username });
    };
});

/*app.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("X-XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false });
});*/

app.MapGet("/recipes", [Authorize] async (Data data) =>
{
    using(DataAccessAdapter adapter = new(sqlConnectionString))
    {
        var metaData = new LinqMetaData(adapter);
        var recipes = await metaData.Recipe.ToListAsync();
        if(recipes is null)
            return Results.NotFound();
        return Results.Ok(recipes);
    }
});

app.MapGet("/recipes/{id}", [Authorize] async (Data data, Guid id) =>
{
    using (DataAccessAdapter adapter = new(sqlConnectionString))
    {
        var metaData = new LinqMetaData(adapter);
        var recipes = await metaData.Recipe.FirstOrDefaultAsync(r => r.Id == id);
        if (recipes is null)
            return Results.NotFound();
        return Results.Ok(recipes);
    }
});

app.MapPost("/recipes", [Authorize] async (Data data, Recipe recipe) =>
{
    using (DataAccessAdapter adapter = new(sqlConnectionString))
    {
        var metaData = new LinqMetaData(adapter);
        var newRecipeEntity = new RecipeEntity
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Ingredients = recipe.Ingredients,
            Instructions = recipe.Instructions
        };
        await adapter.SaveEntityAsync(newRecipeEntity);
        foreach(var category in recipe.Categories)
        {
            var categoryEntity = new CategoryEntity { Id = Guid.NewGuid(), Name = category };
            await adapter.SaveEntityAsync(categoryEntity);
            var recipeCategory = new RecipeCategoryEntity { Id = Guid.NewGuid(), RecipeId = newRecipeEntity.Id, CategoryId = categoryEntity.Id};
            await adapter.SaveEntityAsync(recipeCategory);
        }
        return Results.Ok();
    }
});

app.MapPut("/recipes/{id}", [Authorize] async (Data data, Guid id, Recipe newRecipe) =>
{
    try
    {
        var updatedRecipe = await data.EditRecipeAsync(id, newRecipe);
        return Results.Ok(updatedRecipe);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }

});

app.MapDelete("/recipes/{id}", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, Guid id) =>
{
    try
    {
        await data.RemoveRecipeAsync(id);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }

});

app.MapGet("/categories", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context) =>
{
    try
    {
        var categories = await data.GetCategoriesAsync();
        return Results.Ok(categories);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});

app.MapPost("/categories", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, string category) =>
{
    try
    {
        await data.AddCategoryAsync(category);
        return Results.Created($"/categories/{category}", category);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});

app.MapPut("/categories", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, string category, string newCategory) =>
{
    try
    {
        await data.EditCategoryAsync(category, newCategory);
        return Results.Ok($"Category ({category}) updated to ({newCategory})");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});

app.MapDelete("/categories", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, string category) =>
{
    try
    {
        await data.RemoveCategoryAsync(category);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});

app.MapPost("/recipes/category", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, Guid id, string category) =>
{
    try
    {
        await data.AddCategoryToRecipeAsync(id, category);
        return Results.Created($"recipes/category/{category}", category);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});

app.MapDelete("/recipes/category", [Authorize] async (Data data, IAntiforgery antiforgery, HttpContext context, Guid id, string category) =>
{
    try
    {
        await data.RemoveCategoryFromRecipeAsync(id, category);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex?.Message ?? string.Empty);
    }
});





app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

