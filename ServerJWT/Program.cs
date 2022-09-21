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
using SD.LLBLGen.Pro.QuerySpec.Adapter;
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
builder.Services.AddTransient(a => new DataAccessAdapter(sqlConnectionString));
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("Client");

app.MapPost("/register", async (DataAccessAdapter adapter, [FromBody] User newUser) =>
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
});

app.MapPost("/login", [AllowAnonymous] async (DataAccessAdapter adapter, User user) =>
{
    var passwordHasher = new PasswordHasher<UserEntity>();
    var metaData = new LinqMetaData(adapter);
    var usersList = await metaData.User.ToListAsync();
    if (usersList is null)
        throw new Exception("Could not fetch users list from DB");
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
});

app.MapGet("/recipes", [Authorize] async (DataAccessAdapter adapter) =>
{
    var metaData = new LinqMetaData(adapter);
    // Fetch recipes, categories, recipeCategory tables
    var recipesEntityList = await metaData.Recipe.ToListAsync();
    var categoriesEntityList = await metaData.Category.ToListAsync();
    var recipeCategory = await metaData.RecipeCategory.ToListAsync();
    if (recipesEntityList is null)
        return Results.NotFound();
    var recipesList = new List<Recipe>();
    foreach (var recipe in recipesEntityList)
    {
        var currentRecipe = new Recipe 
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Ingredients = recipe.Ingredients,
            Instructions = recipe.Instructions,
            Categories = new(),
        };
        // Get all the ids of the category entities for the current recipe
        var currentRecipeCategories = recipeCategory.Where(r => r.RecipeId == currentRecipe.Id).ToList();
        foreach(var category in currentRecipeCategories)
        {
            // Get the category name from the category table and add it to the current recipe
            var currentCategory = categoriesEntityList.FirstOrDefault(c => c.Id == category.CategoryId);
            if(currentCategory is not null)
                currentRecipe.Categories.Add(currentCategory.Name);
        }
        recipesList.Add(currentRecipe);
    }
    return Results.Ok(recipesList);
});

app.MapGet("/recipes/{id}", [Authorize] async (DataAccessAdapter adapter, Guid id) =>
{
    var metaData = new LinqMetaData(adapter);
    var recipeEntity = await metaData.Recipe.FirstOrDefaultAsync(r => r.Id == id);
    if (recipeEntity is null)
        return Results.NotFound("Couldn't find the requested recipe.");
    var categoriesEntityList = await metaData.Category.ToListAsync();
    var recipeCategory = await metaData.RecipeCategory.ToListAsync();
    var recipe = new Recipe
    {
        Id = recipeEntity.Id,
        Title = recipeEntity.Title,
        Ingredients = recipeEntity.Ingredients,
        Instructions = recipeEntity.Instructions,
        Categories = new(),
    };
    var currentRecipeCategories = recipeCategory.Where(r => r.RecipeId == recipe.Id).ToList();
    foreach (var category in currentRecipeCategories)
    {
        // Get the category name from the category table and add it to the current recipe
        var currentCategory = categoriesEntityList.FirstOrDefault(c => c.Id == category.CategoryId);
        if (currentCategory is not null)
            recipe.Categories.Add(currentCategory.Name);
    }
    return Results.Ok(recipe);
});

app.MapPost("/recipes", [Authorize] async (DataAccessAdapter adapter, Recipe recipe) =>
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
    foreach (var category in recipe.Categories)
    {
        var categoryEntity = new CategoryEntity { Id = Guid.NewGuid(), Name = category };
        await adapter.SaveEntityAsync(categoryEntity);
        var recipeCategory = new RecipeCategoryEntity { RecipeId = newRecipeEntity.Id, CategoryId = categoryEntity.Id };
        await adapter.SaveEntityAsync(recipeCategory);
    }
    return Results.Ok();
});

app.MapPut("/recipes/{id}", [Authorize] async (DataAccessAdapter adapter, Guid id, Recipe newRecipe) =>
{
    //TODO
});

app.MapDelete("/recipes/{id}", [Authorize] async (DataAccessAdapter adapter, Guid id) =>
{
    var recipeEntity = new RecipeEntity { Id = id };
    await adapter.DeleteEntityAsync(recipeEntity);
    return Results.Ok("Entity Deleted");
});

app.MapGet("/categories", [Authorize] async (DataAccessAdapter adapter) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoriesEntityList = await metaData.Category.ToListAsync();
    var categoriesList = new List<string>();
    if (categoriesEntityList is null)
        return Results.NotFound();
    foreach (var category in categoriesEntityList)
        categoriesList.Add(category.Name);
    return Results.Ok(categoriesList);
});

app.MapPost("/categories", [Authorize] async (DataAccessAdapter adapter, string category) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoryList = await metaData.Category.ToListAsync();
    var categoryData = categoryList.FirstOrDefault(c => c.Name == category);
    if (categoryData is not null)
        return Results.BadRequest("Category already exists");
    var categoryEntity = new CategoryEntity { Id = Guid.NewGuid(), Name = category };
    await adapter.SaveEntityAsync(categoryEntity);
    return Results.Ok($"New category added: {category}");
});

app.MapPut("/categories", [Authorize] async (DataAccessAdapter adapter, string category, string newCategory) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoryEntity = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
    if (categoryEntity is null)
        return Results.NotFound();
    categoryEntity.Name = newCategory;
    await adapter.SaveEntityAsync(categoryEntity);
    return Results.Ok($"`{category}` updated to `{newCategory}`");
});

app.MapDelete("/categories", [Authorize] async (DataAccessAdapter adapter, string category) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoryEntity = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
    if (categoryEntity is null)
        return Results.NotFound();
    await adapter.DeleteEntityAsync(categoryEntity);
    return Results.Ok();
});

app.MapPost("/recipes/category", [Authorize] async (DataAccessAdapter adapter, Guid id, string category) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoryEntity = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
    if (categoryEntity is null) return Results.NotFound("Category not found");
    var recipeEntity = await metaData.Recipe.FirstOrDefaultAsync(r => r.Id == id);
    if (recipeEntity is null) return Results.NotFound("Recipe not found");
    var recipeCategoryExists = metaData.RecipeCategory
    .FirstOrDefault(r => r.RecipeId == id && r.CategoryId == categoryEntity.Id);
    if (recipeCategoryExists is not null) return Results.BadRequest("Category already exists");
    var recipeCategoryEntity = new RecipeCategoryEntity
    {
        RecipeId = id,
        CategoryId = categoryEntity.Id
    };
    await adapter.SaveEntityAsync(recipeCategoryEntity);
    return Results.Ok();
});

app.MapDelete("/recipes/category", [Authorize] async (DataAccessAdapter adapter, Guid id, string category) =>
{
    var metaData = new LinqMetaData(adapter);
    var categoryEntity = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
    if (categoryEntity is null) return Results.NotFound("Category not found");
    var recipeEntity = await metaData.Recipe.FirstOrDefaultAsync(r => r.Id == id);
    if (recipeEntity is null) return Results.NotFound("Recipe not found");
    var recipeCategoryEntity = await metaData.RecipeCategory.FirstOrDefaultAsync(r => r.RecipeId == id && r.CategoryId == categoryEntity.Id);
    if (recipeCategoryEntity is null) return Results.NotFound("This category is not in the recipe");
    await adapter.DeleteEntityAsync(recipeCategoryEntity);
    return Results.Ok();
});

/*app.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append("X-XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false });
});*/



app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

