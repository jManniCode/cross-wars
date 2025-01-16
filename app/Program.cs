using Microsoft.AspNetCore.Builder;
using app;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;
using System.Collections.Generic;

// Initialize the database connection
Database database = new();
NpgsqlDataSource db = database.Connection();

var builder = WebApplication.CreateBuilder(args);

// Lägg till tjänster
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Prioritera mainPage.html som standardfil
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "mainPage.html" }
});
app.UseStaticFiles(); // Serve static files

// Middleware för cookies
app.Use(async (context, next) =>
{
    const string clientIdCookieName = "ClientId";

    if (!context.Request.Cookies.TryGetValue(clientIdCookieName, out var clientId))
    {
        clientId = GenerateUniqueClientId();
        context.Response.Cookies.Append(clientIdCookieName, clientId, new CookieOptions
        {
            HttpOnly = true,
            Secure = app.Environment.IsDevelopment() ? false : true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
    }
    await next();
});

// Endast aktivera Swagger i utvecklingsmiljön
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

Actions actions = new(app, db);

app.UseHttpsRedirection();
app.Run();

// Helper-metod för att generera en unik klient-ID
static string GenerateUniqueClientId()
{
    using var rng = RandomNumberGenerator.Create();
    var bytes = new byte[16];
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}