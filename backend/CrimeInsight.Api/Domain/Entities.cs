using Microsoft.EntityFrameworkCore;

namespace CrimeInsight.Api.Domain;

[Index(nameof(Year), nameof(Region), nameof(CategoryMajor), nameof(CategoryMinor), IsUnique = true)]
[Index(nameof(Year), nameof(Region))]
public class CrimeObservation
{
    public long Id { get; set; }
    public int Year { get; set; }
    public string Region { get; set; } = "";
    public string Province { get; set; } = "";
    public string District { get; set; } = "";
    public string CategoryMajor { get; set; } = "";
    public string CategoryMinor { get; set; } = "";
    public int Count { get; set; }
}

[Index(nameof(Year), nameof(StartedAtUtc))]
public class IngestionRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Year { get; set; }
    public string Status { get; set; } = "Running";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int RawRows { get; set; }
    public int Observations { get; set; }
    public int Regions { get; set; }
    public string? Sha256 { get; set; }
    public string? ErrorSummary { get; set; }
    public string SourceEndpoint { get; set; } = "";
}
