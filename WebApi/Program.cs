using Core.Interfaces;
using Core.Services;
using Serilog;
using Serilog.Events;
using WebApi.BackgroundWorkers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IChunkStorage, SimpleFileChunkStorage>();
builder.Services.AddSingleton<IDataChannelWorkerFactory, DataChannelWorkerFactory>();

builder.Services.AddHostedService<ControlChannelWorker>();

var app = builder.Build();

app.Run();