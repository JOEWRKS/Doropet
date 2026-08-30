# Dororong M1 표현·애니메이션 Stage A 체크포인트 보고서

- 기록일: 2026-08-30 (Asia/Seoul)
- 저장소: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1-expression-animation`
- 브랜치: `feature/dororong-m1-expression-animation`
- 문서 작성 시 기준 HEAD: `85f166160bba6593364e48617336571d94319c32`
- 원격 저장소: `https://github.com/JOEWRKS/Doropet.git`
- 상위 기준: phase-1 checkpoint `cc04e67e8cc330d9afe0af607b66188527a42cd4`
- 관련 Draft PR: `#1` — phase-1 보존용이며 전체 M1 완료 PR이 아님
- 기준 로드맵: `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`
- 기준 제품 명세: `docs/specs/2026-08-26-dororong-m1-design.md`

## 1. 이 문서의 목적

이 문서는 phase-1 몸선 보정 이후 시작한 표정·애니메이션 작업 가운데, 열린 눈·중간 눈·완전히 감은 눈의 세 정적 프레임을 사용자 제공 이미지로 교체한 현재 시점까지를 하나의 체크포인트로 정리한다.

다음 작업자가 대화 내용을 다시 추측하지 않아도 되도록 아래 내용을 한곳에 남긴다.

1. 이미 확정된 제품 방향과 절대 보존 조건.
2. 눈 감기 표현에서 어떤 접근을 시도했고 왜 실패했는지.
3. 사용자가 직접 그려 제공한 세 프레임을 어떻게 런타임에 적용했는지.
4. 현재 파일·해시·빌드·테스트·게시 산출물의 정확한 상태.
5. 통과한 범위와 아직 통과하지 않은 범위.
6. 다음 단계에서 바로 이어서 할 작업과 다시 건드리면 안 되는 부분.

이 문서는 내부 추론 기록이 아니다. 사용자에게 공개된 지시, 저장소 변경, 실행 결과, 보존된 산출물로 확인 가능한 사실만 정리한다.

## 2. 현재 결론

현재 체크포인트의 결론은 다음과 같다.

| 범위 | 판정 | 근거와 경계 |
|---|---|---|
| phase-1 몸 외곽선 | `PASS` | attempt 8 실제 Windows 결과를 사용자가 `좋다`라고 승인했다. |
| phase-1 시각 자산 정체성 | `PRESERVED` | 꼬리 없는 도로롱 실루엣과 승인된 몸선 권위를 별도 fixture로 보존한다. |
| attempt-8 → phase-1 checkpoint 정확한 런타임 바이너리 provenance | `UNVERIFIED` | 기존에 닫은 provenance 경계를 유지한다. 원인을 추정하지 않는다. |
| 사용자 제공 open / half / closed 파일의 제품 적용 | 구현·파일 동일성 `PASS` | 세 런타임 PNG와 세 authored-frame source가 사용자 제공 native 96 참조와 정확히 일치한다. |
| attempt 28 exact-publish WPF 정적 렌더 | 제한된 범위 `PASS` | open → half → closed → half → open이 게시된 DLL에서 구분되어 렌더된다. |
| 사용자의 최종 미적 승인 | 명시적 품질 `PASS`로 승격하지 않음 | 사용자는 이 상태에서 다음 단계 전 마무리를 요청했다. 이는 체크포인트 중단 지시로 기록하며, 별도의 긍정적 품질 판정을 추정하지 않는다. |
| SLEEP 전체 동작과 모든 깨우기 경로 | `UNVERIFIED` | 정적 closed 프레임 적용과 실제 수면/깨우기 동작 승인은 서로 다른 검증이다. |
| 클릭·드래그·CURIOUS·STARTLED·IDLE·WALK 표현 | `UNVERIFIED` | 다음 로드맵 단계의 작업이다. |
| 투명 영역 클릭 통과·포커스 비탈취 등 실제 비방해성 | `UNVERIFIED` | 자동화나 정적 검사로 실제 다른 프로그램과의 상호작용 PASS를 추정하지 않는다. |
| 전체 Dororong M1 | `PARTIAL` | 아직 필요한 Windows 관찰과 상태별 표현 작업이 남아 있다. |

즉, 이 체크포인트는 “세 눈 상태의 정적 이미지 입력과 런타임 매핑을 사용자 제공 프레임으로 고정했다”는 범위의 마감이다. 전체 M1이나 Stage A 전체 완료를 뜻하지 않는다.

## 3. 유지해야 하는 제품 결정

### 3.1 캐릭터 정체성

