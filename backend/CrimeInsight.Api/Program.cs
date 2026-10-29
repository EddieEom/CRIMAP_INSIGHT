using System.Security.Cryptography;
using System.Text;
using CrimeInsight.Api.Data;
using CrimeInsight.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<CrimeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CrimeInsight")));
builder.Services.AddHttpClient<OdcloudClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OpenData:BaseUrl"] ?? "https://api.odcloud.kr/api/");
    client.Timeout = TimeSpan.FromSeconds(40);
});
builder.Services.AddScoped<IngestionService>();
builder.Services.AddCors(o => o.AddPolicy("web", p => p.WithOrigins(
    builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
// Local-developer bootstrap. Production databases should use versioned EF Core migrations.
if (app.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("CrimeInsight")))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<CrimeDbContext>();
    for (var attempt = 1; attempt <= 8; attempt++)
    {
        try
        {
            await database.Database.EnsureCreatedAsync();
            app.Logger.LogInformation("CrimeInsight database schema is ready");
            break;
        }
        catch (Exception ex) when (attempt < 8)
        {
            app.Logger.LogWarning("Database bootstrap attempt {Attempt} failed: {Type}", attempt, ex.GetType().Name);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
app.UseCors("web");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/health", async (CrimeDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "online", database = "connected", service = "CRIMAP INSIGHT", timeUtc = DateTime.UtcNow })
        : Results.Problem("Database not reachable", statusCode: 503));
app.MapGet("/api/meta", async (CrimeDbContext db, CancellationToken ct) =>
{
    var synced = await db.Observations.AsNoTracking().Select(x => x.Year).Distinct()
        .OrderByDescending(x => x).ToListAsync(ct);
    return Results.Ok(new { availableYears = DatasetCatalog.Paths.Keys.OrderByDescending(x => x), syncedYears = synced,
        source = "경찰청 범죄 발생 지역별 통계", granularity = "연간 시군구별 집계",
        caveat = "발생 건수는 개인별 피해 위험도 또는 인구당 발생률을 의미하지 않습니다." });
});
app.MapGet("/api/regions", async (int year, CrimeDbContext db, CancellationToken ct) =>
{
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    var regions = await db.Observations.AsNoTracking().Where(x => x.Year == year)
        .Select(x => new { x.Region, x.Province }).Distinct().OrderBy(x => x.Province).ThenBy(x => x.Region)
        .ToListAsync(ct);
    return Results.Ok(regions);
});
app.MapGet("/api/categories", async (int year, CrimeDbContext db, CancellationToken ct) =>
{
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    var groups = await db.Observations.AsNoTracking().Where(x => x.Year == year)
        .Select(x => new { x.CategoryMajor, x.CategoryMinor }).Distinct()
        .OrderBy(x => x.CategoryMajor).ThenBy(x => x.CategoryMinor).ToListAsync(ct);
    return Results.Ok(groups);
});
app.MapGet("/api/summary", async (int year, string? major, CrimeDbContext db, CancellationToken ct) =>
{
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    var query = db.Observations.AsNoTracking().Where(x => x.Year == year);
    if (!string.IsNullOrWhiteSpace(major)) query = query.Where(x => x.CategoryMajor == major);
    var byCategory = await query.GroupBy(x => x.CategoryMajor)
        .Select(g => new { category = g.Key, count = g.Sum(x => (long)x.Count) })
        .OrderByDescending(x => x.count).ToListAsync(ct);
    var byRegion = await query.GroupBy(x => new { x.Region, x.Province })
        .Select(g => new { region = g.Key.Region, province = g.Key.Province, count = g.Sum(x => (long)x.Count) })
        .OrderByDescending(x => x.count).ToListAsync(ct);
    return Results.Ok(new { year, category = major, total = byCategory.Sum(x => x.count),
        regionCount = byRegion.Count, categories = byCategory, regions = byRegion,
        note = "동일 연도 내 범죄중분류의 합산입니다. 행정구역별 발생 건수이며 인구 보정 수치가 아닙니다." });
});
app.MapGet("/api/breakdown", async (int year, string region, string? major, CrimeDbContext db, CancellationToken ct) =>
{
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    var query = db.Observations.AsNoTracking().Where(x => x.Year == year && x.Region == region);
    if (!string.IsNullOrWhiteSpace(major)) query = query.Where(x => x.CategoryMajor == major);
    var rows = await query.Select(x => new { major = x.CategoryMajor, minor = x.CategoryMinor, count = x.Count })
        .OrderByDescending(x => x.count).ToListAsync(ct);
    return Results.Ok(new { year, region, total = rows.Sum(x => (long)x.count), categories = rows });
});
app.MapGet("/api/compare", async (int year, string regionA, string regionB, CrimeDbContext db, CancellationToken ct) =>
{
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    if (regionA == regionB) return Results.BadRequest(new { error = "서로 다른 지역을 선택하세요." });
    var selected = new[] { regionA, regionB };
    var rows = await db.Observations.AsNoTracking().Where(x => x.Year == year && selected.Contains(x.Region))
        .GroupBy(x => new { x.Region, x.CategoryMajor })
        .Select(g => new { region = g.Key.Region, category = g.Key.CategoryMajor, count = g.Sum(x => (long)x.Count) })
        .OrderBy(x => x.category).ToListAsync(ct);
    if (!rows.Any(x => x.region == regionA) || !rows.Any(x => x.region == regionB))
        return Results.NotFound(new { error = "비교할 지역 데이터가 존재하지 않습니다." });
    return Results.Ok(new { year, regionA, regionB, breakdown = rows,
        caveat = "절대 발생 건수 비교입니다. 인구 규모가 다르므로 지역의 안전도 순위로 해석할 수 없습니다." });
});
app.MapGet("/api/trends", async (string region, string? major, CrimeDbContext db, CancellationToken ct) =>
{
    var query = db.Observations.AsNoTracking().Where(x => x.Region == region);
    if (!string.IsNullOrWhiteSpace(major)) query = query.Where(x => x.CategoryMajor == major);
    var results = await query.GroupBy(x => x.Year)
        .Select(g => new { year = g.Key, count = g.Sum(x => (long)x.Count) })
        .OrderBy(x => x.year).ToListAsync(ct);
    return Results.Ok(new { region, category = major, points = results,
        caveat = "연도별 분류체계·관할구역 변경을 먼저 검증하세요. 미동기화 연도는 표시되지 않습니다." });
});
app.MapGet("/api/pipeline/runs", async (CrimeDbContext db, CancellationToken ct) =>
    Results.Ok(await db.IngestionRuns.AsNoTracking().OrderByDescending(x => x.StartedAtUtc)
        .Take(20).Select(x => new { x.Id, x.Year, x.Status, x.StartedAtUtc, x.CompletedAtUtc,
            x.RawRows, x.Observations, x.Regions, x.Sha256, x.ErrorSummary, x.SourceEndpoint })
        .ToListAsync(ct)));

app.MapPost("/api/admin/sync/{year:int}", async (int year, HttpContext context,
    IConfiguration config, IngestionService ingestion, CancellationToken ct) =>
{
    var expected = config["Admin:SyncKey"];
    if (string.IsNullOrWhiteSpace(expected))
        return Results.Problem("Admin__SyncKey가 구성되지 않았습니다.", statusCode: 503);
    var provided = context.Request.Headers["X-Admin-Key"].ToString();
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var providedBytes = Encoding.UTF8.GetBytes(provided);
    if (expectedBytes.Length != providedBytes.Length ||
        !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        return Results.Unauthorized();
    if (!DatasetCatalog.Paths.ContainsKey(year)) return Results.BadRequest(new { error = "지원하지 않는 연도" });
    var run = await ingestion.SynchronizeAsync(year, ct);
    if (run is null) return Results.Conflict(new { error = "이미 동기화 작업이 진행 중입니다." });
    return run.Status == "Failed" ? Results.Json(new { run, message = "동기화 실패. 기존 성공 데이터는 보존됩니다." }, statusCode: 422)
        : Results.Ok(new { run, message = run.Status == "Unchanged" ? "기존 데이터와 동일하여 변경하지 않았습니다." : "동기화를 완료했습니다." });
});

app.Run();

public partial class Program { }
