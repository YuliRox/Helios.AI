using LumiRise.Api.Configuration;
using LumiRise.Api.Data;
using LumiRise.Api.Data.Entities;
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure MQTT options
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.Configure<AlarmSettingsOptions>(
    builder.Configuration.GetSection(AlarmSettingsOptions.SectionName));

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
    await dbContext.Database.MigrateAsync();
    await EnsureDefaultRampProfileAsync(dbContext);
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

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

static async Task EnsureDefaultRampProfileAsync(LumiRiseDbContext dbContext)
{
    var defaultMode = RampProfileEntity.DefaultMode;
    var exists = await dbContext.RampProfiles.AnyAsync(x => x.Mode == defaultMode);
    if (exists)
    {
        return;
    }

    dbContext.RampProfiles.Add(new RampProfileEntity
    {
        Mode = defaultMode,
        StartBrightnessPercent = RampProfileEntity.DefaultStartBrightnessPercent,
        TargetBrightnessPercent = RampProfileEntity.DefaultTargetBrightnessPercent,
        RampDurationSeconds = RampProfileEntity.DefaultRampDurationSeconds,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    });
    await dbContext.SaveChangesAsync();
}

public partial class Program;
