using AgentDashboard.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient pointing to Central Orchestrator API
var orchestratorBaseUrl = builder.Configuration["OrchestratorApiUrl"] ?? "http://localhost:5000";
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(orchestratorBaseUrl)
});

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
