<!--
  @file    : THIRD-PARTY-NOTICES.md
  @author  : rudals252
  @brief   : PcOptimizer.exe에 포함된 서드파티 구성 요소와 규칙 데이터의 저작권·라이선스 고지
-->

# 서드파티 고지 (Third-Party Notices)

PC Optimizer 배포 파일(`PcOptimizer.exe`, 런타임 포함 단일 실행 파일)에는 아래 구성 요소가 들어 있습니다.

| 구성 요소 | 버전 | 저작권 | 라이선스 |
|---|---|---|---|
| .NET 런타임·Windows Desktop(WPF) | 10.0.12 | .NET Foundation and Contributors | MIT |
| System.Management, System.CodeDom (NuGet 패키지) | 10.0.12 | .NET Foundation and Contributors | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | .NET Foundation and Contributors | MIT |
| WPF-UI (`Wpf.Ui`, `Wpf.Ui.Abstractions`) | 4.3.0 | Leszek Pomianowski and WPF UI Contributors | MIT |
| winapp2.ini 규칙 스냅샷 | 커밋 `53ae419` | the winapp2 project | CC-BY-SA-4.0 |

- 프로젝트 주소: .NET <https://github.com/dotnet/runtime>, <https://github.com/dotnet/wpf> (System.Management·System.CodeDom도 dotnet/runtime) · CommunityToolkit.Mvvm <https://github.com/CommunityToolkit/dotnet> · WPF-UI <https://github.com/lepoco/wpfui>
- 실행 파일에 포함된 .NET 런타임·WPF가 쓰는 제3자 구성 요소의 고지: <https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT>, <https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT>
- WPF-UI 어셈블리에는 그 프로젝트가 포함한 구성 요소(VirtualizingWrapPanel, Fluent UI System Icons 글꼴 등, 각각 MIT)가 들어 있으며 고지는 WPF-UI 패키지의 `ThirdPartyNotices.txt`를 따릅니다. PC Optimizer는 이 아이콘 글꼴을 직접 사용하지 않습니다.
- 앱 아이콘은 PC Optimizer가 직접 그린 것입니다(`tools/make-icon.ps1`). 별도 글꼴은 포함하지 않으며 Windows 기본 글꼴(Segoe UI, 맑은 고딕)을 사용합니다.

## winapp2.ini (CC-BY-SA-4.0)

앱 캐시 관측 규칙은 winapp2 프로젝트의 `Winapp2.ini`를 바탕으로 하며, 실행 파일 안에 리소스로 포함됩니다. 저작자 표시·변경 사항·같은 조건으로 배포하는 파일은 함께 제공하는 `winapp2-CC-BY-SA-4.0.md`(저장소의 `rules/LICENSE-winapp2.md`)에 적었습니다. 규칙 파일 원문(`winapp2.ini`, `supplement.ini`, `rule-metadata.json`)은 배포 zip의 `rules/` 폴더에 함께 들어 있으며(앱은 실행 중 이 사본을 읽지 않고 실행 파일에 포함된 같은 내용을 사용), 출처·SHA-256은 같은 폴더의 `sources.json`에 있습니다.

라이선스 전문: <https://creativecommons.org/licenses/by-sa/4.0/legalcode>

## MIT License

위 표에서 MIT로 표시한 구성 요소는 각 저작권자의 아래 조건에 따라 배포됩니다.

```
MIT License

Copyright (c) .NET Foundation and Contributors
Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 포터블 ZIP의 추가 원문 고지

패키징 시 복원된 런타임·WPF-UI 버전의 원문을 LICENSES/에 복사합니다:
`dotnet-THIRD-PARTY-NOTICES.txt`, `dotnet-LICENSE.txt`, `windowsdesktop-LICENSE.txt`,
`WPF-UI-ThirdPartyNotices.txt`, `WPF-UI-LICENSE.md`. 앱은 이 문서를 실행 입력으로 사용하지 않습니다.