- 대상은 일반적인 데스크톱 동물이 아니라 사용자가 지정한 NIKKE 파생 팬 캐릭터 도로롱이다.
- 도로롱에게 꼬리는 없다.
- 장미와 리본 뒤의 흰 형상은 꼬리가 아니라 리본 장식이다.
- 머리, 앞머리, 얼굴 배치, 장미, 리본, 몸 비율, 꼬리 없는 실루엣을 임의로 다시 디자인하지 않는다.
- phase-1에서 승인된 몸 외곽선은 머리·머리카락의 얇은 선 기준과 맞춘 단일 굵기 결과다.
- 상태 표현을 바꾸더라도 몸선·실루엣을 다시 생성하거나 이전 두꺼운 몸선으로 되돌리지 않는다.

### 3.2 행동과 입력

- 최종 상태 우선순위는 `DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP > WALK/IDLE`다.
- 직접 클릭은 동시에 계산된 빠른 접근 반응에 덮이지 않는다.
- SLEEP 중 단순 클릭도 자연스럽게 깨어 `CLICK_REACTION`으로 이어져야 한다.
- 클릭과 드래그는 Windows 드래그 임계값으로 구분한다.
- 투명 픽셀은 뒤 프로그램으로 입력이 통과해야 한다.
- 보이는 캐릭터 픽셀만 클릭·드래그 대상이다.
- 일반 클릭/드래그가 사용 중인 프로그램의 키보드 포커스를 빼앗지 않아야 한다.

### 3.3 범위 제한

현재 작업은 먹이, 호감도, 경험치, 계정, 클라우드, AI 대화, 외부 서비스, 자동 시작, 복잡한 설정, 여러 캐릭터, 사운드, 정교한 멀티 모니터 대응으로 확장하지 않는다.

## 4. 이 단계의 출발점

phase-1 종료 시 다음은 이미 구현되어 있었다.

- Windows 전용 C# / .NET 8 / WPF 앱.
- 투명·무테·최상위 창.
- IDLE / WALK / CURIOUS / STARTLED / CLICK_REACTION / DRAGGED / SLEEP 행동 코어.
- 마우스 접근 속도 판정, 클릭, 드래그, 수면, 깨우기, 화면 경계 처리.
- 알파 기반 히트 테스트와 종료 메뉴.
- 사용자 승인 몸선과 꼬리 없는 도로롱 기본 자산.

그러나 닫힌 눈은 자연스럽지 않았고, 마우스·상황별 표정과 동작 애니메이션은 아직 본격 구현하지 않은 상태였다. 이에 따라 `feature/dororong-m1-expression-animation` 브랜치에서 표현 작업을 시작했다.

## 5. 로드맵과 작업 순서

`docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`에서 다음 순서를 고정했다.

1. Stage A — 닫힌 눈과 수면/깨우기 기반.
2. Stage B — 상태별 presentation mapping과 회귀 보호.
3. Stage C — 클릭·드래그 직접 상호작용 동작.
4. Stage D — CURIOUS / STARTLED 접근 반응.
5. Stage E — IDLE / WALK 자율 생활감.
6. Stage F — 현재 Windows PC에서 순서대로 직접 관찰.
7. Stage G — 모든 필수 항목이 통과한 뒤 M1 마감과 인계.

이번 체크포인트는 Stage A 중에서도 정적 open / half / closed 프레임 기반을 고정한 지점이다. 수면의 전체 동작, 모든 깨우기 경로, 사용자 실제 관찰은 아직 Stage A에 남아 있다.

## 6. 눈 감기 표현 작업의 시간순 이력

### 6.1 초기 닫힌 눈 보정

초기에는 기존 열린 눈 위에 닫힌 눈 선만 덮는 방식으로 접근했다. 위치와 곡률을 조금씩 조정하며 다음을 시도했다.

- 닫힌 눈을 위·아래로 이동.
- 두 눈을 독립적으로 맞춤.
- 화면 오른쪽 눈을 왼쪽으로 3px 이동.
- 얕은 `⌣` 곡선, 짧은 선, 한 줄/두 줄 두께 비교.
- open → half → closed → half → open의 blink timing 추가.

이 방식은 눈만 움직이면 주변 앞머리와 얼굴의 관계가 무너진다는 문제를 해결하지 못했다. 특히 화면 오른쪽 눈이 앞머리 경계와 합쳐져 갈고리나 검은 얼룩처럼 보이거나, 눈이 얼굴 위에서 독립적으로 미끄러지는 인상을 만들었다.

