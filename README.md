# 공공데이터를 활용한 범죄 현황 웹사이트

**CRIMAP INSIGHT**는 경찰청의 지역별 범죄 발생 통계를 지도와 그래프로 보여주는 웹사이트입니다. 범죄가 얼마나 발생하는지 쉽게 확인하고, 다른 지역과 비교하며 범죄 예방에 관심을 가지도록 만드는 것을 목표로 했습니다.

> [데이터 출처: 공공데이터포털 경찰청 범죄 발생 지역별 통계](https://www.data.go.kr/data/3074462/fileData.do) · REST API(JSON)를 사용합니다. **실시간 사건 위치나 개인별 위험도를 제공하는 서비스는 아닙니다.**

## 이 프로젝트에서 중점적으로 만든 것

- **누구나 쉽게 보는 범죄 현황판:** 연도와 범죄 종류를 선택하면 총 발생 건수, 많이 발생한 범죄 종류, 지역별 순위를 보여줍니다.
- **범죄 지도:** 시·도별 발생 건수에 따라 색상을 달리 표시했습니다. 현재 버전은 실제 행정구역 지도가 아닌 **시·도 타일맵**입니다.
- **지역 상세·비교:** 지역을 고르면 범죄 종류별 건수와 연도별 변화를 확인하고, 두 지역을 비교할 수 있습니다.
- **안정적인 공공데이터 처리:** CSV 업로드 없이 경찰청 API 데이터를 수집하고, 잘못된 값·중복·누락을 검증해 MSSQL에 저장하도록 만들었습니다.
- **데이터 품질 확인:** SHA-256으로 같은 자료의 중복 수집을 방지하고, 트랜잭션과 처리 기록으로 성공·실패를 추적합니다.

## 사용 기술

| 구분 | 기술 | 사용한 곳 |
| --- | --- | --- |
| 프론트엔드 | **Vue 3, TypeScript, Vite, CSS, Lucide Icons** | 다크톤 범죄 현황판, 시·도 타일맵, 그래프, 지역 비교 |
| 백엔드 | **C#, ASP.NET Core 8 Minimal API** | 통계 조회 API, 데이터 동기화 API |
| 외부 데이터 | **경찰청 ODcloud REST API, HttpClient, System.Text.Json** | JSON 수집 및 파싱 |
| 데이터 처리 | **SHA-256, LINQ, EF Core 8** | 데이터 검증·정규화·중복 확인·트랜잭션 |
| 데이터베이스 | **Microsoft SQL Server 2022 (Docker)** | 범죄 통계와 동기화 이력 저장 |
| 개발·확인 | **Visual Studio 2022, VS Code, Swagger UI, xUnit, Git/GitHub** | 개발·디버깅·테스트·버전 관리 |

## 주요 화면

1. **현황판:** 연도·범죄 종류·지역 필터, 범죄 맵, 순위, 지역 상세
2. **지역 비교:** 두 지역의 범죄 종류별 발생 건수 비교
3. **데이터 확인:** API 동기화, 처리 상태, 원본 행 수와 저장 건수 확인
4. **이 사이트는?:** 데이터 출처, 사용 목적, 해석 시 주의사항 안내

## 데이터 처리 과정

```text
경찰청 ODcloud REST API (JSON)
       ↓
ASP.NET Core에서 페이지별 수집
       ↓
누락·음수·중복 등 데이터 검사
       ↓
지역 × 범죄 종류로 정규화
       ↓
SHA-256으로 내용 변경 확인
       ↓
MSSQL 트랜잭션 저장 + 처리 이력 기록
       ↓
조회 API → Vue 범죄 현황판
```

**2025년 로컬 동기화 확인 결과:** 원본 38행, 229개 지역, **통계 관측값 8,702개**. 8,702개는 실제 범죄 발생 건수가 아니라 DB에 저장된 지역별·분류별 데이터 개수입니다. 사용자 환경에서 DB 저장, 조회 API, React 프론트 연동까지 검증한 후 Vue로 변경했습니다. Vue 수정본의 최종 빌드와 화면 검증은 실행 환경에서 다시 확인해야 합니다.

## 폴더 구성

```text
CRIMAP_INSIGHT/
├── backend/
│   ├── CrimeInsight.Api/        # ASP.NET Core REST API, 수집·검증·저장
│   └── CrimeInsight.Tests/      # xUnit 테스트
├── frontend/                   # Vue 3 + TypeScript
│   └── src/
│       ├── App.vue             # 현황판·지역 비교·데이터 확인
│       ├── components/         # 지도·추이 그래프
│       ├── lib/api.ts          # 백엔드 API 호출
│       └── styles.css
├── docs/                       # API 및 설계 설명
├── scripts/                    # 백엔드 실행·API 확인 스크립트
├── docker-compose.yml          # SQL Server 2022
└── CRIMAP_INSIGHT.sln          # Visual Studio 솔루션
```

## 로컬 실행 방법

**필요 환경:** .NET 8 SDK, Node.js 20 이상, Docker Desktop, 경찰청 Open API 일반 인증키.

1. `cp .env.example .env` (Windows PowerShell: `Copy-Item .env.example .env`) 후 `.env`에 `MSSQL_SA_PASSWORD`를 설정합니다.
2. 프로젝트 루트에서 `docker compose up -d`를 실행합니다. SQL Server의 Windows 접속 주소는 **`127.0.0.1,11433`**입니다.
3. ASP.NET Core **User Secrets** 또는 로컬 환경변수로 다음 값을 입력합니다. **GitHub에 키나 비밀번호를 올리지 마세요.**

```text
ConnectionStrings__CrimeInsight=Server=127.0.0.1,11433;Database=CrimeInsightDB;User Id=crimap_app;Password=<DB_PASSWORD>;Encrypt=True;TrustServerCertificate=True;
OpenData__ServiceKey=<ODCLOUD_GENERAL_API_KEY>
Admin__SyncKey=<YOUR_ADMIN_KEY>
```

처음 설치 시 SQL Server에 `CrimeInsightDB` 및 전용 로그인 `crimap_app`을 만들어 권한을 부여해야 할 수 있습니다. 개발 환경에서 앱은 EF Core `EnsureCreatedAsync()`로 테이블 생성을 시도합니다.

4. 백엔드: `dotnet run --project .\backend\CrimeInsight.Api --launch-profile http`
5. 백엔드 확인: `http://localhost:5088/swagger` 및 `/api/health`
6. 프론트엔드: `cd frontend` → `npm install` → `npm run dev`
7. 브라우저: `http://localhost:5173` → 데이터 확인 화면에서 관리자 키로 원하는 연도 동기화

## 통계 해석 시 주의사항

이 데이터는 **해당 연도에 기록된 지역별 발생 건수**입니다. 실제 사건 위치나 현재 위험한 장소를 나타내지 않습니다. 지역 인구나 방문객 수가 다르므로 단순 발생 건수로 어느 지역이 더 위험한지 단정할 수 없습니다.

## 개발하면서 중요하게 생각한 점

화면에 숫자를 표시하는 데서 끝내지 않고, **외부 API에서 받은 데이터를 어떻게 검사하고 저장했는지** 직접 설명할 수 있도록 구성했습니다. 또한 데이터를 다시 수집해도 중복 저장되지 않는지, 오류가 나면 기존 자료가 보존되는지, 실제 DB와 조회 API가 연결되는지를 확인하는 과정에 중점을 두었습니다.
