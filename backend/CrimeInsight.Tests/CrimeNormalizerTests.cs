using System.Text.Json;
using CrimeInsight.Api.Services;
using Xunit;

namespace CrimeInsight.Tests;

public class CrimeNormalizerTests
{
    private static List<JsonElement> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(x => x.Clone()).ToList();
    }

    [Fact]
    public void Normalizes_Wide_Regional_Columns_And_Zero()
    {
        var rows = Parse("""[{"범죄대분류":"절도범죄","범죄중분류":"절도","부산 남구":15,"서울 중구":"1,000","세종시":0}]""");
        var data = CrimeNormalizer.Normalize(2025, rows);
        Assert.Equal(3, data.Count);
        Assert.Contains(data, x => x.Region == "서울 중구" && x.Count == 1000);
        Assert.Contains(data, x => x.Region == "세종시" && x.Count == 0 && x.Province == "세종");
    }

    [Fact]
    public void Rejects_Negative_Count()
    {
        var rows = Parse("""[{"범죄대분류":"절도","범죄중분류":"절도","부산 남구":-1}]""");
        Assert.Throws<DataQualityException>(() => CrimeNormalizer.Normalize(2025, rows));
    }

    [Fact]
    public void Rejects_Duplicate_Composite_Keys()
    {
        var rows = Parse("""[{"범죄대분류":"절도","범죄중분류":"절도","부산 남구":3},{"범죄대분류":"절도","범죄중분류":"절도","부산 남구":9}]""");
        Assert.Throws<DataQualityException>(() => CrimeNormalizer.Normalize(2025, rows));
    }

    [Fact]
    public void Rejects_Missing_Classification()
    {
        var rows = Parse("""[{"범죄대분류":"절도","부산 남구":1}]""");
        Assert.Throws<DataQualityException>(() => CrimeNormalizer.Normalize(2025, rows));
    }

    [Fact]
    public void Rejects_Null_Count_Instead_Of_Silently_Converting_To_Zero()
    {
        var rows = Parse("""[{"범죄대분류":"절도","범죄중분류":"절도","부산 남구":null}]""");
        Assert.Throws<DataQualityException>(() => CrimeNormalizer.Normalize(2025, rows));
    }
}
