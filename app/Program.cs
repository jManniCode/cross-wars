using Microsoft.AspNetCore.Builder;
using app;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;

// Initialize the database connection
Database database = new();
NpgsqlDataSource db = database.Connection();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseDefaultFiles(); // Serve default files like index.html
app.UseStaticFiles();  // Serve static files (CSS, JS, etc.)
// Middleware to set cookies
app.Use(async (context, next) =>
{
    const string clientIdCookieName = "ClientId";

    if (!context.Request.Cookies.TryGetValue(clientIdCookieName, out var clientId))
    {
        clientId = GenerateUniqueClientId();
        context.Response.Cookies.Append(clientIdCookieName, clientId, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
    }
    await next();
});

// Initialize the Actions class and pass the database connection
Actions actions = new(app, db);





app.UseHttpsRedirection();
app.Run();

// Helper function to generate a unique client ID
static string GenerateUniqueClientId()
{
    using var rng = RandomNumberGenerator.Create();
    var bytes = new byte[16];
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}