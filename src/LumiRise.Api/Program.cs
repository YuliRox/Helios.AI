using LumiRise.Api.Configuration;
using LumiRise.Api.Data;
using LumiRise.Api.Infrastructure;
using LumiRise.Api.Services.Alarm.Implementation;
using LumiRise.Api.Services.Alarm.Interfaces;
using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure MQTT options
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.Configure<AlarmSchedulerOptions>(
    builder.Configuration.GetSection(AlarmSchedulerOptions.SectionName));

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:Postgres' is required.");

builder.Services.AddDbContext<LumiRiseDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddHangfire(configuration => configuration
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(storageOptions =>
        storageOptions.UseNpgsqlConnection(postgresConnectionString)));
builder.Services.AddHangfireServer();

// Register MQTT services
builder.Services.AddSingleton<IMqttConnectionManager, MqttConnectionManager>();
builder.Services.AddSingleton<IDimmerStateMonitor, DimmerStateMonitor>();
builder.Services.AddSingleton<IInterruptionDetector, InterruptionDetector>();
builder.Services.AddScoped<IDimmerCommandPublisher, DimmerCommandPublisher>();

// Register Alarm services
builder.Services.AddScoped<IAlarmStateMachineFactory, AlarmStateMachineFactory>();
builder.Services.AddScoped<AlarmExecutionJob>();
builder.Services.AddSingleton<IAlarmRecurringJobSynchronizer, AlarmRecurringJobSynchronizer>();
builder.Services.AddHostedService<AlarmRecurringJobSyncHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LumiRiseDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseHangfireDashboard(
    "/hangfire",
    new DashboardOptions
    {
        Authorization =
        [
            new AllowAllHangfireDashboardAuthorizationFilter()
        ]
    });

app.Run();
