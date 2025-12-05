using AccountsManager.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.IO;


var gameBuildsPath = Path.Combine(Directory.GetCurrentDirectory(), "GameBuilds");
if (!Directory.Exists(gameBuildsPath))
    Directory.CreateDirectory(gameBuildsPath);


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddScoped<Sql>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500_000_000;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500_000_000;
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
    c.OperationFilter<SwaggerFileUploadOperationFilter>();
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
    app.UseHttpsRedirection(); // HTTPS only in dev
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


app.MapControllers();


app.Run();



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
