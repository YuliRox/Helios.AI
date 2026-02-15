using LumiRise.Api.Configuration;
using LumiRise.Api.Services.Mqtt.Implementation;
using LumiRise.Api.Services.Mqtt.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LumiRise.Tests.Services.Mqtt;

/// <summary>
/// Unit tests for DimmerCommandPublisher.
/// Focus on command structure validation and parameter validation.
/// </summary>
public class DimmerCommandPublisherTests(ITestOutputHelper testOutput)
{
    private readonly ILogger<DimmerCommandPublisher> _logger = new ErrorFailingLogger<DimmerCommandPublisher>(testOutput.WriteLine);
    private readonly Mock<IMqttConnectionManager> _connectionManagerMock = new();
    private readonly MqttOptions _options = new();

    [Fact]
    public void Publisher_CanBeInstantiated()
    {
        var publisher = new DimmerCommandPublisher(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        publisher.Should().NotBeNull();
    }

    [Fact]
    public async Task SetBrightnessAsync_WithInvalidPercentageOver100_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        Func<Task> act = () => publisher.SetBrightnessAsync(101, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetBrightnessAsync_WithNegativePercentage_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        Func<Task> act = () => publisher.SetBrightnessAsync(-1, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RampBrightnessAsync_WithInvalidStart_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        Func<Task> act = () => publisher.RampBrightnessAsync(-1, 50, TimeSpan.FromSeconds(1),
            null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RampBrightnessAsync_WithInvalidTarget_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _logger,
            _connectionManagerMock.Object,
            Options.Create(_options));

        Func<Task> act = () => publisher.RampBrightnessAsync(50, 101, TimeSpan.FromSeconds(1),
            null, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
