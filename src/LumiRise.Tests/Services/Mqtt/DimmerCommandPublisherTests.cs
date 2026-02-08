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
public class DimmerCommandPublisherTests
{
    private readonly Mock<ILogger<DimmerCommandPublisher>> _loggerMock = new();
    private readonly Mock<IMqttConnectionManager> _connectionManagerMock = new();
    private readonly MqttOptions _options = new();

    [Fact]
    public void Publisher_CanBeInstantiated()
    {
        var publisher = new DimmerCommandPublisher(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            Options.Create(_options));

        Assert.NotNull(publisher);
    }

    [Fact]
    public async Task SetBrightnessAsync_WithInvalidPercentageOver100_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.SetBrightnessAsync(101, CancellationToken.None));
    }

    [Fact]
    public async Task SetBrightnessAsync_WithNegativePercentage_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.SetBrightnessAsync(-1, CancellationToken.None));
    }

    [Fact]
    public async Task RampBrightnessAsync_WithInvalidStart_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.RampBrightnessAsync(-1, 50, TimeSpan.FromSeconds(1),
                null, CancellationToken.None));
    }

    [Fact]
    public async Task RampBrightnessAsync_WithInvalidTarget_ThrowsArgumentException()
    {
        var publisher = new DimmerCommandPublisher(
            _loggerMock.Object,
            _connectionManagerMock.Object,
            Options.Create(_options));

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.RampBrightnessAsync(50, 101, TimeSpan.FromSeconds(1),
                null, CancellationToken.None));
    }
}
