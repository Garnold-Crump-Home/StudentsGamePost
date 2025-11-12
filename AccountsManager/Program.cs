using AccountsManager.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Folder for game builds
var gameBuildsPath = Path.Combine(Directory.GetCurrentDirectory(), "GameBuilds");
if (!Directory.Exists(gameBuildsPath))
    Directory.CreateDirectory(gameBuildsPath);


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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "GameBuilds")),
    RequestPath = "/GameBuilds"
});



app.UseCors("AllowAll");


app.UseHttpsRedirection();


app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();