관련 커밋 흐름은 다음과 같다.

| 커밋 | 내용 |
|---|---|
| `0ad1145` | resting eyelid 재작성 |
| `adff0a4` | 닫힌 눈을 아래로 이동 |
| `40b624c` | 더 얕은 닫힌 눈 적용 |
| `c488e9f` | 닫힌 눈 정렬 |
| `a5a62b4` | 양쪽 눈을 독립적으로 fitting |
| `612f252` | half-close transition 추가 |
| `9ee97ed` | 앞머리 occlusion과 timing 수정 |
| `99c58ff` | 닫힌 눈을 머리카락 안쪽으로 inset |
| `87cd4f8` | 화면 오른쪽 눈을 왼쪽으로 3px 이동 |
| `19cf6c4` | 눈 이동을 없애고 stationary facial state로 재설계 |

### 6.2 앞머리 가림과 얼굴 전체 상태 문제

사용자는 “눈을 감을 때 앞머리가 눈을 가리는 관계를 처리해야 한다”고 명확히 지적했다. 이후 단순 눈 오버레이가 아니라 얼굴 상태 전체를 다루는 방식으로 전환했다.

시도한 방향은 다음과 같다.

- canonical 얼굴에서 눈 영역을 비우고 상태별 눈을 다시 합성.
- 눈을 그린 뒤 canonical 앞머리를 다시 최상단에 복원.
- 화면 오른쪽 눈과 앞머리 사이에 face-color clearance를 확보.
- 얼굴 전체 또는 머리 전체를 별도 이미지로 생성해 붙이는 방식 탐색.

머리/얼굴 전체를 새로 그린 이미지를 붙이는 접근은 닫힌 눈 하나는 만들어도 원래 캐릭터와 머리카락, 볼, 입, 장미, 선화가 달라졌다. 사용자는 “기존 캐릭터와 같은 상태에서 표정만 바뀌어야 한다”고 재차 교정했다. 이 접근들은 제품 자산으로 채택하지 않았다.

보존된 실패 증거에는 다음 계열이 포함된다.

- `stage-a-pasted-head-attempt-7`, `attempt-8`
- `stage-a-generated-head-paste-attempt-10`
- `stage-a-generated-face-paste-attempt-11`, `attempt-12`
- `stage-a-single-mouth-attempt-13`
- `stage-a-canonical-lower-face-attempt-14`

실패 이유는 유사도 부족, 입 중복, 턱선/검은 얼룩, 앞머리 선 손상, 볼 주변 불필요한 선, 오른쪽 분홍색 돌출, 원래 캐릭터와 다른 머리/얼굴 질감이었다.

### 6.3 iris-bearing 중간 프레임과 기하학적 lid 시도

열린 눈을 비율로 잘라 중간 프레임을 만드는 방식도 시도했다. 이 방식은 작은 96px 런타임에서 보라색 눈 잔여물이 점·블록·한 줄로 남아 자연스러운 blink가 되지 않았다.

이후 iris를 제거하고 lid-only squint/closed pair를 만들었지만 다음 문제가 반복됐다.

- squint가 작은 `∨`나 대시처럼 보임.
- closed가 깊고 각진 `U`처럼 보임.
- 한쪽 눈이 앞머리와 합쳐져 J/갈고리처럼 보임.
- squint와 closed의 차이가 실제 크기에서 거의 읽히지 않음.
- 정적 확대본은 구조 검사를 통과해도 실제 화면에서는 다른 형태로 지각됨.

관련 주요 커밋은 다음과 같다.

| 커밋 | 내용과 최종 상태 |
|---|---|
| `2ea39ed` | authored native blink states 통합 |
| `221e8ea` | iris transition을 lid-only squint로 교체 |
| `d6b85cd` | 전체 blink repair region 회귀 보호 |
| `c41691b` | 정렬된 lid pair 통합 — 정적 검사는 통과했으나 live 결과가 실패 |
| `747fe14` | 당시 live blink 검증 경계 기록 |
| `85f1661` | direct user rejection에 따라 이전 판정 철회 |

특히 `c41691b` 계열 결과는 자동·정적 검사와 1차 자체 검토가 통과했지만 실제 화면에서 사용자가 즉시 이상함을 지적했다. 이 경험 때문에 이후에는 자체 시각 검증 결과를 사용자 품질 승인으로 대체하지 않는 원칙을 더 엄격하게 적용했다.

### 6.4 이미지 레이어 재구축 — attempt 25

attempt 25에서는 independently painted whole-face 방식 대신 아래 구조를 만들었다.

