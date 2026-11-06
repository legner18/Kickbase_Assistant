using System.Text.Json.Serialization;
using KickbaseAssistant.Api.Realtime;
using KickbaseAssistant.Infrastructure;
using KickbaseAssistant.Infrastructure.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKickbaseAssistantDatabase(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.Configure<LiveHubOptions>(builder.Configuration.GetSection(LiveHubOptions.SectionName));
builder.Services.AddSingleton<LiveWorkerStatusStore>();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200"];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration[$"{LiveHubOptions.SectionName}:PublishKey"]))
{
    app.Logger.LogWarning("LiveHub:PublishKey ist nicht gesetzt - der Worker kann keine Live-Updates an das Frontend senden.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();

app.MapControllers();
app.MapHub<LiveHub>("/hubs/live");

app.Run();
