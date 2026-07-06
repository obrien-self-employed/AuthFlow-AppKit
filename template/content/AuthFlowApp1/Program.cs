using AuthFlow.AppKit;
using Radzen;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // When installed as a Windows Service, the process starts with its
    // working directory set to %SystemRoot%\System32 - pinning the content
    // root to the executable's own directory keeps `dotnet run` (dev) and
    // the installed service (prod) consistent. See INSTALLER.md.
    ContentRootPath = AppContext.BaseDirectory,
});

// Lets the exact same published binary run either as a normal console app
// (dev/manual testing) or as a genuine Windows Service (prod, installed by
// installer/setup.iss). No-op unless launched by the Service Control
// Manager, so `dotnet run` keeps working unchanged.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = builder.Configuration["WindowsService:ServiceName"] ?? "AuthFlowApp1";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

// AuthFlow license activation + heartbeat + in-app update checker, all in
// one call - see https://github.com/Brackford-0brien/AuthFlow-AppKit for
// what this wires up and the appsettings.json "AuthFlow" section it reads.
builder.Services.AddAuthFlowAppKit(builder.Configuration, options =>
{
    options.ProductDisplayName = "AuthFlowApp1";
    options.CompanyName = "Brackford-0brien";
    options.ServiceBaseName = "AuthFlowApp1";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<AuthFlowApp1.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AuthFlow.AppKit.Services.AuthFlowLicenseState).Assembly);

app.Run();
