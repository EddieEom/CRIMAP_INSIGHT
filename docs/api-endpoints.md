# REST API 계약

기본 URL: `http://localhost:5088/api`

| Method | Path | 설명 |
|---|---|---|
| GET | `/health` | 서버 가동 확인 |
| GET | `/meta` | 제공 연도 및 DB 저장 연도 |
| GET | `/regions?year=2025` | 지역 목록 |
| GET | `/categories?year=2025` | 범죄 대/중분류 |
| GET | `/summary?year=2025&major=절도범죄` | 지역·대분류별 발생 건수 합계 |
| GET | `/breakdown?year=2025&region=부산%20남구` | 지역의 범죄 중분류별 발생 건수 |
| GET | `/compare?year=2025&regionA=부산%20남구&regionB=부산%20해운대구` | 두 지역의 대분류별 비교 |
| GET | `/trends?region=부산%20남구` | DB 동기화된 연도 기준 추이 |
| GET | `/pipeline/runs` | 최근 20건의 적재 이력 |
| POST | `/admin/sync/2025` | **X-Admin-Key** 헤더 인증 후 ODcloud API 수집/검증/DB 반영 |

오류코드: 잘못된 연도 400, 지역 데이터 없음 404, 관리자 키 오류 401, 동시 수집 409, 검증 또는 외부 API 오류 422, 관리자 키 미설정 503.

`POST /admin/sync/2025` 예시 (PowerShell):

```powershell
$adminKey = Read-Host 'Enter your own Admin__SyncKey'
Invoke-RestMethod -Method Post -Uri 'http://localhost:5088/api/admin/sync/2025' -Headers @{ 'X-Admin-Key' = $adminKey }
```

`data.go.kr` 키는 브라우저에서 보내지 않습니다. 백엔드의 `OpenData__ServiceKey`에만 설정합니다.
