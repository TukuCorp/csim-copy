using CarbonSim.Web;
using FluentAssertions;

namespace CarbonSim.Web.Tests;

/// <summary>
/// The display currency: the engine's internal unit is the US dollar the trainer deck quotes, and a
/// host may show Vietnamese dong instead by converting at its configured rate. These tests pin the
/// number a player reads and the numbers on both sides of the conversion.
/// </summary>
public sealed class CurrencyDisplayTests
{
    [Fact]
    public void The_internal_unit_is_shown_as_dollars_with_its_code()
    {
        using CultureScope culture = new("en-US");

        CurrencyDisplay display = new(CurrencyDisplay.Usd, 25_000m);

        display.Money(1_250_000m).Should().Be("1,250,000 USD");
        display.Money2(1234.5m).Should().Be("1,234.50 USD");
        display.IsVnd.Should().BeFalse();
    }

    [Fact]
    public void Dong_money_is_the_internal_amount_times_the_configured_rate()
    {
        using CultureScope culture = new("vi-VN");

        CurrencyDisplay display = new(CurrencyDisplay.Vnd, 25_000m);

        display.ToDisplay(1_000m).Should().Be(25_000_000m);
        display.Money(1_000m).Should().Be("25.000.000 VND");
    }

    [Fact]
    public void A_typed_dong_amount_converts_back_to_the_internal_unit()
    {
        CurrencyDisplay display = new(CurrencyDisplay.Vnd, 25_000m);

        display.ToInternal(25_000_000m).Should().Be(1_000m);
        display.ToInternal(display.ToDisplay(777m)).Should().Be(777m);
    }

    [Fact]
    public void Dong_is_grouped_for_an_English_reader_too()
    {
        using CultureScope culture = new("en-US");

        CurrencyDisplay display = new(CurrencyDisplay.Vnd, 25_000m);

        display.Money(1_000m).Should().Be("25,000,000 VND");
    }

    [Fact]
    public void A_price_per_tonne_carries_its_code_in_either_currency()
    {
        using CultureScope culture = new("en-US");

        new CurrencyDisplay(CurrencyDisplay.Usd, 25_000m).Price(104.5m).Should().Be("104.50 USD");
        new CurrencyDisplay(CurrencyDisplay.Vnd, 25_000m).Price(104.5m).Should().Be("2,612,500 VND");
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("VND", true)]
    [InlineData("vnd", true)]
    [InlineData("EUR", false)]
    [InlineData("", false)]
    public void Only_the_two_offered_currencies_are_recognised(string code, bool known)
    {
        CurrencyDisplay.IsKnown(code).Should().Be(known);
    }

    [Fact]
    public void An_unknown_code_falls_back_to_the_internal_unit()
    {
        CurrencyDisplay display = new("EUR", 25_000m);

        display.Code.Should().Be(CurrencyDisplay.Usd);
    }
}
