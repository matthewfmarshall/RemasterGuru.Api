using RemasterGuru.Api.Auth;
using RemasterGuru.Api.Endpoints;
using RemasterGuru.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddRemasterGuruInfrastructure(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://127.0.0.1:3000")
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

app.UseMiddleware<DevUserAuthMiddleware>();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapApiV1();

app.Run();
