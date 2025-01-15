using System.Security.Cryptography;
using CrossWars;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Serve static files from wwwroot
app.UseDefaultFiles(); // Serving index.html as the default file
app.UseStaticFiles(); // Serves other static files like CSS, JS, images, etc.

// Middleware to set or retrieve the client identifier cookie
app.Use(async (context, next) =>
{
    const string clientIdCookieName = "ClientId";

    if (!context.Request.Cookies.TryGetValue(clientIdCookieName, out var clientId))
    {
        // Generate a new unique client ID
        clientId = GenerateUniqueClientId();
        context.Response.Cookies.Append(clientIdCookieName, clientId, new CookieOptions
        {
            HttpOnly = true, // Prevent client-side JavaScript from accessing the cookie
            Secure = false,   // Use only over HTTPS (false for dev)
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365) // Cookie expiration
        });
        Console.WriteLine($"New client ID generated and set: {clientId}");
    }
    else
    {
        // Console.WriteLine($"Existing client ID found: {clientId}");
    }

    // Pass to the next middleware
    await next();
});

// Helper function to generate a unique client ID
static string GenerateUniqueClientId()
{
    using var rng = RandomNumberGenerator.Create();
    var bytes = new byte[16];
    rng.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}

// Routes are defined and registered for listening for network requests
// in the constructors of the initalized classed below
// and then in turn processed by methods as called from the routes definitions 
CrossWarsActions game = new(app);

app.Run();