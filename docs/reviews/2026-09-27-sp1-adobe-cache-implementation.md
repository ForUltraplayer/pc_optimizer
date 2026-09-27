# SP1 T10 일부 — Adobe 기본 미디어 캐시 정리

구현 커밋 `4a7b17c` (기준 `4eb6aac`), 브랜치 `codex/sp1-actions`. 사용자 지시대로 기능 구현을 우선하며 독립 리뷰/서브에이전트는 생략했다. 아래는 구현자 자체 검증이며 기존 리뷰의 독립 승인 상태를 변경하지 않는다.

## 구현 범위

- `AdobeCacheTargets.cs`: 현재 등록 프로필의 기본 Roaming/Adobe/Common/Media Cache Files(`.cfa`, `.pek`)와 Peak Files(`.pek`)만 서로 다른 카탈로그로 제공한다. 생성·수정 시각 모두 90일 이상 지난 파일만 선택한다. 오래됐다는 것이 미사용을 증명하지는 않는다.
- 프로필/Known Folder가 맞지 않거나 카탈로그가 없으면 거절한다. 캐시 위치 설정 형식을 추측하거나 다른 경로로 대체하지 않는다. 사용자 지정 캐시 위치는 여전히 미지원이며 기본 위치에 남아 있는 파일만 별도로 확인한다. DB `Media Cache`, `.ims`, 프로젝트/렌더/원본 파일은 이번 패턴에 포함하지 않았다.
- `UserTempTargets.ProtectionFor`로 기존 보호 정책·다른 SID 경계를 공유한다. 기존 긴 경로 정규화와 NativeFileCleanupPlatform의 부모 핸들 고정·파일 ID·hardlink/정션/placeholder 거절을 그대로 사용한다.
- `AdobeProcessGuard.cs`: 프로세스 이름만 조회한다. Premiere/Media Encoder/Audition/AfterFX/aerender/공유 Dynamic Link 보조 프로세스 등 알려진 작성자가 실행 중이거나 열거 실패/빈 결과이면 거절한다. 다른 세션도 보수적으로 포함한다. 프로그램 종료/시작·명령줄 수집은 없다.
- `FileCleanupAdapter`: 준비 전후·실행 시작·파일별 쓰기 직전에 앱 상태를 확인한다. 앱이 중간에 실행되면 남은 파일을 처리하지 않으며 이미 삭제한 수/건너뜀/실패·공간 변화를 보존한다. 첫 쓰기 전 거절은 Started=false. 고정 파일 집합·새 파일 제외·배타 파일 핸들·일회성 계획·공통 관문은 유지한다.
- App/App.xaml.cs에 AppFiles 실행기를 등록하고 두 선택 항목을 제공한다. Adobe 앱 카드의 **Adobe 기본 캐시 정리 대상 확인**은 미디어 캐시 미리보기를 새로 만들며, 실제 삭제에는 별도 실행 확인이 필요하다. 파형 폴더는 작업 선택 창에서 별도로 선택한다. SystemOnly/사용자 미확인에는 카드 실행을 제공하지 않는다.
- `ActionPresentation`은 준비 거절과 부분 삭제 후 앱 실행/조회 실패를 구분한다. 다음 미디어 사용 때 캐시 재생성 비용과 되돌릴 수 없는 삭제를 확인 화면에 표시한다. 진단 카드의 전체 관측 크기를 삭제량으로 사용하지 않는다.

## 근거와 한계

