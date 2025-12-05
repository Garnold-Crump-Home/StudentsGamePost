using AccountsManager.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.IO;

// -------------------------------
// Folder for game builds
// -------------------------------
var gameBuildsPath = Path.Combine(Directory.GetCurrentDirectory(), "GameBuilds");
if (!Directory.Exists(gameBuildsPath))
    Directory.CreateDirectory(gameBuildsPath);

// -------------------------------
// Build WebApplication
// -------------------------------
var builder = WebApplication.CreateBuilder(args);

// -------------------------------
// Configure Kestrel and ports
// -------------------------------
// Use HTTP port 5000 in development (Codespaces-friendly)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500_000_000; // 500 MB
    options.ListenAnyIP(5000); // HTTP for dev
    // Uncomment below for production HTTPS
    // options.ListenAnyIP(7239, listenOptions => listenOptions.UseHttps());
});

// -------------------------------
// Services
// -------------------------------
builder.Services.AddControllers();
builder.Services.AddScoped<Sql>();

// Limit form body for large file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500_000_000; // 500 MB
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AccountsManager API", Version = "v1" });
    c.OperationFilter<SwaggerFileUploadOperationFilter>(); // <-- Add our custom filter
});

// -------------------------------
// Build app
// -------------------------------
var app = builder.Build();

// Development tools
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static files for Unity builds
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath ?? Directory.GetCurrentDirectory(), "GameBuilds")),
    RequestPath = "/GameBuilds",
    ServeUnknownFileTypes = true,
    ContentTypeProvider = new FileExtensionContentTypeProvider(
        new Dictionary<string, string>
        {
            { ".data", "application/octet-stream" },
            { ".wasm", "application/wasm" },
            { ".framework.js", "application/javascript" },
            { ".loader.js", "application/javascript" }
        }
    )
});

// Middleware
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Map controllers
app.MapControllers();

// -------------------------------
// Run
// -------------------------------
app.Run();

// -------------------------------
// Swagger OperationFilter for IFormFile uploads
// -------------------------------
public class SwaggerFileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) || p.ParameterType == typeof(IFormFile[]));

        if (!fileParams.Any()) return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = fileParams.ToDictionary(
                            p => p.Name,
                            p => new OpenApiSchema { Type = "string", Format = "binary" }
                        ),
                        Required = new HashSet<string>(fileParams.Select(p => p.Name))
                    }
                }
            }
        };
    }
}
