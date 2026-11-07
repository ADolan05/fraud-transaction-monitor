using FraudMonitor.Api.Data;
using FraudMonitor.Api.Fraud;
using FraudMonitor.Api.Fraud.Rules;
using FraudMonitor.Api.Simulation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddDbContext<FraudDbContext>(options => options.UseNpgsql(connectionString));

// Fraud rules engine: each rule is its own class; the engine sums the weights of those that fire.
builder.Services.Configure<FraudEngineOptions>(builder.Configuration.GetSection(FraudEngineOptions.Section));
builder.Services.AddSingleton<IFraudRule, LargeAmountRule>();
builder.Services.AddSingleton<IFraudRule, VelocityRule>();
builder.Services.AddSingleton<IFraudRule, ImpossibleTravelRule>();
builder.Services.AddSingleton<IFraudRule, OddHourRule>();
builder.Services.AddSingleton<IFraudRule, RiskyCategoryRule>();
builder.Services.AddSingleton<IFraudRule, CardTestingRule>();
builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddScoped<TransactionScorer>();

// Fake transaction stream.
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.Section));
builder.Services.AddSingleton<SimulatorState>();
builder.Services.AddHostedService<TransactionSimulator>();

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
        app.Logger.LogInformation("Seeded synthetic customers, accounts, merchants and transaction history.");
}

// Swagger is on in every environment so reviewers can try the API without the front end.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
