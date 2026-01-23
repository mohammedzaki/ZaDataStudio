using ZaDataStudio.Web.Components;
using ZaDataStudio.Web.Services;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Infrastructure Services (Clean Architecture)
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerComparisonService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Excel.ExcelMappingService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.DataComparisonService>();

// Register legacy services (for backward compatibility during migration)
builder.Services.AddScoped<SessionPersistenceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