- [Adobe 미디어 캐시 관리](https://helpx.adobe.com/premiere/desktop/troubleshooting/media-issues/manage-media-cache.html): `.cfa`/`.pek`, Media Cache Files와 별도 DB의 구분, 필요할 때 캐시 재생성.
- [Adobe 기본 위치와 수동 정리](https://helpx.adobe.com/premiere/desktop/troubleshooting/playback-issues/choppy-playback-and-poor-performance-issue.html): Windows 기본 Common 위치와 사용자가 옮길 수 있다는 근거.
- [Adobe 나이 기준 정리](https://helpx.adobe.com/premiere/desktop/troubleshooting/media-issues/automatically-manage-your-media-cache-files.html): 앱 자체 관리의 확장자 제한과 기본 90일 설정 참고. 제품의 생성+수정 시각 동시 제한·DB 제외·명시적 확인은 별도 구현 결정이다. Adobe 공식 도구를 호출하는 기능으로 표현하지 않는다.
- 프로세스 이름 관측과 파일 잠금은 모든 버전/이름 변경/미래 작성 프로세스 부재를 보장하는 OS 예약 잠금이 아니다. 마지막 관측 직후 앱을 새로 실행하는 경합을 완전히 막지 못한다. 정리 중 관련 앱을 열지 않도록 안내하며 배타 핸들을 얻지 못한 파일은 지우지 않는다. 실제 Adobe 버전별 작업 재개/캐시 재생성 평가는 아직 하지 않았다.
- 사용자 실제 캐시·설정은 변경하지 않았다. Steam/NVIDIA 및 사용자 지정 Adobe 경로는 T10 잔여다. T6 시작 프로그램·T9 Update/DO·T12 실환경 평가는 별도로 남는다.

## 검증

1. 신규 단위/화면 연결 32/32: `dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'FullyQualifiedName~AdobeCacheCleanupTests|FullyQualifiedName~AdobeCardHasPreparationButtonOnlyForFullScope' --logger 'trx;LogFileName=sp1-adobe-focused-final.trx'`.
2. 전체 기본 1490/1490: `dotnet test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-adobe-basic.trx'`.
3. Release 경고 0/오류 0: `dotnet build PcOptimizer.sln -c Release --no-restore`.
4. 관리자 전체 Smoke 44/44: `dotnet test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-adobe-smoke.trx'`. 새 네이티브 소유 fixture 삭제 및 호스트 프로세스 이름 조회 2건 포함. Online/ToolSmoke는 해당 도구/네트워크 코드 변경이 없어 이번에 재실행하지 않았다.

TRX는 `tests/PcOptimizer.Tests/TestResults/`에 있다. 신규 테스트는 90일/확장자/프로젝트·DB 제외, 계획 이후 파일, 준비 전후/쓰기 직전 앱 실행, 중간 실패의 부분 개수, 잠금/정션/hardlink/보호, 다른 사용자 범위, 임의 키/옮겨진 Roaming 거절, 실제 ActionWorkflow의 별도 확인과 재검사, 실제 XAML 버튼 노출을 다룬다. Smoke는 GUID 소유 fixture에만 실제 삭제를 시행하고 호스트 프로세스는 조회만 한다.

초기 신규 테스트 29/29 후 화면 연결 테스트 2건이 실패했다. 테스트가 Adobe 외의 확인 불가 카드에 생성된 숨겨진 템플릿 버튼까지 함께 찾았기 때문이다. AppCache 카드로 조건을 좁혀 실제 Full/SystemOnly 버튼을 확인했고 최종 32/32와 전체 기본 검증을 통과했다. 소스 변경을 정당화하기 위해 기대 결과를 완화하지 않았다.

`artifacts/sp1-adobe/adobe-card-action.png`를 오프스크린 렌더링하고 직접 확인했다. 실제 창 표시나 사용자 키보드 입력 없이 Adobe 카드 버튼을 확인했으며 사용자 환경의 모든 DPI/사용성 검증으로 간주하지 않는다.

## 배포 완료

`tools/package.ps1 -OutputDirectory dist/sp1-evaluation`의 locked restore/publish와 고지·규칙·원본 해시·아카이브 검증이 통과했다.

- ZIP: `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.4-win-x64.zip`.
- 최상위 4개, 내부 13파일, EXE 65,152,498 bytes.
- SHA-256: `44E4D6FB82CABC7E367240866411E2D6EF8CC3C1FAFF6F57B8D0819CCCB94D9B`.
- 이전 ZIP과 실행 중인 앱을 보존했다. master 병합·원격 게시 없음. 자체 검증 완료이며 Adobe 실사용/다른 계정/.NET 없는 PC까지 출시 검증을 완료했다는 뜻은 아니다.
