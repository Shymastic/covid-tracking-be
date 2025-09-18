using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Data;
using Covid19DataAPI.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.Edm;
using Covid19DataAPI.Models;

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

// Full OData Configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    })
    .AddOData(options =>
    {
        options.AddRouteComponents("odata", GetEdmModel());

        // Enable all OData query options
        options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(null);

        // Enable advanced features
        options.EnableQueryFeatures();
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Swagger with OData support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "COVID-19 API with Full OData Support",
        Version = "v1",
        Description = "Complete COVID-19 data API with full OData v4 support including aggregations, advanced queries, and metadata"
    });
    c.ResolveConflictingActions(apiDescriptions =>
    {
        // Prefer OData controllers over regular API controllers for OData routes
        var odataActions = apiDescriptions.Where(desc => desc.RelativePath?.StartsWith("odata/") == true);
        if (odataActions.Any())
            return odataActions.First();
        return apiDescriptions.First();
    });
});

var app = builder.Build();

// Middleware pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "COVID-19 API v1");
    c.RoutePrefix = "";
    c.DocumentTitle = "COVID-19 Full OData API";
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

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("COVID-19 API with FULL OData v4 Support");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine($"Homepage (Swagger): http://localhost:5129");
Console.WriteLine($"OData Service Document: http://localhost:5129/odata/");
Console.WriteLine($"OData Metadata: http://localhost:5129/odata/$metadata");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("Available OData Endpoints:");
Console.WriteLine("Basic Queries:");
Console.WriteLine("  GET /odata/Countries");
Console.WriteLine("  GET /odata/CovidCases");
Console.WriteLine("");
Console.WriteLine("Advanced Queries:");
Console.WriteLine("  GET /odata/Countries?$filter=Region eq 'Asia'&$orderby=Population desc");
Console.WriteLine("  GET /odata/CovidCases?$expand=Country&$top=10&$orderby=Confirmed desc");
Console.WriteLine("  GET /odata/CovidCases?$select=Confirmed,Deaths,Country/CountryName&$expand=Country($select=CountryName)");
Console.WriteLine("");
Console.WriteLine("Aggregation Queries:");
Console.WriteLine("  GET /odata/CovidCases?$apply=aggregate(Confirmed with sum as TotalConfirmed)");
Console.WriteLine("  GET /odata/CovidCases?$apply=groupby((Country/Region),aggregate(Deaths with sum as TotalDeaths))");
Console.WriteLine("  GET /odata/CovidCases?$apply=filter(Confirmed gt 1000)/aggregate(Confirmed with average as AvgConfirmed)");
Console.WriteLine("");
Console.WriteLine("Count Queries:");
Console.WriteLine("  GET /odata/Countries/$count");
Console.WriteLine("  GET /odata/CovidCases/$count?$filter=Recovered gt 0");
Console.WriteLine("=".PadRight(80, '='));

app.Run();

static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();

    // Configure Countries entity set
    var countries = builder.EntitySet<Country>("Countries");
    countries.EntityType.HasKey(c => c.Id);

    // Configure CovidCases entity set
    var covidCases = builder.EntitySet<CovidCase>("CovidCases");
    covidCases.EntityType.HasKey(c => c.Id);

    // Configure navigation properties
    covidCases.EntityType.HasRequired(c => c.Country);

    Console.WriteLine("Full EDM Model created with aggregation support");
    return builder.GetEdmModel();
}