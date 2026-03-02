using FluentAssertions;
using Ticket.Configuration;
using Ticket.Validators;

namespace Ticket.Tests.Unit.Services;

public class NotificationOptionsValidatorTests
{
    private readonly NotificationOptionsValidator _validator = new();

    [Theory]
    [InlineData("none")]
    [InlineData("log")]
    [InlineData("email")]
    [InlineData("webhook")]
    public void Should_Pass_ForAllowedChannels(string channel)
    {
        var options = new NotificationOptions { PreferredChannel = channel, NotifyOnTicketCreated = true, NotifyOnTicketResolved = true };
        _validator.Validate(options).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("slack")]
    public void Should_Fail_ForInvalidChannel(string channel)
    {
        var options = new NotificationOptions { PreferredChannel = channel };
        _validator.Validate(options).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_WhenResolvedEnabledWithoutCreate()
    {
        var options = new NotificationOptions
        {
            PreferredChannel = "log",
            NotifyOnTicketCreated = false,
            NotifyOnTicketResolved = true
        };

        var result = _validator.Validate(options);
        result.IsValid.Should().BeFalse();
    }
}

public class RateLimitingOptionsValidatorTests
{
    private readonly RateLimitingOptionsValidator _validator = new();

    [Fact]
    public void Should_Pass_ForValidOptions()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 100,
            WindowSeconds = 30,
            QueueLimit = 5
        };

        _validator.Validate(options).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(-1, 10, 0)]
    public void Should_Fail_ForInvalidPermitLimit(int permit, int window, int queue)
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = permit,
            WindowSeconds = window,
            QueueLimit = queue
        };

        _validator.Validate(options).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 0, 0)]
    [InlineData(5, -1, 0)]
    public void Should_Fail_ForInvalidWindow(int permit, int window, int queue)
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = permit,
            WindowSeconds = window,
            QueueLimit = queue
        };

        _validator.Validate(options).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_ForNegativeQueueLimit()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 5,
            WindowSeconds = 10,
            QueueLimit = -1
        };

        _validator.Validate(options).IsValid.Should().BeFalse();
    }
}
