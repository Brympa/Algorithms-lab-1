using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AlgorithmLab;
using AlgorithmLab.Interop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped<ChartInterop>();

await builder.Build().RunAsync();