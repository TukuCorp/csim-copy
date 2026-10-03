using System.Xml.Linq;
using CarbonSim.Web.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Tests;

/// <summary>
/// The two resource files are the whole of the screen text, so they have to agree key for key and
/// neither may carry an empty value: a missing Vietnamese key would quietly show English to a
/// Vietnamese player, which is exactly the failure the language switch is meant to prevent.
/// </summary>
public sealed class LocalizationTests
{
    [Fact]
    public void Every_English_key_has_a_non_empty_Vietnamese_value_and_no_extras()
    {
        Dictionary<string, string> english = Read("SharedStrings.resx");
        Dictionary<string, string> vietnamese = Read("SharedStrings.vi.resx");

        english.Should().NotBeEmpty();
        vietnamese.Keys.Should().BeEquivalentTo(english.Keys, "a key with no Vietnamese value would fall back to English");
        english.Values.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
        vietnamese.Values.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void Vietnamese_is_offered_alongside_English_and_uses_the_culture_cookie()
    {
        CarbonSimCultures.Supported.Should().BeEquivalentTo(["en", "vi"]);
        CarbonSimCultures.CookieName.Should().Be("carbonsim.culture");
    }

    [Fact]
    public void The_localiser_answers_in_Vietnamese_for_a_Vietnamese_culture()
    {
        using CultureScope culture = new("vi-VN");
        using ServiceProvider provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        IStringLocalizer<SharedStrings> strings = provider.GetRequiredService<IStringLocalizer<SharedStrings>>();

        strings["Dashboard"].Value.Should().Be("Bảng điều khiển");
        strings["AllowanceAuction"].Value.Should().Be("Đấu giá hạn ngạch");
        strings["SurrenderAndBanking"].Value.Should().Be("Giao nộp và lưu giữ");
    }

    private static Dictionary<string, string> Read(string fileName)
    {
        string path = Path.Combine(RepositoryRoot(), "src", "CarbonSim.Web", "Resources", fileName);

        File.Exists(path).Should().BeTrue($"{fileName} is part of the shipped screens");

        XDocument document = XDocument.Load(path);

        return document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty);
    }

    /// <summary>Walks up from the test's own output folder to the folder holding the solution.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CarbonSim.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be found from the test output folder.");
    }
}
