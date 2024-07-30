using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using minimal_api_rate_limiting.Data;
using minimal_api_rate_limiting.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("minimal-api-rate-limiting"));

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            await context.HttpContext.Response.WriteAsync($"Too many requests. Please try again after {retryAfter.TotalMinutes} minute(s).", cancellationToken: token);
        }
        else
        {
            await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken: token);
        }
    };

    options.AddPolicy("getPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: key => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10
            }));

    options.AddPolicy("postPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: key => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    dbContext.Customers.AddRange(
        new Customer { Name = "Peter Parker", Email = "peter.parker@marvel.com" },
        new Customer { Name = "Mary Jane", Email = "mary.jane@marvel.com" },
        new Customer { Name = "Ben Parker", Email = "ben.parker@marvel.com" }
    );

    dbContext.SaveChanges();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.MapGet("/customers/", async (AppDbContext dbContext) =>
    await dbContext.Customers.AsNoTracking().ToListAsync()).RequireRateLimiting("getPolicy");

app.MapPost("/customers/", async (Customer model, AppDbContext dbContext) =>
{
    await dbContext.Customers.AddAsync(model);
    await dbContext.SaveChangesAsync();

    return Results.Created($"/customers/{model.Id}", model);    
}).RequireRateLimiting("postPolicy");

app.Run();
