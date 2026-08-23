using AgentDashboard.Components;
using CentralOrchestrator.Data;
using CentralOrchestrator.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Direct SQL Server DbContext connection
var connectionString = builder.Configuration.GetConnectionString("AgentRegistryConnection") 
    ?? "Server=localhost;Database=AgentRegistryDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AgentRegistryDbContext>(options =>
{
    if (connectionString.Contains("Data Source=") && connectionString.EndsWith(".db"))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Register Core Database Services directly in Blazor Server
builder.Services.AddScoped<IAgentRegistryService, AgentRegistryService>();
builder.Services.AddScoped<IHumanTaskService, HumanTaskService>();
builder.Services.AddScoped<IDatabaseQueueService, DatabaseQueueService>();

// Add Blazor Server Interactive Component Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
