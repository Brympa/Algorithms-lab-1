using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AlgorithmLab;
using AlgorithmLab.Core.Algorithms;
using AlgorithmLab.Core.Benchmark;
using AlgorithmLab.Core.Dataset;
using AlgorithmLab.Interop;
using AlgorithmLab.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Клиентский HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Реестр алгоритмов и мастер-датасет
builder.Services.AddSingleton<IAlgorithmRegistry, AlgorithmRegistry>();
builder.Services.AddSingleton<IMasterDatasetProvider, MasterDatasetProvider>();

// Движок замеров
builder.Services.AddScoped<PrecisionBenchmarkEngine>();

// Графика и JSInterop
builder.Services.AddScoped<ChartInterop>();
builder.Services.AddScoped<Plotly3DInterop>();

// Хранилище (PostgreSQL 18 + Offline Local Cache)
builder.Services.AddScoped<IExperimentStorageService, HybridExperimentStorageService>();

// Очередь задач для алгоритмов
builder.Services.AddScoped<BenchmarkQueueService>();

await builder.Build().RunAsync();