using RemasterGuru.Infrastructure;
using RemasterGuru.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRemasterGuruInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RemasterJobWorker>();

var host = builder.Build();
host.Run();
