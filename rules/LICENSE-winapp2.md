<!--
  @file    : LICENSE-winapp2.md
  @author  : rudals252
  @brief   : 포함한 winapp2.ini 스냅샷과 같은 형식의 보충 규칙 파일의 저작자 표시·라이선스·변경 사항 고지
-->

# winapp2.ini 저작자 표시와 라이선스

## 원본

- 파일: `rules/winapp2.ini`
- 저작자: the winapp2 project (<https://github.com/MoscaDotTo/Winapp2>)
- 원본 위치: <https://raw.githubusercontent.com/MoscaDotTo/Winapp2/master/Non-CCleaner/Winapp2.ini>
- 원본 커밋: `53ae41946c3d3c8bb348d81081c34b6050f0b561` (2026-09-15T10:41:40Z, 파일 머리글 `Version: 260915`, 항목 4,068개)
- 내려받은 시각: 2026-09-26T11:25:03Z (1회, 앱은 실행 중에 규칙을 내려받지 않음)
- 라이선스: Creative Commons Attribution-ShareAlike 4.0 International (CC-BY-SA-4.0)
  - 원본 라이선스 안내: <https://github.com/MoscaDotTo/Winapp2/blob/master/License.md>
  - 라이선스 전문: <https://creativecommons.org/licenses/by-sa/4.0/legalcode>

## 변경 사항

- 줄 끝을 CRLF에서 LF로 바꿨다. 그 밖의 내용은 바꾸지 않았다.
- 원본 그대로의 SHA-256(`upstreamSha256`)과 포함 파일의 SHA-256(`sha256`)은 `rules/sources.json`에 기록했다.

## 같은 조건으로 배포하는 파일

- `rules/supplement.ini`, `rules/rule-metadata.json`: PC Optimizer가 작성한 보충 규칙과 검토 메타데이터. winapp2.ini 형식을 따르며, 프로젝트 정책(설계 문서 2장)에 따라 CC-BY-SA-4.0으로 배포한다.

## 사용 방식

PC Optimizer 1차는 이 규칙을 파일 크기 관측 범위를 정하는 데만 쓴다. 파일 삭제, 레지스트리 변경(RegKey), 폴더 삭제 지시(REMOVESELF) 실행은 하지 않는다. 같은 INI 형식을 쓴다는 사실만으로 다른 규칙 파일의 라이선스 의무를 추론하지 않는다.
