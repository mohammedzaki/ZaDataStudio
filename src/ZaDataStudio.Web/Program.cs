using ZaDataStudio.Web.Components;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Infrastructure.Persistence.Repositories;
using ZaDataStudio.Application.Mapping;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Infrastructure Services (Clean Architecture)
builder.Services.AddScoped<ISessionRepository, SessionRepository>();

// Register SQL Server services with connection manager (Singleton pattern per scope)
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerConnectionManager>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerDatabaseService>();
builder.Services.AddScoped<IDatabaseService>(sp => sp.GetRequiredService<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerDatabaseService>());

// Register Application and Infrastructure services
builder.Services.AddScoped<ILookupColumnAnalyzer, LookupColumnAnalyzer>();
builder.Services.AddScoped<IMappingComparisonService, MappingComparisonService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerComparisonService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Excel.ExcelMappingService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.DataComparisonService>();

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


