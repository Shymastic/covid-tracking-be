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

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    })
    .AddOData(options => options
        .AddRouteComponents("odata", GetEdmModel())
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(1000)
        .EnableQueryFeatures());

// In-memory database for OData compatibility
builder.Services.AddDbContext<CovidDbContext>(options =>
    options.UseInMemoryDatabase("CovidDB"));

// Register services
builder.Services.AddScoped<CovidDataImporter>();
builder.Services.AddHttpClient();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "COVID-19 API with Real CSV Import & OData",
        Version = "v1",
        Description = "Complete COVID-19 API with configurable CSV import from GitHub and full OData support"
    });

    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "COVID-19 API");
        c.RoutePrefix = "";
    });
}

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Seed database with imported data for OData
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<CovidDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<CovidDataImporter>();

        // Import CSV data first
        var success = await importer.ImportAllDataAsync();

        if (success)
        {
            // Populate EF context for OData
            var countries = CovidDataImporter.GetCountries();
            var cases = CovidDataImporter.GetCovidCases(0, int.MaxValue);

            context.Countries.AddRange(countries);
            context.CovidCases.AddRange(cases);
            await context.SaveChangesAsync();

            Console.WriteLine($"? Data imported: {countries.Count} countries, {cases.Count} cases");
        }
        else
        {
            Console.WriteLine("? Data import failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Setup failed: {ex.Message}");
    }
}

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("COVID-19 API with OData Support");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine($"Swagger UI: http://localhost:5129");
Console.WriteLine($"OData Metadata: http://localhost:5129/odata/$metadata");
Console.WriteLine($"Countries: http://localhost:5129/odata/Countries");
Console.WriteLine($"Covid Cases: http://localhost:5129/odata/CovidCases");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("OData Examples:");
Console.WriteLine("GET /odata/Countries?$filter=Region eq 'Asia'");
Console.WriteLine("GET /odata/CovidCases?$orderby=Confirmed desc&$top=10");
Console.WriteLine("GET /odata/CovidCases?$expand=Country");
Console.WriteLine("=".PadRight(70, '='));

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

    // Configure relationship
    covidCases.EntityType.HasRequired(c => c.Country);

    return builder.GetEdmModel();
}