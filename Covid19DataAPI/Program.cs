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
        .SetMaxTop(1000));

// In-memory database for OData compatibility
builder.Services.AddDbContext<CovidDbContext>(options =>
    options.UseInMemoryDatabase("CovidDB"));

// Register services
builder.Services.AddSingleton<CovidDataImporter>();
builder.Services.AddHttpClient();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .AllowAnyOrigin()
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

    // Fix Swagger conflict by resolving conflicting actions
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Document both API and OData routes differently
    c.DocInclusionPredicate((docName, description) =>
    {
        // Include regular API routes
        if (description.RelativePath?.StartsWith("api/") == true)
            return true;

        // Include OData routes but mark them differently
        if (description.RelativePath?.StartsWith("odata/") == true)
            return true;

        return false;
    });
});

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "COVID-19 API");
    c.RoutePrefix = "";
    c.DocumentTitle = "COVID-19 API Documentation";
});

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Auto-import data on startup if configured
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ImportSettings>>();
    if (config.Value.AutoImportOnStartup)
    {
        try
        {
            var importer = scope.ServiceProvider.GetRequiredService<CovidDataImporter>();
            var success = await importer.ImportAllDataAsync();
            Console.WriteLine(success ? "? COVID-19 data imported successfully on startup!" : "? Data import failed - check logs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Auto-import failed: {ex.Message}");
        }
    }
}

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("COVID-19 API with Real CSV Import & OData");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine($"Swagger UI: http://localhost:5129");
Console.WriteLine($"REST API: http://localhost:5129/api/");
Console.WriteLine($"OData API: http://localhost:5129/odata/");
Console.WriteLine($"OData Metadata: http://localhost:5129/odata/$metadata");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("API Examples:");
Console.WriteLine("REST: GET /api/countries");
Console.WriteLine("OData: GET /odata/Countries?$filter=Region eq 'Asia'");
Console.WriteLine("OData: GET /odata/CovidCases?$orderby=Confirmed desc&$top=10");
Console.WriteLine("Manual Import: POST /api/covidcases/import");
Console.WriteLine("=".PadRight(70, '='));

app.Run();

// OData EDM Model configuration
static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();
    builder.EntitySet<Country>("Countries");
    builder.EntitySet<CovidCase>("CovidCases");
    return builder.GetEdmModel();
}