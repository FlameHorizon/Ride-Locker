using Microsoft.EntityFrameworkCore;
using Website.Components;
using Website.Data;
using Website.Services;
using Syncfusion.Blazor;
using Syncfusion.Licensing;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion setup with licence key.
var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

if (!string.IsNullOrWhiteSpace(syncfusionKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
}

builder.Services.AddSyncfusionBlazor();

// In-memory cache
builder.Services.AddMemoryCache();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core
builder.Services.AddDbContextFactory<AppDbContext>(options =>
  options.UseSqlite(
    builder.Configuration.GetConnectionString("AppDbContext") ??
    throw new InvalidOperationException("Connection string 'AppDbContext' not found.")));

// For EFCore.BulkExtensions and sqlite3 there is a need for additional reference
// and this call below.
SQLitePCL.Batteries.Init();

// Upload state service used for uploading gpx tracks to database.
builder.Services.AddScoped<UploadModalStateService>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.UseStaticFiles();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// EF Core - apply migrations during startup.
// NOTE: This should be done only in dev env.
// See more on: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#apply-migrations-at-runtime
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // If 'data' folder does not exists, database will not be created
    // and application will crash.
    string dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    if (Directory.Exists(dataDir) == false)
    {
        Directory.CreateDirectory(dataDir);
    }
    await db.Database.MigrateAsync();
}

app.Run();
