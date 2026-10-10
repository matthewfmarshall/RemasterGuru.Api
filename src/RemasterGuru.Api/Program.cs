using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RemasterGuru.Api.Auth;
using RemasterGuru.Api.Checkout;
using RemasterGuru.Api.Endpoints;
using RemasterGuru.Api.HostedServices;
using RemasterGuru.Api.Print;
using RemasterGuru.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var auth0Enabled = Auth0Settings.IsConfigured(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddRemasterGuruInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IPrintFulfillmentProvider, RpiPrintFulfillmentProvider>();
builder.Services.AddScoped<IPrintOrderSubmissionService, PrintOrderSubmissionService>();
builder.Services.AddRemasterGuruCors(builder.Configuration);

if (auth0Enabled)
{
    var authority = Auth0Settings.GetAuthority(builder.Configuration);
    var audience = builder.Configuration["Auth0:Audience"]
        ?? throw new InvalidOperationException("Auth0:Audience is required when Auth0:Domain is set.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = ClaimTypes.NameIdentifier,
            };
        });
    builder.Services.AddAuthorization();
}

if (builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Print:AutoSubmitInDevelopment", false))
{
    builder.Services.AddHostedService<PrintAutoSubmitHostedService>();
}

var app = builder.Build();

await app.Services.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicyExtensions.AppPolicyName);

if (auth0Enabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<Auth0UserAuthMiddleware>();
}
else
{
    app.UseMiddleware<DevUserAuthMiddleware>();
}

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapApiV1();
app.MapStripeCheckout();
app.MapPrintFulfillment();

app.Run();