- 눈이 제거된 공통 얼굴 base.
- open / half / closed 독립 눈 레이어.
- 눈 위에 다시 복원되는 canonical foreground hair 레이어.
- mouth, lower face, body, alpha, eye 바깥 픽셀을 보존하는 합성 회귀 검사.

이 구조는 머리카락과 입 중복 문제를 줄이고 open / half / closed 상태를 분리했다. 그러나 완전 감김 프레임이 half-close의 아래쪽에서 시작해 두 번째 입처럼 낮게 보였고, 사용자가 이를 거부했다.

보존 경로:

`artifacts/repro/stage-a-eye-layer-rebuild-attempt-25/`

### 6.5 upper-anchor antialias 시도 — attempt 26

attempt 26에서는 full-close를 half-close의 아래쪽이 아니라 위쪽 eyelid anchor에 맞추고 `y=51..53`의 짧은 `⌣`로 설계했다.

초기 기하학 검사는 잘못된 낮은 위치를 실제로 RED로 잡았다. 하지만 antialiased 곡선은 얼굴색과 섞이며 exact WPF 출력에서 회색 가로 대시로 무너졌다. 위치 숫자는 맞아도 실제 크기에서 닫힌 눈으로 읽히지 않았으므로 자체 검토에서 폐기했다.

보존 경로:

`artifacts/repro/stage-a-closed-lid-anchor-attempt-26/`

### 6.6 pixel-core closed lid — attempt 27

attempt 27에서는 antialias에만 의존하지 않고 머리 선에 가까운 어두운 픽셀 core를 사용했다.

- 좌우 동일한 폭과 중심.
- 위쪽 endpoint, 아래쪽 center를 가진 세 행 `⌣`.
- 기존 half-close는 변경하지 않음.
- closed layer와 composite hash를 새 회귀 검사에 고정.

자동 검사와 exact WPF 정적 렌더는 통과했지만, 사용자는 이 결과도 만족스럽지 않다고 판단하고 직접 세 상태를 그리기로 했다. attempt 27은 최종 자산이 아니라 superseded evidence로 보존한다.

보존 경로:

`artifacts/repro/stage-a-pixel-core-closed-lid-attempt-27/`

### 6.7 사용자 직접 작성 프레임 — attempt 28

사용자는 다음 세 이미지를 직접 제공했다.

1. 열린 눈.
2. 중간 감김.
3. 완전히 감은 눈.

세 첨부 PNG는 모두 192×192였으며, 모든 2×2 블록의 네 픽셀이 동일했다. 즉 96×96 이미지를 nearest-neighbor 방식으로 정확히 2배 확대한 파일이었다.

검사 결과:

- 각 파일의 2×2 uniform block 수: `9216 / 9216`.
- 알파 구조는 세 상태 모두 동일.
- 각 2×2 블록의 좌상단 픽셀을 한 번만 취하면 정보 손실 없이 원래 96×96을 복원할 수 있음.

첨부 원본은 attempt-specific reference 경로에 그대로 보존했고, inverse 2× 변환으로 native 96 참조를 만들었다. AI 이미지 생성이나 추가 재작화는 사용하지 않았다.

보존 경로:

`artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/reference/`

## 7. attempt 28 구현 내용

### 7.1 런타임 자산 교체

다음 제품 자산을 사용자 제공 native 96 프레임으로 교체했다.

| 상태 | 런타임 파일 | SHA-256 |
|---|---|---|
| open | `src/Dororong.App/Assets/dororong-canonical.png` | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |
| half | `src/Dororong.App/Assets/dororong-blink-squint.png` | `AE2ECC443279153F7174B05E4DAD1E3491BE0232E25F7DDA3E6BFD1174B12F50` |
| closed | `src/Dororong.App/Assets/dororong-closed-eyes.png` | `D3C88F3546FABD487C679C8822FD1F52AF8AC5052132A11D57370AC86E514FFA` |

세 상태의 변경 픽셀은 사용자 파일 기준 눈 영역 안에만 있다.

- half vs open: 변경 `101`픽셀, alpha 변경 `0`, bounds `(17,46)..(44,57)`.
- closed vs open: 변경 `157`픽셀, alpha 변경 `0`, bounds `(17,46)..(43,60)`.

### 7.2 authored-frame source 추가

재생성 도구가 이전 자동 생성 눈으로 되돌리지 못하도록 다음 세 파일을 제품의 영속 authored-frame source로 추가했다.

