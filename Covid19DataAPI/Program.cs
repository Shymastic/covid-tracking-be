using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Data;
using Covid19DataAPI.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Use In-Memory database for code-first approach
builder.Services.AddDbContext<CovidDbContext>(options =>
    options.UseInMemoryDatabase("CovidDB"));

// Register the data loader as singleton (shared across requests)
builder.Services.AddSingleton<CovidDataLoader>();

// Add HTTP Client for potential future external data loading
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "COVID-19 Code-First API",
        Version = "v1",
        Description = "Code-first COVID-19 API with auto-loaded data. No database required!"
    });
});

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "COVID-19 API");
    c.RoutePrefix = "";
});

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Pre-load data on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dataLoader = scope.ServiceProvider.GetRequiredService<CovidDataLoader>();
        await dataLoader.EnsureDataLoadedAsync();
        Console.WriteLine("COVID-19 data pre-loaded successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not pre-load data: {ex.Message}");
    }
}

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("COVID-19 Code-First API with Auto Data Loading");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("Swagger UI: http://localhost:5129");
Console.WriteLine("REST API: http://localhost:5129/api/");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("Features:");
Console.WriteLine("? Code-First (No database setup required)");
Console.WriteLine("? Auto data loading on startup");
Console.WriteLine("? 28 countries with realistic COVID data");
Console.WriteLine("? 30 days of historical data per country");
Console.WriteLine("? REST API support");
Console.WriteLine("? Consistent data across all team members");
Console.WriteLine("=".PadRight(60, '='));

app.Run();