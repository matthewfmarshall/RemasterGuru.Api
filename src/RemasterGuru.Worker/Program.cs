using RemasterGuru.Infrastructure;
using RemasterGuru.Worker;
using RemasterGuru.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRemasterGuruInfrastructure(builder.Configuration);
builder.Services.AddHttpClient(nameof(XaiImageRemasterService));
builder.Services.AddSingleton<IImageRemasterService, XaiImageRemasterService>();
builder.Services.AddHostedService<RemasterJobWorker>();

var host = builder.Build();
host.Run();
