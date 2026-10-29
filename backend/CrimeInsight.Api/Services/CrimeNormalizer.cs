using System.Globalization;
using System.Text.Json;
using CrimeInsight.Api.Domain;

namespace CrimeInsight.Api.Services;

public sealed class DataQualityException(IReadOnlyList<string> issues)
    : Exception("데이터 품질 검증에 실패했습니다: " + string.Join(" / ", issues.Take(5)))
{
    public IReadOnlyList<string> Issues { get; } = issues;
}

public static class CrimeNormalizer
{
    private static readonly string[] ProvincePrefixes =
    [
        "서울", "부산", "대구", "인천", "광주", "대전", "울산", "세종시", "세종",
        "경기도", "경기", "강원도", "강원", "충북", "충남", "충청북도", "충청남도",
        "전북", "전남", "전라북도", "전라남도", "경북", "경남", "경상북도", "경상남도", "제주", "제주도"
    ];

    public static IReadOnlyList<CrimeObservation> Normalize(int year, IReadOnlyList<JsonElement> rows)
    {
        var issues = new List<string>();
        var result = new List<CrimeObservation>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (rows.Count == 0) throw new DataQualityException(["원본 행 수가 0입니다."]);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var major = ReadLabel(row, "범죄대분류");
            var minor = ReadLabel(row, "범죄중분류");
            if (major.Length == 0 || minor.Length == 0)
            {
                issues.Add($"행 {i + 1}: 범죄 분류 누락");
                continue;
            }
            var matched = 0;
            foreach (var prop in row.EnumerateObject())
            {
                var region = prop.Name.Trim();
                if (!IsRegion(region)) continue;
                matched++;
                if (!TryReadCount(prop.Value, out var count))
                {
                    issues.Add($"행 {i + 1} / {region}: 숫자 누락 또는 형식 오류");
                    continue;
                }
                var key = $"{year}\u001f{major}\u001f{minor}\u001f{region}";
                if (!keys.Add(key))
                {
                    issues.Add($"행 {i + 1} / {region}: 중복 분류-지역 조합");
                    continue;
                }
                var (province, district) = SplitRegion(region);
                result.Add(new CrimeObservation
                {
                    Year = year, CategoryMajor = major, CategoryMinor = minor,
                    Region = region, Province = province, District = district, Count = count
                });
            }
            if (matched == 0) issues.Add($"행 {i + 1}: 인식 가능한 지역 컬럼 없음");
        }
        // A schema mismatch must never silently overwrite the previous year's snapshot.
        if (issues.Count > 0) throw new DataQualityException(issues.Take(30).ToArray());
        if (result.Count == 0) throw new DataQualityException(["유효한 통계 레코드가 없습니다."]);
        return result;
    }

    private static string ReadLabel(JsonElement row, string name)
    {
        foreach (var p in row.EnumerateObject())
            if (p.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "").Trim() : "";
        return "";
    }

    private static bool IsRegion(string key) => ProvincePrefixes.Any(prefix =>
        key == prefix || key.StartsWith(prefix + " ", StringComparison.Ordinal));

    public static (string Province, string District) SplitRegion(string region)
    {
        if (region is "세종시" or "세종") return ("세종", "세종시");
        var pos = region.IndexOf(' ');
        return pos < 0 ? (region, region) : (region[..pos], region[(pos + 1)..]);
    }

    private static bool TryReadCount(JsonElement value, out int count)
    {
        count = 0;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetInt32(out count) && count >= 0;
        if (value.ValueKind != JsonValueKind.String) return false;
        var raw = (value.GetString() ?? "").Replace(",", "").Trim();
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 0;
    }
}
