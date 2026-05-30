using System.Globalization;
using FluentAssertions;
using KuchniaUCygana.Web.ModelBinding;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Web;

public sealed class FlexibleDecimalModelBinderTests
{
    [Theory]
    [InlineData("4.5", "4.5")]
    [InlineData("4,5", "4.5")]
    [InlineData("-18.2", "-18.2")]
    [InlineData("-18,2", "-18.2")]
    [InlineData("-51,2", "-51.2")]
    public void TryParseDecimal_ShouldAcceptDotAndCommaDecimalSeparators(string rawValue, string expectedValue)
    {
        var expected = decimal.Parse(expectedValue, CultureInfo.InvariantCulture);

        var parsed = FlexibleDecimalModelBinder.TryParseDecimal(rawValue, out var value);

        parsed.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("4,5,6")]
    public void TryParseDecimal_ShouldRejectInvalidNumbers(string rawValue)
    {
        var parsed = FlexibleDecimalModelBinder.TryParseDecimal(rawValue, out _);

        parsed.Should().BeFalse();
    }
}
