using HomeRecipes_UserRoles_v1.DatabaseSpecific;
using HomeRecipes_UserRoles_v1.EntityClasses;
using HomeRecipes_UserRoles_v1.Linq;
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
    // Create User entity
    var userEntity = new UserEntity
    {
        Id = Guid.NewGuid(),
        Username = newUser.UserName,
        Password = hasher.HashPassword(newUser, newUser.Password),
        RefreshToken = "",
        IsActive = true,
    };
    await adapter.SaveEntityAsync(userEntity);
    // Add Guest role to user
    var roleId = await metaData.Role.FirstOrDefaultAsync(r => r.RoleName == "Guest");
    var roleEntity = new UserRoleEntity { UserId = userEntity.Id, RoleId = roleId.Id };
    await adapter.SaveEntityAsync(roleEntity);
    return Results.Ok(userEntity);
});

app.MapPost("/login", [AllowAnonymous] async (DataAccessAdapter adapter, User user) =>
{
    var passwordHasher = new PasswordHasher<UserEntity>();
    var metaData = new LinqMetaData(adapter);
    var userData = await metaData.User.FirstOrDefaultAsync(u => u.Username == user.UserName);
    if (userData is null)
        return Results.NotFound("User does not exist");
    var verifyPassword = passwordHasher.VerifyHashedPassword(userData, userData.Password, user.Password);
    if (verifyPassword == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    // Get role of the user
    var userRoleEntity = new UserRoleEntity{UserId = userData.Id};
    var userRolesIds = await metaData.UserRole.FirstOrDefaultAsync(u => u.UserId == userData.Id);
    var RoleEntity = await metaData.Role.FirstOrDefaultAsync(r => r.Id == userRolesIds.RoleId);
    var userRole = "Guest";
    if (RoleEntity != null)
        userRole = RoleEntity.RoleName;   
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
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role,userRole),
            }),
        Expires = DateTime.UtcNow.AddHours(6),
        Audience = audience,
        Issuer = issuer,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
    };
    var token = jwtTokenHandler.CreateToken(tokenDescriptor);
    var jwtToken = jwtTokenHandler.WriteToken(token);
    return Results.Ok(new AuthenticatedResponse { RefreshToken = "", Token = jwtToken, UserName = userData.Username, UserRole = userRole });
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

app.MapGet("/users",[Authorize(Roles = "Admin")] async (DataAccessAdapter adapter) =>
{
    var metaData = new LinqMetaData(adapter);
    var userEntities = await metaData.User.ToListAsync();
    var userRolesEntitites = await metaData.UserRole.ToListAsync();
    var rolesEntities = await metaData.Role.ToListAsync();
    if (rolesEntities is null) return Results.BadRequest("Could not fetch roles from DB");
    var usersList = new List<User>();
    var user1 = new List<string>();
    foreach (var user in userEntities)
    {
        var userDTO = new User(user.Id.ToString(), "");
        var userRolesEntity = userRolesEntitites.FirstOrDefault(u => u.UserId == user.Id);
        if (userRolesEntity is null)
        {
            userDTO.UserRoles.Add("Guest");
        }
        else
        {
            var userRoles = rolesEntities.Where(r => r.Id == userRolesEntity.RoleId).ToList();
            foreach (var role in userRoles)
                userDTO.UserRoles.Add(role.RoleName);
        }
        usersList.Add(userDTO);
    }
    return Results.Ok(usersList);
});

app.MapPut("/users", [Authorize(Roles = "Admin")] async (DataAccessAdapter adapter, Guid userId, string newRole) =>
{
    return Results.NoContent();
});

app.MapGet("/recipes/{id}", [Authorize] async (DataAccessAdapter adapter, Guid id) =>
{
    var metaData = new LinqMetaData(adapter);
    var recipeEntity = await metaData.Recipe.FirstOrDefaultAsync(r => r.Id == id);
    if (recipeEntity is null)
        return Results.NotFound("Couldn't find the requested recipe.");
    var recipe = new Recipe
    {
        Id = recipeEntity.Id,
        Title = recipeEntity.Title,
        Ingredients = recipeEntity.Ingredients,
        Instructions = recipeEntity.Instructions,
        Categories = new(),
    };
    var currentRecipeCategories = await metaData.RecipeCategory.Where(r => r.RecipeId == recipe.Id).ToListAsync();
    foreach (var category in currentRecipeCategories)
    {
        // Get the category name from the category table and add it to the current recipe
        var currentCategory = await metaData.Category.FirstOrDefaultAsync(c => c.Id == category.CategoryId);
        if (currentCategory is not null)
            recipe.Categories.Add(currentCategory.Name);
    }
    return Results.Ok(recipe);
});

app.MapPost("/recipes", [Authorize(Roles = "admin")] async (DataAccessAdapter adapter, Recipe recipe) =>
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
        var categoryExistsInDb = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
        if (categoryExistsInDb == null)
            return Results.BadRequest($"The `{category}` category is not in the DB please add it before inserting it into the recipe.");
        var categoryEntity = new CategoryEntity { Id = Guid.NewGuid(), Name = category };
        await adapter.SaveEntityAsync(categoryEntity);
        var recipeCategory = new RecipeCategoryEntity { RecipeId = newRecipeEntity.Id, CategoryId = categoryEntity.Id };
        await adapter.SaveEntityAsync(recipeCategory);
    }
    return Results.Ok();
});

app.MapPut("/recipes/{id}", [Authorize] async (DataAccessAdapter adapter, Guid id, Recipe newRecipe) =>
{
    var metaData = new LinqMetaData(adapter);
    var recipeCategory = await metaData.RecipeCategory.Where(r => r.RecipeId == id).ToListAsync();
    var recipeEntity = new RecipeEntity
    {
        Id = newRecipe.Id,
        Title = newRecipe.Title,
        Ingredients = newRecipe.Ingredients,
        Instructions = newRecipe.Instructions,
        IsNew = false
    };
    foreach (var category in newRecipe.Categories)
    {
        var categoryExistsInDb = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
        // Check if the category exists in the DB
        if (categoryExistsInDb == null)
            return Results.BadRequest($"The `{category}` category is not in the DB please add it before inserting it into the recipe.");
        // Check if the category is already in the recipe
        var categoryExistsInRecipe = recipeCategory.FirstOrDefault(r => r.CategoryId == categoryExistsInDb.Id);
        if (categoryExistsInRecipe == null)
            adapter.SaveEntity(new RecipeCategoryEntity { RecipeId = id, CategoryId = categoryExistsInDb.Id });
    }
    foreach(var elem in recipeCategory)
    {
        var categoryEntity = await metaData.Category.FirstOrDefaultAsync(c => c.Id == elem.CategoryId);
        if(categoryEntity == null)
        {
            await adapter.DeleteEntityAsync(elem);
            return Results.BadRequest($"The with id `{elem.CategoryId}` could not be found in the DB so it was removed.");
        }
        if (!newRecipe.Categories.Contains(categoryEntity.Name))
            await adapter.DeleteEntityAsync(categoryEntity);
    }
    await adapter.SaveEntityAsync(recipeEntity);
    return Results.Ok($"Recipe `{newRecipe.Title}` updated successfully");
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
    var categoryData = await metaData.Category.FirstOrDefaultAsync(c => c.Name == category);
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
        return Results.NotFound($"Category {category} does not exist in the DB.");
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
    var recipeCategoryExists = await metaData.RecipeCategory
    .FirstOrDefaultAsync(r => r.RecipeId == id && r.CategoryId == categoryEntity.Id);
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