- `src/Dororong.App/Assets/frame-sources/dororong-canonical.png`
- `src/Dororong.App/Assets/frame-sources/dororong-blink-squint.png`
- `src/Dororong.App/Assets/frame-sources/dororong-closed-eyes.png`

각 source 파일은 대응하는 런타임 파일과 SHA-256이 정확히 같다.

### 7.3 생성 파이프라인 수정

`tools/Generate-CanonicalArt.ps1`을 다음과 같이 수정했다.

- 기존 몸선 소스·mask·subpixel 파이프라인은 phase-1 권위와 진단을 위해 유지.
- 최종 open / half / closed 런타임 출력은 authored-frame source 세 장을 직접 사용.
- 입력 해시를 pin해 source 프레임이 조용히 바뀌면 실패.
- 생성 결과가 정확히 세 authored frame을 출력하도록 변경.
- 기존 eye-layer compositor와 생성 눈 레이어는 현재 제품 생성 경로에서 제거.

이 변경은 C# 행동 코어, 상태 우선순위, WPF frame mapping, blink timing을 변경하지 않는다. 바뀐 것은 최종 시각 프레임의 source-of-truth다.

### 7.4 phase-1 몸선 권위 분리

사용자 제공 open 파일은 이전 canonical open과 시각적으로 같지만, 172개의 부분 투명 edge 픽셀에서 RGB가 채널당 1 정도 다르다. alpha는 동일하다.

phase-1 몸선의 정확한 이전 권위를 새 authored open으로 암묵적으로 바꾸지 않기 위해 다음 fixture를 별도로 추가했다.

`tests/fixtures/dororong-body-outline-native-authority.png`

SHA-256:

`238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`

`Dororong.App.ContinuousAuthority.Tests.ps1`은 이 고정 fixture를 사용한다. 따라서 사용자가 직접 그린 표정 프레임과 phase-1 몸선 권위가 서로 다른 목적의 기준으로 분리된다.

## 8. 테스트 우선 작업 기록

### 8.1 런타임 프레임 동일성 RED

먼저 `tests/Dororong.App.UserAuthoredEyeFrames.Tests.ps1`을 추가했다.

검사가 잡아야 하는 오류는 “런타임 open / half / closed 중 하나라도 사용자 제공 native 96 프레임과 다름”이다.

교체 전 실행 결과는 예상대로 실패했다.

```text
Open runtime asset differs from the user-authored frame at (29,24).
Expected '1049134212', observed '1049068419'.
```

그 뒤 세 제품 자산을 교체했고, 런타임 파일과 참조 파일의 96×96 전체 ARGB 픽셀 동일성 검사가 통과했다.

### 8.2 재생성 RED

`tests/Dororong.App.UserAuthoredEyeFrameGeneration.Tests.ps1`도 먼저 추가했다.

검사가 잡아야 하는 오류는 “생성 파이프라인을 다시 실행하면 사용자 프레임 대신 이전 자동 생성 자산이 출력됨”이다.

수정 전 실행 결과는 예상대로 실패했다.

```text
Generated dororong-canonical.png diverged from the supplied frame.
Expected '699348D1...9A78', observed '238AC7F0...B511'.
```

생성 파이프라인 수정 뒤 open / half / closed 세 출력이 authored-frame source와 정확히 일치했다.

### 8.3 기존 회귀 검사 수정

다음 기존 검사를 새 source-of-truth에 맞게 수정했다.

- `tests/Dororong.App.BlinkRecovery.Tests.ps1`
  - half/closed hash를 사용자 제공 프레임으로 갱신.
  - obsolete iris frame이 돌아오지 않는지 유지.
  - canonical mouth/lower face와 playback sequence 보호 유지.
- `tests/Dororong.App.BlinkStateArt.Tests.ps1`
  - 이전 생성 lid geometry 계약을 제거.
  - 정확한 세 authored frame hash, 96×96, 동일 alpha, 눈 영역 밖 불변, half의 축소된 iris, closed의 iris 제거, 세 상태 구분, 재생성 동일성을 검사.
- `tests/Dororong.App.ExactArt.Tests.ps1`
  - exact asset hash와 generator output 계약을 authored frame 세 장으로 갱신.
  - 96 DPI presenter, alpha hit testing, 상태→파일 mapping 검사는 유지.
- `tests/Dororong.App.ContinuousAuthority.Tests.ps1`
  - phase-1 몸선 exact authority를 별도 고정 fixture로 이동.

이전 eye-free/eye-layer/closed-lid-anchor 전용 검사는 사용자 제공 완성 프레임이 새로운 source-of-truth가 되면서 제품 회귀 기준에서 제거했다. 이전 attempt-specific artifacts는 실패/비교 증거로 그대로 보존한다.

