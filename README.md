# 공공데이터를 활용한 범죄 현황 웹사이트

**CRIMAP INSIGHT**는 경찰청에서 제공하는 범죄 발생 지역별 통계를 지도와 그래프로 보여주는 웹사이트입니다. 사용자가 연도와 지역, 범죄 종류를 선택해 발생 건수를 확인하고 다른 지역과 비교할 수 있도록 만들었습니다.

> 데이터 출처: [공공데이터포털 - 경찰청 범죄 발생 지역별 통계](https://www.data.go.kr/data/3074462/fileData.do)  
> 데이터 특성: **연간·지역별 통계**이며, 실시간 범죄 신고나 개별 사건 위치 정보가 아닙니다.

## 개발 목적

이 프로젝트에서 중점적으로 만들고 싶었던 것은 **누구나 쉽게 이해할 수 있는 범죄 현황판**과 **공공데이터를 안정적으로 처리하는 백엔드**입니다.

1. **쉽게 보는 범죄 현황:** 지역별 발생 건수, 많이 발생한 범죄 종류, 지역 순위를 한 화면에서 확인할 수 있도록 구성했습니다.
2. **범죄 통계 지도:** 시·도별 발생 건수를 색으로 구분해 표시하고, 지역을 선택하면 관련 통계를 찾아볼 수 있도록 했습니다. 현재 지도는 실제 행정구역 경계가 아닌 **시·도 타일맵**입니다.
3. **지역 비교:** 서로 다른 두 지역의 범죄 종류별 발생 건수를 비교할 수 있도록 했습니다.
4. **외부 API 데이터 처리:** CSV 파일을 직접 업로드하지 않고 경찰청 **REST API(JSON)**에서 자료를 가져와 검사한 뒤 MSSQL에 저장하도록 구성했습니다.
5. **데이터 관리:** 누락·중복·잘못된 값 검사, 같은 데이터 재수집 방지, 실패 시 기존 데이터 보존, 수집 기록 조회를 구현했습니다.

## 사용 기술

| 구분 | 사용 기술 | 사용 목적 |
| --- | --- | --- |
| 프론트엔드 | **Vue 3, TypeScript, Vite, CSS, Lucide Icons** | 범죄 현황판, 시·도 타일맵, 통계 그래프, 지역 비교 화면 |
| 백엔드 | **C#, ASP.NET Core 8 Minimal API** | 통계 조회 API 및 공공데이터 수집 API |
| 데이터 처리 | **HttpClient, System.Text.Json, SHA-256** | JSON 수집·정규화·검증, 데이터 변경 여부 확인 |
| 데이터베이스 | **Microsoft SQL Server 2022, EF Core 8** | 통계 데이터, 지역·범죄 분류, 수집 이력 저장 |
| 개발 환경 | **Visual Studio 2022, VS Code, Docker Desktop, Git/GitHub** | 개발·디버깅·DB 컨테이너·버전 관리 |
| 테스트 | **xUnit, Swagger UI** | 데이터 처리 로직 테스트 및 REST API 확인 |

## 주요 기능

| 화면 | 기능 |
| --- | --- |
| 범죄 현황판 | 연도·범죄 종류·지역 검색, 전체 발생 건수, 주요 범죄 종류 표시 |
| 범죄 맵 | 시·도별 건수에 따라 색상이 달라지는 타일맵 |
| 지역별 순위 | 발생 건수 순위와 선택 지역의 세부 통계 확인 |
| 지역 비교 | 두 지역의 범죄 종류별 발생 건수 비교 |
| 데이터 확인 | API 수집 결과, 적재 건수, 상태 및 작업 이력 확인 |

### 공공데이터 처리 흐름

```text
경찰청 공공데이터 REST API (JSON)
        ↓
ASP.NET Core: API 호출 및 페이지별 데이터 수집
        ↓
데이터 검사 (누락 / 중복 / 잘못된 값)
        ↓
범죄 종류 × 지역을 한 건씩 저장할 수 있는 구조로 변환
        ↓
SHA-256으로 변경 여부 비교
        ↓
MSSQL 저장 (트랜잭션 처리, 수집 이력 기록)
        ↓
ASP.NET Core 조회 API → Vue 현황판
```

**2025년 동기화 실행 결과 예시:** 원본 38행, 지역 229개, 정규화된 관측값 8,702개를 수집·저장했습니다. 여기서 **8,702개는 범죄 발생 건수가 아니라 DB에 저장한 통계 관측값의 개수**입니다.

## 프로젝트 구조

```text
CRIMAP_INSIGHT/
├── frontend/
│   └── src/
│       ├── App.vue                    # 범죄 현황판, 비교, 데이터 확인
│       ├── components/                # 타일맵 및 추이 그래프
│       ├── lib/api.ts                 # 백엔드 API 호출
│       └── styles.css                 # 반응형 UI
├── backend/
│   ├── CrimeInsight.Api/
│   │   ├── Program.cs                 # REST API 및 서버 설정
│   │   ├── Services/                  # 공공데이터 수집·검증·저장
│   │   ├── Data/                      # EF Core DB 설정
│   │   └── Domain/                    # 데이터 모델
│   └── CrimeInsight.Tests/            # xUnit 테스트
├── docs/                              # 구조 및 API 설명
├── scripts/                           # 실행·점검 스크립트
├── docker-compose.yml                 # SQL Server 2022
└── CRIMAP_INSIGHT.sln                 # Visual Studio 솔루션
```

## 로컬 실행 방법

### 1. 준비물

.NET 8 SDK, Node.js 20 이상, Docker Desktop, 경찰청 공공데이터 **오픈 API 일반 인증키**가 필요합니다.

### 2. SQL Server 실행

프로젝트 루트에서 `.env.example`을 `.env`로 복사하고 `MSSQL_SA_PASSWORD`에 본인이 정한 강력한 비밀번호를 입력합니다.

```powershell
Copy-Item .env.example .env
notepad .env
docker compose up -d
docker compose ps
```

Docker의 SQL Server는 **`127.0.0.1,11433`**에서 접근하도록 설정되어 있습니다.

### 3. 백엔드 설정 및 실행

Visual Studio에서 `CRIMAP_INSIGHT.sln`을 열고 `CrimeInsight.Api` 프로젝트의 **사용자 암호 관리(User Secrets)**에서 설정합니다. 실제 인증키·비밀번호는 GitHub에 올리지 않습니다.

```json
{
  "ConnectionStrings": {
    "CrimeInsight": "Server=127.0.0.1,11433;Database=CrimeInsightDB;User Id=crimap_app;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;"
  },
  "OpenData": { "ServiceKey": "YOUR_ODCLOUD_GENERAL_SERVICE_KEY" },
  "Admin": { "SyncKey": "YOUR_PRIVATE_ADMIN_KEY" }
}
```

`crimap_app`은 로컬 SQL Server에 따로 생성한 사용자이며, 개발 환경에서 데이터베이스 접근 권한이 필요합니다. 이후 백엔드를 실행합니다.

```powershell
dotnet run --project .\backend\CrimeInsight.Api --launch-profile http
```

API 문서: `http://localhost:5088/swagger`  
DB 연결 확인: `http://localhost:5088/api/health`

### 4. 데이터 동기화

PowerShell 새 창에서 관리자 키를 입력하고 2025년 데이터를 동기화합니다.

```powershell
$adminKey = Read-Host "관리자 키"
Invoke-RestMethod -Uri "http://localhost:5088/api/admin/sync/2025" -Method Post -Headers @{ "X-Admin-Key" = $adminKey }
```

동기화가 성공하면 통계 조회 API로 저장된 자료를 확인할 수 있습니다.

### 5. 프론트엔드 실행

```powershell
cd frontend
npm install
npm run dev
```

웹사이트: **http://localhost:5173**

### 6. 테스트

```powershell
dotnet test .\backend\CrimeInsight.Tests\CrimeInsight.Tests.csproj
cd frontend
npm run build
```

## 구현하면서 확인한 점

- Windows에서 SQL Server 기본 포트 `1433`을 사용할 수 없어 Docker 외부 포트를 `11433`으로 변경했습니다.
- Windows에 설치된 SQL Server와 Docker SQL Server는 별도 인스턴스이므로 계정과 DB를 구분해 연결했습니다.
- 원본 API 자료는 범죄 종류가 행, 지역이 열로 구성되어 있어 지역별로 검색하고 비교할 수 있는 형태로 변환했습니다.
- API 수집 성공 여부뿐 아니라 DB 저장 결과와 조회 API, 프론트엔드 화면이 실제로 이어지는지 확인했습니다.

## 데이터 해석 시 주의사항

- 이 사이트는 **범죄 발생 건수**를 보여주며 특정 지역의 안전도를 판단하거나 범죄를 예측하는 서비스가 아닙니다.
- 연간 집계 자료이므로 **실시간 위험 경보가 아닙니다**.
- 인구가 많은 지역일수록 총 발생 건수도 많을 수 있습니다. 발생 건수만으로 개인의 피해 가능성을 비교할 수는 없습니다.
- 시·도 타일맵은 지역의 상대적 위치를 단순화한 화면이며 실제 범죄 발생 장소를 표시하지 않습니다.

자세한 서버 설계는 [docs/architecture.md](docs/architecture.md), API 경로는 [docs/api-endpoints.md](docs/api-endpoints.md)에서 확인할 수 있습니다.