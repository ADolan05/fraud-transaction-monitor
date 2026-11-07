using FraudMonitor.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddDbContext<FraudDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddControllers()
    // Send enums as "Checking" instead of 0 so the API is readable in Swagger and the UI.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations and seed synthetic data on startup so `docker compose up` gives a working app.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();
    await db.Database.MigrateAsync();
    if (await DbSeeder.SeedAsync(db))
        app.Logger.LogInformation("Seeded synthetic customers, accounts and merchants.");
}

// Swagger is on in every environment so reviewers can try the API without the front end.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
