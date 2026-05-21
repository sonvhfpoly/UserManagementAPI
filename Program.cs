using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure EF Core with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={AppDomain.CurrentDomain.BaseDirectory}app.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB created and apply migrations if any
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/users", async (ApplicationDbContext db) =>
{
    var users = await db.Users.AsNoTracking().ToListAsync();
    return Results.Ok(users);
});

app.MapGet("/api/users/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/api/users", async (User user, ApplicationDbContext db) =>
{
    if (user is null)
        return Results.BadRequest(new { error = "User payload is required." });

    var validationErrors = ValidateUser(user).ToList();
    if (validationErrors.Any())
        return Results.BadRequest(new { errors = validationErrors.Select(e => e.ErrorMessage).ToArray() });

    user.CreatedAt = DateTime.UtcNow;
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapPut("/api/users/{id:int}", async (int id, User updated, ApplicationDbContext db) =>
{
    if (updated is null)
        return Results.BadRequest(new { error = "User payload is required." });

    var validationErrors = ValidateUser(updated).ToList();
    if (validationErrors.Any())
        return Results.BadRequest(new { errors = validationErrors.Select(e => e.ErrorMessage).ToArray() });

    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Results.NotFound();

    user.FirstName = updated.FirstName;
    user.LastName = updated.LastName;
    user.Email = updated.Email;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/api/users/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Results.NotFound();

    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

static IEnumerable<ValidationResult> ValidateUser(User user)
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(user);
    Validator.TryValidateObject(user, validationContext, validationResults, true);
    return validationResults;
}

app.Run();
