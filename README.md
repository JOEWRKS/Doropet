# Doropet · 도로롱

A small Windows desktop pet based on Dororong (Doro), a fan character derived from **GODDESS OF VICTORY: NIKKE**.

**승리의 여신: 니케**에서 파생된 팬 캐릭터 **도로롱(Doro)**을 바탕으로 만든 Windows 데스크톱 펫입니다.

## 한국어

### 기능

- 걷기·숨쉬기·눈 깜빡임·수면 등 자율 애니메이션
- 마우스 방향 추적, 가까이 머물면 준비 자세 후 짧은 도약
- 머리 잡아 이동, 볼 당기기와 탄성 복원·반동
- 창·작업표시줄 가장자리에 매달리기, 매달린 앞발 당기기·한쪽 발 휘젓기
- 앉기 메뉴, 트레이 종료·로그 폴더 열기, 중복 실행 방지

### 사용법

1. **Windows x64**에서 설치 파일을 실행하거나, 포터블 ZIP 전체를 풀고 `Dororong.exe`를 실행합니다. 배포용 패키지는 .NET 런타임을 포함합니다.
2. 머리를 드래그해 이동합니다. 가장자리에서 앞발 준비 동작이 보일 때 놓으면 매달립니다.
3. 볼은 드래그하면 제자리에서 늘어나고 놓으면 복원됩니다. 매달린 앞발은 클릭하거나 당길 수 있습니다.
4. 마우스를 가까이 두면 쫓아보고, 더 가까운 범위에 머물면 폴짝 뜁니다.
5. 캐릭터 우클릭 → **앉아 / Exit**, 트레이 우클릭 → **로그 폴더 열기 / 종료**를 사용합니다.

설치는 관리자 권한 없이 사용자 계정에만 적용됩니다. 업데이트 전에는 트레이에서 종료하세요. 제거는 Windows 앱 목록의 **도로롱 (Dororong)**에서 합니다.

### 상태

현재 **0.1.0 개발 빌드**입니다. 설치본 교체와 실행을 검증했지만, 코드 서명·깨끗한 PC·버전 간 업그레이드 검증 및 캐릭터 배포 권한 확인은 남아 있습니다. 자동 업데이트·자동 시작·계정·데이터 전송 기능은 없습니다.

## English

### Features

- Autonomous walking, breathing, blinking and sleeping
- Mouse tracking and a short pounce after the pointer lingers nearby
- Head dragging, stretchy cheeks and spring-back reactions
- Window/taskbar edge hanging, with individual front-paw pulls and waves
- Sit command, tray controls, local logs and single-instance protection

### Usage

1. On **Windows x64**, run the installer or extract the entire portable ZIP and launch `Dororong.exe`. Packaged builds include the .NET runtime.
2. Drag the head to move the pet. Release near a compatible edge when the front-paw readiness animation appears to hang.
3. Pull a cheek to stretch it in place; release to spring back. Click or drag a hanging front paw to interact with that side.
4. Move the pointer nearby to attract attention; linger closer to trigger a short pounce.
5. Right-click the character for **앉아 (Sit) / Exit**, or the tray icon for **로그 폴더 열기 (Open logs) / 종료 (Exit)**.

Installation is per-user and requires no administrator rights. Exit through the tray before updating. Uninstall **도로롱 (Dororong)** through Windows' app list.

### Status

This is a **0.1.0 development build**. Installed updates and startup have been checked; code signing, clean-machine and cross-version acceptance, and character distribution permissions remain pending. No auto-update, autostart, accounts or data upload.

## Structure · 구성

| Path | Purpose · 역할 |
| --- | --- |
| `src/Dororong.App` | WPF UI, rendering and Windows integration · 화면·렌더링·Windows 연동 |
| `src/Dororong.Core` | Behavior and platform logic · 행동·발판 로직 |
| `tests` | Regression and packaging checks · 회귀·패키지 검사 |
| `tools` / `installer` | Animation tools, publishing and installer · 애니메이션 도구·패키징·설치 |
| `docs` | Design and verification notes · 설계·검증 기록 |

## Development · 개발

Windows + .NET 8-compatible SDK:

```powershell
dotnet restore DororongDesktopPet.sln
dotnet run --project src/Dororong.App/Dororong.App.csproj -c Release
```

[Build, test & package · 빌드·검사·패키징](docs/BUILD.md) · [Task log · 작업 기록](TASKS.md)

## License & credits · 라이선스와 출처

Original project source code is available under the [MIT License](LICENSE), allowing modification, redistribution and commercial use of that code with the required notices. **Character art, sprites, derived animation assets, names and trademarks are excluded**; their rights remain with their respective owners. Third-party dependencies retain their own licenses.

직접 작성한 프로젝트 소스 코드는 [MIT 라이선스](LICENSE)로 공개하며, 고지 유지 조건으로 수정·재배포·코드의 상업적 이용을 허용합니다. **캐릭터·원본 이미지·스프라이트·파생 애니메이션 리소스·명칭·상표는 MIT 적용 대상이 아니며**, 권리는 각 권리자에게 있습니다. 외부 의존성에는 각자의 라이선스가 적용됩니다.

Dororong artwork here is adapted from user-supplied reference images of the NIKKE-derived fan character, not an original character created by this project. This is an **unofficial, non-commercial fan-made derivative work**, not affiliated with or endorsed by NIKKE's developers or publishers. The original fan artist and asset distribution permissions have not yet been verified. The code license does not authorize commercial use or redistribution of the character assets.

사용된 도로롱은 프로젝트가 창작한 독자 캐릭터가 아니라, 사용자가 제공한 NIKKE 파생 팬 캐릭터 이미지를 바탕으로 제작했습니다. **상업적 목적 없이 만든 비공식 팬메이드 2차창작**이며, NIKKE 개발사·배급사의 공식 승인이나 제휴를 의미하지 않습니다. 원 팬아트 작가 및 이미지 배포 허락은 아직 확인되지 않았습니다. 코드의 MIT 라이선스는 캐릭터 리소스의 상업적 이용이나 재배포 권한을 부여하지 않습니다.

NIKKE official copyright notice · NIKKE 공식 저작권 표기: © Proxima Beta Pte. Ltd. © SHIFT UP CORP. ([Official site · 공식 사이트](https://www.nikke-kr.com/indexm.html)). Attribution does not grant redistribution rights. 출처 표기는 재배포 허락을 대신하지 않습니다.
