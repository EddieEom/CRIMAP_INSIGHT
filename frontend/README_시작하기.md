CRIMAP INSIGHT - Vue Alert Dashboard Edition

변경 방향
- 홍보성 랜딩 페이지 느낌 제거
- 범죄 현황판 / 경각심 대시보드 느낌으로 개편
- 쉬운 문장, 학부생 프로젝트 느낌 유지
- 첫 화면에 범죄 맵, 상위 지역 순위, 범죄 유형 비중, 지역 상세 배치

실행 방법
1. backend ASP.NET Core API를 먼저 실행한다.
2. 이 frontend 폴더에서 아래 명령어 실행
   npm install
   npm run dev
3. 브라우저에서 http://localhost:5173 접속

비고
- API 기본 경로는 상대경로 /api 이며, Vite 프록시 설정을 사용한다.
- 범죄 맵은 실제 행정 경계 GIS 지도가 아니라 시도 단위 타일맵이다.