## 9. 사용자 제공 파일과 native 96 identity

| 파일 | 크기 | 바이트 | SHA-256 |
|---|---:|---:|---|
| `user-open-2x.png` | 192×192 | 14,728 | `1A3A8B5E7E852D8AD63F782EBD87BAE93C556C172A685080A99CF83BC6B8CAD2` |
| `user-half-2x.png` | 192×192 | 14,534 | `026F91F0154623CFD0B38766B7475983399F1D5104E06FC3DE1E67626326D65D` |
| `user-closed-2x.png` | 192×192 | 14,344 | `8970FFE6C32C4EEE478AB9F55F26B33B3282719E2716ABBEE2FE53E843A1185B` |
| `user-open-native96.png` | 96×96 | 9,678 | `699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78` |
| `user-half-native96.png` | 96×96 | 9,441 | `AE2ECC443279153F7174B05E4DAD1E3491BE0232E25F7DDA3E6BFD1174B12F50` |
| `user-closed-native96.png` | 96×96 | 9,281 | `D3C88F3546FABD487C679C8822FD1F52AF8AC5052132A11D57370AC86E514FFA` |

테스트용 영속 참조는 다음에도 복사했다.

- `tests/fixtures/dororong-user-authored-open-reference.png`
- `tests/fixtures/dororong-user-authored-half-reference.png`
- `tests/fixtures/dororong-user-authored-closed-reference.png`

## 10. 검증 결과

### 10.1 자동 검사

현재 변경을 대상으로 다음을 확인했다.

| 검사 | 결과 |
|---|---|
| Dororong App PowerShell suites | `13 / 13 PASS` |
| Dororong Core tests | `78 / 78 PASS` |
| Debug solution build | 경고 `0`, 오류 `0` |
| Release solution build | 경고 `0`, 오류 `0` |
| `git diff --check` | exit `0`; whitespace 오류 없음 |

13개 앱 검사는 blink recovery/sequence/state art, body mask, continuous authority, dragged angle, exact art, occlusion delegation, runtime composition, sleep pose, subpixel outline, user-authored frame identity, user-authored regeneration을 포함한다.

자동 검사 통과는 정적 파일·코드·매핑·생성 계약의 증거다. 실제 Windows에서 자연스럽게 보이고 사용을 방해하지 않는다는 전체 제품 판정은 아니다.

### 10.2 attempt 28 게시 산출물

게시 경로:

`artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/runtime/`

| 산출물 | SHA-256 |
|---|---|
| `Dororong.App.exe` | `BCBA3EB07F0048E8584406E033A4DB56A0857A44E0F43EA18E5752E66A01E2C4` |
| `Dororong.App.dll` | `970897745655D1BD3D98F489227E3F07067958C7C7853255924E3906B9934219` |
| `Dororong.Core.dll` | `79E04B968D948D850B26479C6550F20C12504B2C416610C55D4108A2C3E0B983` |

`artifacts/publish/win-x64`는 사용하지 않았고, 이전 verdict evidence를 덮어쓰지 않았다.

### 10.3 exact WPF 정적 렌더

검증 경로:

`artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/verification/published-wpf-render-inspection-1/`

게시된 assembly의 `DororongPresenter`를 사용해 다음 phase를 렌더했다.

| 순서 | 상태 | phase | 렌더 SHA-256 |
|---:|---|---:|---|
| 1 | open | `0.64` | `0946CC4A8D063F2CD8E22ED33E99C93BCA622E508653EBEF87C6D930D7AF4E72` |
| 2 | half | `0.66` | `C631A0A2BDF4A467C2132328381026CCA73136794733F8DFE3F7CBB4391F3021` |
| 3 | closed | `0.70` | `B88709D9E54E820C7568108C0065872072E1EF15750FBC428C44D29F657FA54B` |
| 4 | half | `0.74` | `C631A0A2BDF4A467C2132328381026CCA73136794733F8DFE3F7CBB4391F3021` |
| 5 | open | `0.78` | `0946CC4A8D063F2CD8E22ED33E99C93BCA622E508653EBEF87C6D930D7AF4E72` |

검사한 화면:

- native 144×144 전체 프레임.
- 전체 context 2× montage.
- 얼굴 nearest-neighbor 8× montage.
- open / half / closed 개별 프레임.

source native 96과 WPF 출력의 character crop을 비교한 결과:

