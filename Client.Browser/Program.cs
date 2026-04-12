using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OpenGarrison.Client.Browser;
using OpenGarrison.Client.Browser.Services;
using OpenGarrison.ClientShared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped(static serviceProvider =>
{
    var httpClient = serviceProvider.GetRequiredService<HttpClient>();
    var stockPackSpriteAssets = ClientRuntimeBootstrap.CreateGameplayPackSpriteAssetService("stock.gg2", httpClient)
        ?? throw new InvalidOperationException("The stock gameplay pack sprite service could not be created for the browser host.");

    return new ClientRuntimeComposition(
        [],
        new GameplayPackSpriteAssetServiceRegistry(new Dictionary<string, GameplayPackSpriteAssetService>
        {
            ["stock.gg2"] = stockPackSpriteAssets,
        }));
});
builder.Services.AddScoped<BrowserAssetProbeService>();
builder.Services.AddScoped<BrowserGameHostService>();

ClientRuntimeBootstrap.InitializeContentRoot();
ClientRuntimeBootstrap.InitializeBrowserBaseAddress(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
