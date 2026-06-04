using FluentAssertions;
using KuchniaUCygana.Application.Services;

namespace KuchniaUCygana.Tests.Unit.Application;

public sealed class TransportLabelCodeNormalizerTests
{
    [Theory]
    [InlineData("BAG-000123-01", "BAG-000123-01")]
    [InlineData("https://test.local/delivery/verify/BAG-000123-01", "BAG-000123-01")]
    [InlineData("https://test.local/delivery/verify/BAG-000123-01?ignored=true", "BAG-000123-01")]
    [InlineData("/delivery/verify/BAG-000123-01", "BAG-000123-01")]
    public void Normalize_ReturnsBagCode(string rawCode, string expected)
    {
        TransportLabelCodeNormalizer.Normalize(rawCode).Should().Be(expected);
    }
}
