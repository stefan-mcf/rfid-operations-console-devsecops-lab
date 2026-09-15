using RfidOps.Core.Domain;

namespace RfidOps.Tests;

public sealed class IdentifierTests
{
    [Theory]
    [InlineData(" demo-tag-001 ", "DEMO-TAG-001")]
    [InlineData("ABC123", "ABC123")]
    public void TagIdentifierNormalizesValidValues(string input, string expected)
    {
        Assert.Equal(expected, TagIdentifier.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("TAG WITH SPACES")]
    [InlineData("-INVALID-")]
    public void TagIdentifierRejectsInvalidValues(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => TagIdentifier.Normalize(input));
    }

    [Fact]
    public void ReaderIdentifierNormalizesValidValues()
    {
        Assert.Equal("DEMO-READER-01", ReaderIdentifier.Normalize("demo-reader-01"));
    }
}
