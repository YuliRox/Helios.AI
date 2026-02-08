using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure MQTT options
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.SectionName));

// Register MQTT services
builder.Services.AddSingleton<IMqttConnectionManager, MqttConnectionManager>();
builder.Services.AddSingleton<IDimmerStateMonitor, DimmerStateMonitor>();
builder.Services.AddSingleton<IInterruptionDetector, InterruptionDetector>();
builder.Services.AddScoped<IDimmerCommandPublisher, DimmerCommandPublisher>();

// Register MqttConnectionManager as hosted service
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<IMqttConnectionManager>() as MqttConnectionManager
        ?? throw new InvalidOperationException("MqttConnectionManager not registered"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