- 공통 위치 offset: `(24,25)`.
- alpha 차이: `0`픽셀.
- opaque RGB 차이: `0`픽셀.
- 부분 투명 edge RGB 차이: 상태별 `152`픽셀.
- 부분 투명 edge의 최대 채널 차이: `1`.

이 152픽셀 차이는 Pbgra32 premultiplied render round-trip에서 세 상태에 동일하게 나타나는 부분 투명 edge 반올림이다. 눈 상태나 실루엣의 추가 변형이 아니다.

### 10.4 실제 실행 프로세스

새 실행 전 화면에는 오래된 attempt 24 프로세스가 남아 있었다.

- 이전 PID: `32072`
- 이전 경로: `artifacts/repro/stage-a-eye-geometry-attempt-24/runtime/Dororong.App.exe`

PID와 exact executable path를 확인한 뒤 해당 이전 프로세스만 종료했다.

현재 실행한 attempt 28:

- PID: `30704`
- 실행 시각: `2026-08-30 19:13:26 +09:00`
- 경로: `artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/runtime/Dororong.App.exe`

문서 작성 시점에는 이 프로세스 한 개만 실행 중이었다. 프로세스 생존은 실행 파일이 떠 있다는 증거일 뿐, 모든 시각/상호작용 기능이 통과했다는 증거로 사용하지 않는다.

## 11. 현재 작업 파일 상태

현재 변경은 아직 commit, push, PR 생성되지 않았다. 문서 작성 전 HEAD는 `85f166160bba6593364e48617336571d94319c32`이고 worktree는 의도적으로 dirty 상태다.

### 11.1 수정된 파일

- `TASKS.md`
- `src/Dororong.App/Assets/dororong-canonical.png`
- `src/Dororong.App/Assets/dororong-blink-squint.png`
- `src/Dororong.App/Assets/dororong-closed-eyes.png`
- `tests/Dororong.App.BlinkRecovery.Tests.ps1`
- `tests/Dororong.App.BlinkStateArt.Tests.ps1`
- `tests/Dororong.App.ContinuousAuthority.Tests.ps1`
- `tests/Dororong.App.ExactArt.Tests.ps1`
- `tools/Generate-CanonicalArt.ps1`

### 11.2 새 파일

- `src/Dororong.App/Assets/frame-sources/dororong-canonical.png`
- `src/Dororong.App/Assets/frame-sources/dororong-blink-squint.png`
- `src/Dororong.App/Assets/frame-sources/dororong-closed-eyes.png`
- `tests/Dororong.App.UserAuthoredEyeFrames.Tests.ps1`
- `tests/Dororong.App.UserAuthoredEyeFrameGeneration.Tests.ps1`
- `tests/fixtures/dororong-user-authored-open-reference.png`
- `tests/fixtures/dororong-user-authored-half-reference.png`
- `tests/fixtures/dororong-user-authored-closed-reference.png`
- `tests/fixtures/dororong-body-outline-native-authority.png`
- 이 보고서.

C# 행동 코드와 XAML은 attempt 28에서 수정하지 않았다.

## 12. 폐기하거나 다시 사용하면 안 되는 접근

다음은 이미 반복 실패했으므로 새 근거 없이 같은 방식으로 돌아가지 않는다.

1. 기존 눈 위에 닫힌 눈 선만 덮고 위치를 계속 미세 이동하는 방식.
2. 양쪽 눈을 같은 기하학으로 취급하는 방식.
3. 앞머리 occlusion을 무시하고 눈 layer만 얼굴 위에 올리는 방식.
4. 열린 iris를 위에서 잘라 70%/25% 중간 프레임을 만드는 방식.
5. 작은 96px 눈을 antialias 곡선 하나만으로 해결하는 방식.
6. generated whole head/face를 붙여 원래 캐릭터 유사도를 희생하는 방식.
7. 확대본의 구조 수치만 보고 실제 native/runtime 품질을 PASS로 판단하는 방식.
8. 자동 GUI 캡처 실패를 제품 실패 또는 성공으로 바꾸어 해석하는 방식.

사용자 제공 세 프레임이 현재의 시각 권위다. 이후 동작을 추가할 때 이 세 파일을 자동 생성 눈으로 되돌리지 않는다.

## 13. 확인된 한계와 남은 위험

### 13.1 아직 검증하지 않은 Stage A 범위

- SLEEP에서 closed 프레임이 장시간 안정적으로 유지되는지.
- 수면 breathing이 프레임을 흐리게 하거나 위치를 어색하게 만들지 않는지.
- 느린 접근, 빠른 접근, 클릭만 한 경우, 드래그 시작의 각 wake 경로가 올바른 표현으로 이어지는지.
- 클릭-only wake가 STARTLED나 DRAGGED로 잘못 보이지 않는지.
- 실제 데스크톱에서 blink 속도와 dwell이 자연스러운지.

