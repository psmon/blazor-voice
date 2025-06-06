using BlazorVoice.Akka;
using BlazorVoice.Components;
using BlazorVoice.Services;

using MudBlazor.Services;

using NLog;
using NLog.Config;
using NLog.Web;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

var builder = WebApplication.CreateBuilder(args);

// NLog 설정 파일 로드
var env = builder.Environment.EnvironmentName;
var nlogConfigFile = env switch
{
    "Development" => "NLog.Development.config",
    "Local" => "NLog.Local.config",
    _ => "NLog.config"
};
builder.Logging.ClearProviders();
LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigFile);
builder.Host.UseNLog();

logger.Info("Start BlazorVoice Service API");


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(); // Add MudBlazor services

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});


//DI Singleton
builder.Services.AddSingleton<AkkaService>();


//DI Scoped
builder.Services.AddScoped<OpenAIService>();

//DI Transient for DB

var app = builder.Build();


//AKKA System
var akkaService = app.Services.GetRequiredService<AkkaService>();

var actorSystem = akkaService.CreateActorSystem("default");


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<AudioStreamHub>("/audiostream"); // Map SignalR hub

app.Run();
