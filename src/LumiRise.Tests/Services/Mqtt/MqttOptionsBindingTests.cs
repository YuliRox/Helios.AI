using LumiRise.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace LumiRise.Tests.Services.Mqtt;

public class MqttOptionsBindingTests
{
    private static readonly object EnvironmentLock = new();

    [Fact]
    public void EnvironmentVariables_BindUsernameAndPassword()
    {
        const string usernameKey = "Mqtt__Username";
        const string passwordKey = "Mqtt__Password";
        const string expectedUsername = "prod-user";
        const string expectedPassword = "prod-pass";

        lock (EnvironmentLock)
        {
            var originalUsername = Environment.GetEnvironmentVariable(usernameKey);
            var originalPassword = Environment.GetEnvironmentVariable(passwordKey);

            try
            {
                Environment.SetEnvironmentVariable(usernameKey, expectedUsername);
                Environment.SetEnvironmentVariable(passwordKey, expectedPassword);

                var configuration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();

                var options = new MqttOptions();
                configuration.GetSection(MqttOptions.SectionName).Bind(options);

                options.Username.Should().Be(expectedUsername);
                options.Password.Should().Be(expectedPassword);
            }
            finally
            {
                Environment.SetEnvironmentVariable(usernameKey, originalUsername);
                Environment.SetEnvironmentVariable(passwordKey, originalPassword);
            }
        }
    }
}
