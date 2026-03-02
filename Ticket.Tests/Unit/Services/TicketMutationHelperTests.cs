using FluentAssertions;
using Ticket.Domain.Entities;
using Ticket.Exceptions;
using Ticket.Interfaces.Infrastructure;
using Ticket.Services.Infrastructure;
using Ticket.Tests.TestUtilities.TestDataBuilders;

namespace Ticket.Tests.Unit.Services;

public class TicketMutationHelperTests
{
    private readonly StubSanitizer _sanitizer = new();

    [Fact]
    public void SanitizeRequired_Should_ReturnSanitizedContent()
    {
        var result = TicketMutationHelper.SanitizeRequired(_sanitizer, "<b>body</b>", "err");
        result.Should().Be("sanitized:<b>body</b>");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void SanitizeRequired_Should_Throw_WhenEmpty(string input)
    {
        Action act = () => TicketMutationHelper.SanitizeRequired(_sanitizer, input, "err");
        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void SanitizeRequired_Should_Throw_WhenSanitizerRemovesContent()
    {
        _sanitizer.ForceEmpty = true;
        Action act = () => TicketMutationHelper.SanitizeRequired(_sanitizer, "<script>bad</script>", "err");
        act.Should().Throw<BadRequestException>();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("<div>body</div>", "sanitized:<div>body</div>")]
    public void SanitizeOptional_Should_HandleNulls(string input, string? expected)
    {
        var result = TicketMutationHelper.SanitizeOptional(_sanitizer, input);
        result.Should().Be(expected);
    }

    [Fact]
    public void EnsureRowVersion_Should_Throw_WhenProvidedNull()
    {
        byte[]? provided = null;
        Action act = () => TicketMutationHelper.EnsureRowVersion(Array.Empty<byte>(), provided!);
        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void EnsureRowVersion_Should_Throw_WhenProvidedEmpty()
    {
        Action act = () => TicketMutationHelper.EnsureRowVersion(Array.Empty<byte>(), Array.Empty<byte>());
        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void EnsureRowVersion_Should_Throw_WhenMismatch()
    {
        var current = new byte[] { 0, 1, 2 };
        var provided = new byte[] { 9, 9, 9 };
        Action act = () => TicketMutationHelper.EnsureRowVersion(current, provided);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ApplyNormalization_Should_PopulateDerivedFields()
    {
        var ticket = new TicketBuilder()
            .WithRequester("Alice", "alice@example.com")
            .WithRecipient("Bob", "bob@example.com")
            .WithReferenceCode("Ref-1")
            .BuildEntity();

        TicketMutationHelper.ApplyNormalization(ticket, "Operations");

        ticket.TitleNormalized.Should().Be(ticket.Title.ToUpperInvariant());
        ticket.DepartmentNameNormalized.Should().Be("OPERATIONS");
        ticket.RequesterEmailNormalized.Should().Be("ALICE@EXAMPLE.COM");
        ticket.ReferenceCodeNormalized.Should().Be("REF-1".ToUpperInvariant());
    }

    private sealed class StubSanitizer : IContentSanitizer
    {
        public bool ForceEmpty { get; set; }

        public string Sanitize(string? content)
        {
            if (ForceEmpty)
            {
                return string.Empty;
            }

            return $"sanitized:{content}";
        }
    }
}