### 13.2 아직 구현하지 않은 표현 범위

- CLICK_REACTION의 달랑거림/바운스.
- DRAGGED의 매달린 자세와 release settling.
- CURIOUS의 주의 이동/기울임.
- STARTLED의 놀람 표정과 짧은 후퇴.
- WALK의 읽히는 보행 cycle.
- IDLE의 방해되지 않는 생활감.

### 13.3 제품 전체 검증 위험

- 투명 영역 클릭 통과는 실제 다른 프로그램과 함께 확인해야 한다.
- no-focus-theft는 실제 작업 중인 창의 포커스 변화로 확인해야 한다.
- topmost와 작업 영역 경계는 실제 Windows 환경에서 확인해야 한다.
- 멀티 모니터 정교 대응은 현재 핵심 범위가 아니다.
- 현재 exact WPF 정적 렌더는 실제 장시간 desktop motion 검증을 대체하지 않는다.

## 14. 다음 단계 시작점

다음 작업은 새 눈 그림을 다시 만드는 일이 아니다. 사용자 제공 open / half / closed를 고정한 채 동작을 붙이는 일이다.

권장 순서는 다음과 같다.

1. 현재 attempt 28 변경을 하나의 체크포인트로 검토하고, 사용자가 요청할 때만 commit/push한다.
2. SLEEP breathing과 wake transition이 세 authored frame을 손상하지 않는지 먼저 확인한다.
3. click-only wake를 실제 Windows에서 한 항목으로 보여준다.
4. Stage A의 수면/깨우기 관찰이 끝난 뒤 Stage C의 CLICK_REACTION / DRAGGED로 넘어간다.
5. CURIOUS / STARTLED는 직접 상호작용 동작과 분리해 한 번에 하나씩 검증한다.
6. IDLE / WALK polish와 비방해 검증은 마지막 Windows acceptance 순서에서 확인한다.

다음 구현에서 지켜야 할 기술 경계:

- `Dororong.Core` 행동 상태와 우선순위를 표정 때문에 임의 변경하지 않는다.
- WPF presenter는 현재 frame mapping을 유지하면서 transform/motion만 작은 단위로 추가한다.
- open / half / closed source hash를 보호한다.
- 실제 Windows 사용자 관찰 전에는 해당 동작을 PASS로 승격하지 않는다.
- 이전 attempt evidence를 덮어쓰지 않고 새 attempt-specific 경로를 사용한다.

## 15. 빠른 인계용 파일 목록

다음 작업자는 아래 순서로 읽으면 된다.

1. `docs/specs/2026-08-26-dororong-m1-design.md`
2. `docs/roadmaps/2026-08-28-dororong-m1-expression-animation-roadmap.md`
3. `TASKS.md`
4. 이 보고서
5. `docs/handoff/2026-08-28-dororong-phase-1-conversation-and-work-history.md`
6. `docs/verification/2026-08-28-m1-windows-acceptance-manual-attempt-8.md`
7. `artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/reference/`
8. `artifacts/repro/stage-a-user-authored-eye-frames-attempt-28/verification/published-wpf-render-inspection-1/`

## 16. 체크포인트 최종 요약

- phase-1 몸선 `PASS`는 유지한다.
- overall M1은 `PARTIAL`이다.
- 반복 실패한 자동 생성 눈/얼굴 방식은 현재 제품 경로에서 폐기했다.
- 사용자가 직접 제공한 open / half / closed 세 이미지를 exact authored frame으로 고정했다.
- 192×192 첨부는 정확한 2× nearest-neighbor 확대였고, 정보 손실 없이 96×96으로 되돌렸다.
- 런타임 asset, authored-frame source, test reference가 상태별로 같은 hash를 사용한다.
- 생성 파이프라인을 다시 실행해도 세 사용자 프레임을 보존한다.
- 자동 검사와 exact WPF 정적 렌더는 해당 제한 범위에서 통과했다.
- 사용자 표현은 이 지점에서 다음 단계 전 작업을 마무리하라는 지시로 기록하며, 명시하지 않은 품질 PASS를 추정하지 않는다.
- SLEEP/wake, 클릭/드래그, 접근 반응, 자율 동작, 비방해 Windows acceptance는 아직 남아 있다.
- 현재 변경은 미커밋 상태이며 push/PR/merge를 수행하지 않았다.
