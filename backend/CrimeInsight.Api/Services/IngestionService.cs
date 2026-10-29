using System.Security.Cryptography;
using System.Text;
using CrimeInsight.Api.Data;
using CrimeInsight.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrimeInsight.Api.Services;

public sealed class IngestionService(OdcloudClient upstream, CrimeDbContext db, ILogger<IngestionService> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<IngestionRun?> SynchronizeAsync(int year, CancellationToken ct)
    {
        if (!DatasetCatalog.Paths.ContainsKey(year)) throw new ArgumentOutOfRangeException(nameof(year));
        if (!await Gate.WaitAsync(0, ct)) return null;
        var run = new IngestionRun
        {
            Year = year,
            SourceEndpoint = $"https://api.odcloud.kr/api/{DatasetCatalog.Paths[year]}"
        };
        try
        {
            db.IngestionRuns.Add(run);
            await db.SaveChangesAsync(ct);
            var fetched = await upstream.FetchYearAsync(year, ct);
            run.RawRows = fetched.Rows.Count;
            var normalized = CrimeNormalizer.Normalize(year, fetched.Rows);
            run.Observations = normalized.Count;
            run.Regions = normalized.Select(x => x.Region).Distinct().Count();
            if (run.Regions < 5)
                throw new DataQualityException(["인식된 지역이 5개 미만입니다. API 응답 구조를 확인하세요."]);
            var canonical = string.Join("\n", normalized
                .OrderBy(x => x.Region, StringComparer.Ordinal)
                .ThenBy(x => x.CategoryMajor, StringComparer.Ordinal)
                .ThenBy(x => x.CategoryMinor, StringComparer.Ordinal)
                .Select(x => $"{x.Year}|{x.Region}|{x.CategoryMajor}|{x.CategoryMinor}|{x.Count}"));
            run.Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            var previous = await db.IngestionRuns.AsNoTracking()
                .Where(x => x.Year == year && x.Status == "Succeeded" && x.Sha256 == run.Sha256)
                .AnyAsync(ct);
            if (previous)
            {
                run.Status = "Unchanged";
            }
            else
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await db.Observations.Where(x => x.Year == year).ExecuteDeleteAsync(ct);
                db.Observations.AddRange(normalized);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                run.Status = "Succeeded";
            }
        }
        catch (Exception ex)
        {
            // Never store API key, request URL with serviceKey, or stack traces in public logs.
            logger.LogWarning("Ingestion failed for year {Year}: {ErrorType}", year, ex.GetType().Name);
            run.Status = "Failed";
            run.ErrorSummary = ex is DataQualityException d
                ? string.Join(" | ", d.Issues.Take(8))
                : ex is UpstreamDataException ? ex.Message : "데이터 적재 실패. 서버 로그 및 DB 연결을 확인하세요.";
            if (run.ErrorSummary.Length > 1800) run.ErrorSummary = run.ErrorSummary[..1800];
        }
        finally
        {
            run.CompletedAtUtc = DateTime.UtcNow;
            try
            {
                // If a transaction failed, inserted entities can still be tracked.
                db.ChangeTracker.Clear();
                db.IngestionRuns.Update(run);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            finally { Gate.Release(); }
        }
        return run;
    }
}
