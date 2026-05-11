using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Services;
using WeddingOrchestrator.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddScoped<ISongService, SongService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IWeddingService, WeddingService>();
builder.Services.AddScoped<IConflictDetectionService, ConflictDetectionService>();
builder.Services.AddScoped<IDocxService, DocxService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var songService = scope.ServiceProvider.GetRequiredService<ISongService>();
    await songService.SyncFileMetadataAsync();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("DevCors");
app.UseAuthorization();
app.MapControllers();

app.Run();
