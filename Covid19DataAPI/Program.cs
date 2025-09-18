using Covid19DataAPI.Data;
using Covid19DataAPI.Services;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<CovidDataSources>(
    builder.Configuration.GetSection("CovidDataSources"));
builder.Services.Configure<ImportSettings>(
    builder.Configuration.GetSection("ImportSettings"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<CovidDataImporter>();

// EF Context
builder.Services.AddDbContext<CovidDbContext>(options =>
    options.UseInMemoryDatabase("CovidDB"));

// Controllers WITHOUT OData middleware - just regular controllers with OData query support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Add OData query support without OData routing middleware
builder.Services.AddODataQueryFilter();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "COVID-19 API with OData Query Support",
        Version = "v1",
        Description = "COVID-19 data API with REST endpoints and OData query features ($filter, $orderby, $select)"
    });
});

var app = builder.Build();

// Middleware pipeline - NO OData middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "COVID-19 API v1");
    c.RoutePrefix = "";
    c.DocumentTitle = "COVID-19 API Documentation";
});

app.UseCors();
app.UseRouting();
app.MapControllers();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var importer = scope.ServiceProvider.GetRequiredService<CovidDataImporter>();
    var context = scope.ServiceProvider.GetRequiredService<CovidDbContext>();

    Console.WriteLine("Importing data...");
    await importer.ImportAllDataAsync();

    var countries = CovidDataImporter.GetCountries();
    var cases = CovidDataImporter.GetCovidCases(0, int.MaxValue);

    context.Countries.AddRange(countries);
    context.CovidCases.AddRange(cases);
    await context.SaveChangesAsync();

    Console.WriteLine($"Data seeded: {countries.Count} countries, {cases.Count} cases");
}

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("COVID-19 API Server Started");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine($"?? Homepage (Swagger): http://localhost:5129");
Console.WriteLine($"?? REST API: http://localhost:5129/api/countries");
Console.WriteLine($"?? OData-style queries: http://localhost:5129/odata/Countries");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("Available Endpoints:");
Console.WriteLine("REST API:");
Console.WriteLine("  GET /api/countries");
Console.WriteLine("  GET /api/covidcases");
Console.WriteLine("  POST /api/covidcases/import");
Console.WriteLine("");
Console.WriteLine("OData-style Query API:");
Console.WriteLine("  GET /odata/Countries");
Console.WriteLine("  GET /odata/CovidCases");
Console.WriteLine("  GET /odata/Countries?$filter=Region eq 'Asia'");
Console.WriteLine("  GET /odata/CovidCases?$orderby=Confirmed desc&$top=10");
Console.WriteLine("  GET /odata/Countries?$select=CountryName,Population");
Console.WriteLine("=".PadRight(70, '='));

app.Run();