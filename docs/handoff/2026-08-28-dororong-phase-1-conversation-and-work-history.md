# Dororong Desktop Pet 1차 대화·결정·작업 이력

- 기록일: 2026-08-28 (Asia/Seoul)
- Codex 작업 ID: `01a03970-f3b0-7e43-8f9d-62ec267475ae`
- 작업 제목: `Dororong 데스크톱 펫 구축`
- 저장소: `D:\JOEWRKS\.worktrees\DororongDesktopPet-m1`
- 브랜치: `feature/dororong-m1`
- 기준 명세: `docs/specs/2026-08-26-dororong-m1-design.md`
- 이 문서의 역할: 대화에서 확정된 제품 방향, 사용자의 수정 지시, 구현과 검증, 실패한 접근, 현재 한계, 다음 작업자가 이어갈 지점을 한곳에 보존한다.

이 문서는 내부 추론 기록이 아니라 사용자에게 공개된 대화와 저장소·실행 결과로 확인할 수 있는 작업 이력이다. 원문 전체 채팅은 Codex 작업에 남아 있고, 여기에는 제품과 구현에 영향을 준 내용을 빠짐없이 시간순으로 재구성했다.

## 1. 1차 종료 상태

이번 1차에서 확정된 결과는 다음과 같다.

1. Windows 전용 C# / .NET 8 / WPF 데스크톱 펫의 행동 코어와 실행 셸을 구현했다.
2. 투명·테두리 없는 최상위 창, 보이는 캐릭터 부분의 클릭/드래그, 투명 영역의 입력 통과, 비활성 창 동작을 코드와 테스트 대상으로 만들었다.
3. `IDLE / WALK / CURIOUS / STARTLED / CLICK_REACTION / DRAGGED / SLEEP` 상태와 자율 행동, 마우스 접근·빠른 접근·클릭·드래그·수면/깨우기 규칙을 구현했다.
4. 사용자가 제공한 실제 NIKKE 파생 팬 캐릭터 도로롱 원본을 시각 권위로 고정했다. 도로롱에게 꼬리는 없고, 장미와 리본 뒤의 흰 형상은 꼬리가 아니라 리본이다.
5. 여러 차례 실제 Windows 실패를 거쳐 몸 외곽선을 머리/머리카락의 얇은 선 기준에 맞췄다. 내부 가슴/어깨 연장선은 흐리게 남기는 방식이 아니라 렌더링에서 완전히 제거했다.
6. 실제 Windows에서 실행한 정확한 attempt 8 산출물에 대해 사용자가 `좋다`라고 답해 몸 선 보정을 승인했다. 이 범위는 **PASS**다.

이번 1차가 뜻하지 않는 것은 다음과 같다.

- 전체 Milestone 1을 완료했다고 판정하지 않는다.
- 현재 눈 감는 그림은 예전처럼 눈이 얼굴 밖으로 이탈하지는 않지만, 표정과 동작 품질을 다음 단계에서 다시 고쳐야 한다. 해당 품질은 **UNVERIFIED**다.
- 마우스·상황별로 다양한 표정과 동작 애니메이션을 넣는 작업은 아직 시작하지 않았다.
- 남아 있는 실제 Windows 상호작용·비방해 검증 항목은 **UNVERIFIED**다.

## 2. 사용자 대화와 결정의 시간순 기록

### 2.1 최초 제품 제안

사용자는 Windows 데스크톱 위에서 도로롱이 혼자 생활하며 사용자의 행동에 반응하는 작은 동반자형 앱을 요청했다. 핵심 기준은 다음 세 문장이었다.

- `살아 있는 것처럼 보이는가?`
- `상호작용하는 것이 재미있는가?`
- `컴퓨터 작업을 방해하지 않는가?`

첫 버전의 요구 범위는 투명·무테·최상위 창, 자율 정지/보행, 화면 경계 제한, 일곱 행동 상태, 느린/빠른 마우스 접근 구분, 클릭, 드래그, 장시간 방치 후 수면, 자연스러운 깨우기, 명확한 종료 방법이었다. 먹이·호감도·계정·클라우드·AI 대화·외부 서비스·복잡한 설정·자동 시작·정교한 멀티 모니터·사운드·대규모 애니메이션 제작은 첫 버전 범위에서 제외했다.

사용자는 코드를 먼저 많이 쓰기보다 “첫 번째 완성 상태”를 분명히 정의하고, 판단·검증·인계를 적당한 단위로 나누며, 실제 실행 결과로 확인할 것을 요청했다.

### 2.2 작업 위치와 JOENESS 환경

초기 작업 위치가 C 드라이브로 잡히자 사용자는 `왜 다 전부 C로 잡는거야. 지금 D에 있는 폴더에 하위폴더를 만들어서 작업해야지`라고 교정했다. 이후 작업은 D 드라이브의 JOENESS 작업 공간과 현재 worktree에서 진행했다.

사용자는 Codex 환경에 이미 설치된 `JOENESS work harness`를 기본 작업 방식으로 사용하되, 하네스를 다시 설치하거나 수정하지 말라고 지시했다. 프로젝트 초기 설정은 하네스의 정상 절차를 따랐다.

문서 경로와 관리 파일에 대해서도 다음을 확인하고 확정했다.

- 영속 설계 문서는 특정 방법론 이름을 담은 `docs/superpowers/specs/...`가 아니라 프로젝트 중립적인 `docs/specs/...`에 둔다.
- `AGENTS.md`는 임의로 새로 작성하지 않고 JOENESS 프로젝트 초기화 절차가 관리하는 파일로 취급한다.
- 프로젝트 설정 검사는 하네스 제공 스크립트로 수행했다.

### 2.3 행동 상태와 우선순위

사용자는 상태 전이와 행동 규칙 설계로 진행하는 것을 승인했다. 투명 영역이 뒤 프로그램의 클릭을 막지 않는지 질문했고, 이에 따라 다음을 영속 요구로 고정했다.

- 알파 0인 투명 픽셀은 뒤 프로그램으로 입력이 통과해야 한다.
- 캐릭터의 실제로 보이는 부분만 상호작용 대상이다.
- 일반 클릭/드래그가 작업 중인 앱의 키보드 포커스를 빼앗지 않아야 한다.
- WPF 내부 히트 테스트만으로 실제 다른 프로세스 클릭 통과를 증명했다고 간주하지 않는다.

사용자는 SLEEP 상태에서 드래그하지 않고 클릭만 해도 자연스럽게 깨어서 클릭 반응을 보여야 한다고 확인했다. 이 규칙을 구현과 테스트에 포함했다.

초기 승인 우선순위는 `DRAGGED > STARTLED > CLICK_REACTION > CURIOUS > ...`였으나 영속 명세에서 `CLICK_REACTION`이 앞에 놓인 것을 사용자가 발견했다. 이는 전사 오류가 아니라 의도적인 개선이었다. 사용자가 화면에서 직접 일으킨 클릭이 동시에 추론된 빠른 접근 반응에 덮이지 않도록 최종 우선순위를 다음과 같이 확정했다.

`DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP > WALK/IDLE`

사용자는 이 의도적 변경을 선택지 1로 승인했다.

### 2.4 구현과 검증 방식

사용자는 계획대로 실제 구현까지 진행하고, 작업별 하위 에이전트를 사용하며, 추측이 아니라 확인 가능한 증거로 검증하라고 지시했다. 구현·테스트·검토 단위를 나눠 진행했고, 결과는 최종적으로 주 작업자가 다시 검사했다.

GUI 자동화 도구가 현재 Windows 환경에서 화면 캡처/입력에 실패했을 때 사용자는 다른 컴퓨터나 새 Windows 환경으로 옮기지 말라고 했다. 자동화 도구 문제와 도로롱 제품 문제를 분리하고, 현재 PC에서 사용자가 직접 한 항목씩 관찰하는 방식으로 바꿨다. 자동화 실패는 제품 PASS/FAIL로 사용하지 않았고, 사용자 관찰 전 항목은 **UNVERIFIED**로 유지했다.

### 2.5 캐릭터 정체성 교정

초기 임시 캐릭터가 실행됐을 때 사용자는 첫 Windows 확인으로 `보임 / 배경·테두리 없음 / 일반 창 위에 유지됨`을 확인했다. 그러나 동시에 “도로롱”이 일반 이름이 아니라 모바일 게임 NIKKE에서 파생되어 웹에서 유행하는 특정 팬 캐릭터라고 교정했다.

처음 제시된 작은 갈색 캐릭터는 최종 캐릭터가 아니며, 사용자는 분홍 머리·흰 몸의 도로롱 참고 이미지를 `이게 메인임. 참고해서 ㄱㄱ`라고 지정했다. 유사 캐릭터나 일반화한 대체 디자인이 아니라 그 캐릭터를 그대로 사용하라고 재차 지시했다. 공개 라이선스/팬메이드라는 사용자의 의도와 별개로, 프로젝트는 제공된 이미지를 정확한 시각 권위로 취급하고 재디자인하지 않는 기술적 원칙을 채택했다.

사용자가 `꼬리는 원래 없어요`라고 정정했으므로, 다음을 명세와 테스트에 고정했다.

- 도로롱에게 꼬리는 없다.
- 장미/리본 뒤의 흰 부분은 리본이다.
- 원본의 얼굴, 머리카락, 장미, 리본, 눈, 입, 몸 비율과 실루엣을 임의로 재해석하지 않는다.

### 2.6 캐릭터 그림과 몸 선 보정 피드백

사용자는 기본 캐릭터 원형은 잘 표현됐다고 보았지만, 다음 문제를 반복적으로 지적했다.

1. 머리 선은 얇은데 몸 선은 두껍다.
2. 몸통 선의 굵기가 일정하지 않다.
3. 몸 선이 실루엣 밖이나 안쪽으로 삐져나온다.
4. 몸 안쪽의 가슴/어깨 연장선이 남아 있다.
5. 눈을 감을 때 눈이 얼굴에서 이탈한다.

주요 사용자 지시는 다음과 같았다.

- `몸도 머리처럼 선 굵기를 통일해줘.`
- `아직 좀 두껍긴 한데 그것보다는 몸통 선의 굵기가 일정치 않은 거 같아.`
- `몸이 왜 스트로크 삐져나가냐.`
- `머리선을 그린 대로 몸 선도 그리는게 두께 유지해서 그리는게 어려운거야?`
- `다시 해 필요하면 처음부터.`
- `단일 굵기로 가야돼. 단적으로 머리가 얇으니 머리의 선 기준으로 몸을 다시 그리라고.`
- `네가 시각 검증을 해보세요.`
- `잘 된 부분과 고칠 부분을 선정해서 수정해.`
- `몸 안에 있는 선도 지금 옅어지기만 했지 아직 남아있잖아.`

이 피드백은 단순한 취향 제안이 아니라 현재 단계의 실패 판정으로 처리했다. 매번 새 실제 Windows 시도를 별도 기록으로 고정하고 이전 실패를 덮어쓰지 않았다.

### 2.7 이미지 생성 도구 질문과 최종 방식

사용자가 `너 그림 뭘로 그리냐? 이미지 젠으로 그리는거 아냐?`라고 물은 뒤 이미지 생성 기반 편집을 한 번 탐색했다. 결과는 몸 선만 수정하지 않고 머리·얼굴·머리카락·장미·리본·비율까지 다시 그려 도로롱 정체성을 훼손했다. 그 결과는 폐기했고 제품 자산에 통합하지 않았다.

최종 성공 방식은 이미지 생성이 아니라 사용자가 준 원본을 고정한 결정론적 래스터 재구성이었다. 원본의 보호 영역과 실루엣을 유지하고, 몸 소유 영역의 외곽선만 소스 스케일에서 다시 계산해 단 한 번 96px 실행 자산으로 축소했다.

### 2.8 1차 승인과 다음 단계 지시

attempt 8의 정확한 실행 산출물을 본 사용자는 `좋다`라고 답했다. 이 답변으로 다음이 승인됐다.

- 몸 외곽선이 머리/머리카락 선과 비슷한 얇은 한 굵기로 보인다.
- 삐져나오는 선이 없다.
- 몸 안의 연장선이 흐리게 남지 않고 사라졌다.
- 꼬리 없는 원래 실루엣이 유지된다.

같은 메시지에서 사용자는 다음 단계 방향을 명시했다.

> 이제 얘 눈 감는거 수정이랑 마우스 및 상황별 액션을 캐릭터성 유지한 채 다양한 표정과 동작 애니메이션으로 구현

이 요청은 1차 결과를 되돌리는 지시가 아니다. 1차 몸 선 범위를 마감하고, 다음 작업에서 눈 감는 표현과 상태별 연출을 별도 설계·구현하라는 지시로 기록한다.

## 3. 수행한 작업의 시간순 기록

### 3.1 제품 명세와 완료 기준

- 첫 번째 완성 경험과 비범위를 명세로 작성했다.
- 행동 상태별 진입 조건, 최소 유지 시간, 취소 조건, 쿨다운, 수면/깨우기 규칙을 정의했다.
- 직접 입력이 추론 반응보다 우선하도록 상태 전이 우선순위를 수정하고 결정 기록을 남겼다.
- 투명 창의 클릭 통과와 포커스 비탈취를 실제 Windows 검증 항목으로 분리했다.
- 완료는 코드 존재나 빌드 성공이 아니라 실제 Windows 관찰을 포함하도록 정했다.

### 3.2 저장소와 구조

- D 드라이브 JOENESS 작업 공간에 Git 프로젝트/worktree를 구성했다.
- 행동/상태 결정을 담당하는 `Dororong.Core`와 WPF 표현·입력을 담당하는 `Dororong.App`을 분리했다.
- 범용 게임 엔진이나 대형 외부 프레임워크를 추가하지 않았다.
- 캐릭터 표현을 교체해도 행동 코어를 다시 만들지 않도록 presenter 경계를 두었다.

### 3.3 행동 코어 구현

- IDLE과 WALK의 자율 전환과 결정론적 시간 갱신을 구현했다.
- 화면 경계에서 이동 방향을 반사하고 주 작업 영역 안에 유지하도록 했다.
- 느린 접근은 CURIOUS, 빠르게 향하는 접근은 STARTLED로 구분했다.
- STARTLED에서 마우스 반대 방향으로 물러나도록 했고, 포인터가 중심과 겹치는 경계도 다뤘다.
- 클릭 반응, Windows 드래그 임계값 이후의 DRAGGED, 잡은 지점 보존, 놓은 뒤 화면 안 보정을 구현했다.
- 장시간 비활동 후 SLEEP, 접근/클릭/드래그로 맞는 반응을 거쳐 깨우는 규칙을 구현했다.
- 상태 최소 유지 시간과 우선순위를 넣어 상태가 매 프레임 흔들리지 않게 했다.

### 3.4 WPF·Windows 통합

- 무테, 투명, 최상위, 작업표시줄/Alt+Tab 제외 창을 구현했다.
- Win32 확장 스타일과 히트 테스트 경계를 사용해 투명 픽셀 입력 통과와 비활성 클릭을 다뤘다.
- 보이는 알파 영역만 클릭/드래그 대상으로 연결했다.
- 우클릭 컨텍스트 메뉴에 Exit를 넣었다.
- 런타임 루프, 종료 시 타이머/마우스 캡처 정리, 예외 경계를 보강했다.
- 상태·방향·정규화된 진행률을 WPF presenter에 연결했다.

### 3.5 자동화와 실제 Windows 검증 분리

- 핵심 행동은 단위 테스트와 상태 경계 테스트로 검증했다.
- runtime composition과 DRAGGED 각도 연결을 별도 회귀 검사로 만들었다.
- GUI 자동화 도구 실패를 환경 제한으로 기록하고 제품 결과에 섞지 않았다.
- 사용자가 현재 PC에서 하나씩 관찰하도록 exact artifact, PID, 경로, 생성 시간, 해시를 묶어 시도별 문서를 만들었다.
- 실행 후에는 기록한 정확한 PID·경로·생성 시간이 일치할 때만 종료하고 생존 프로세스가 없는지 확인했다.

### 3.6 원본 도로롱 자산 통합

- 사용자 제공 225x225 이미지를 원본 권위로 저장하고 SHA-256을 고정했다.
- 배경 경계에 연결된 흰색만 투명화하고 얼굴/머리/장식/리본을 보호했다.
- 알파를 고려한 바디 히트 테스트를 presenter 입력과 연결했다.
- 실행 시 WPF가 다시 보간하지 않도록 96x96 런타임 자산을 생성했다.
- 열린 눈과 닫힌 눈 자산에서 몸 부분이 동일한지 검사했다.

### 3.7 몸 외곽선 수정의 실패와 개선

몸 선 문제는 다음 순서로 다뤘다.

1. 기존 원본 선을 단순 보정하고 닫힌 눈 흔적을 제거했다. 캐릭터 원형은 통과했지만 몸 선 굵기와 눈 이탈은 실패했다.
2. 96px에서 직접 몸 선을 지우거나 밝게 하는 접근을 시도했다. 일부 삐져나옴은 줄었지만 굵기 불균일과 내부 선을 해결하지 못했다.
3. 손으로 새 Bezier 몸 윤곽을 그리는 접근을 시도했다. 승인된 실루엣이 달라지고 내부 어두운 픽셀이 생겨 폐기했다.
4. 원본에서 58개 픽셀만 선택적으로 옅게 하는 보수적 보정을 시도했다. 새 돌출은 막았지만 실제 화면에서 몸 선이 여전히 무겁고 불균일해 폐기했다.
5. 원본 실루엣에서 몸 윤곽을 다시 추출하고 단일 전역 폭으로 안쪽에 그리는 파이프라인을 만들었다.
6. 픽셀 중심 거리장과 잘못된 측정 normal 때문에 전 구간 공통 폭이 나오지 않는 문제를 진단했다. 측정 선, 마스크, 윤곽, 소유권을 차례로 고쳤다.
7. 몸 선 소유 영역을 완전히 지우고 다시 그리는 구조로 바꿨다. 외곽 `E`와 내부 연장 `C`를 별도 권위로 유지했다.
8. candidate F는 저장소 시각 검사와 자동화는 통과했지만 실제 Windows에서 몸이 두껍고 C 내부 선이 옅게 남아 attempt 7에서 실패했다.
9. 원본·마스크·윤곽·보호 영역을 고정한 채 `W=1.5`, `1.625`, `1.75` 세 후보만 비교했다. 본 작업자가 native 96px, 4배 확대, 흰색/어두운 배경에서 모두 확인했다.
10. `W=1.5`, 외곽 E gain `2.5`, 내부 C gain `0`을 선택했다. 내부 선은 알파를 낮춘 것이 아니라 출력에 전혀 기여하지 않게 했다.
11. attempt 8 실제 Windows 실행과 사용자 `좋다` 판정으로 몸 선 범위를 통과시켰다.

### 3.8 테스트 우선 보정과 독립 검토

최종 보정 전에 기존 candidate F가 실패해야 하는 `ThinOutlineOnly` 회귀를 먼저 추가했다.

- 수정 전 RED: 몸 고불투명 선 두께 중앙값 `1.125`, 머리/머리카락 `0.6875`; 요구 상한 `0.75` 초과. C 거리장을 0으로 만들자 첫 출력 차이가 `(159,111)`에서 발생해 내부 C가 실제 출력에 남는다는 점도 증명했다.
- 수정 후 GREEN: 몸 중앙값 `0.75`, 머리/머리카락 `0.6875`; C 전체를 변형해도 출력 바이트가 동일했다.
- C gain을 다시 `1`로 바꾸는 고의 변형은 명명된 실패를 일으켜 검사가 우연히 통과하지 않음을 확인했다.

별도 읽기 전용 검토는 P0~P3 결함을 찾지 못했다. 최종 판단은 검토자의 서술이 아니라 본 작업자의 재실행 결과와 사용자의 실제 Windows 판정으로 확정했다.

## 4. 현재 영속 제품 결정

### 4.1 플랫폼과 구조

- Windows x64 전용 시작.
- C# / .NET 8 / WPF.
- `Dororong.Core`는 이미지나 WPF를 모르고 상태·방향·진행률을 제공한다.
- `Dororong.App`이 Windows 입력, 투명 창, presenter, 자산을 담당한다.
- 1차는 기본 모니터 작업 영역만 공식 범위다.

### 4.2 상태 우선순위

`DRAGGED > CLICK_REACTION > STARTLED > CURIOUS > SLEEP > WALK/IDLE`

직접 클릭은 같은 순간 계산된 접근 반응에 덮이지 않는다. SLEEP 중 단순 클릭도 깨어나 CLICK_REACTION을 보인다.

### 4.3 비방해 원칙

- 투명 픽셀은 뒤 앱으로 클릭이 통과한다.
- 보이는 몸만 클릭/드래그 대상이다.
- 일반 상호작용은 작업 앱의 키보드 포커스를 빼앗지 않는다.
- 실제 Windows에서 직접 확인하지 않은 비방해 항목은 PASS로 추정하지 않는다.

### 4.4 캐릭터 권위

- 원본: `src/Dororong.App/Assets/dororong-canonical-source.png`
- SHA-256: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- 도로롱은 꼬리가 없다.
- 흰 뒤쪽 형상은 리본이다.
- 얼굴, 머리, 장미, 리본, 눈, 입, 비율, 세 다리와 두 골짜기 실루엣은 보호 영역이다.
- 이미지 생성으로 캐릭터 전체를 다시 그리지 않는다.

### 4.5 승인된 몸 외곽선

- 단일 전역 외곽 반경 `W=1.5`.
- 외곽 `E` 광학 gain `2.5`.
- 내부 연장 `C` gain `0`.
- source 225px에서 계산한 뒤 premultiplied-alpha 방식으로 단 한 번 96x96으로 축소한다.
- 96 DPI / 100% Windows 대상에서 WPF가 96x96 DIP로 그대로 표시한다.
- 다른 DPI/배율에서 동일하게 보인다는 주장은 아직 하지 않는다.

## 5. 검증 결과

| 범위 | 결과 | 근거 |
|---|---|---|
| Core 단위 테스트 | PASS | Release 기준 78 passed, 0 failed, 0 skipped |
| Release 빌드 | PASS | warnings 0, errors 0 |
| Body mask / continuous authority | PASS | 각 전용 PowerShell 회귀 exit 0 |
| Subpixel outline 전체 회귀 | PASS | full suite exit 0 |
| Exact art | PASS | 최종 full suite exit 0 |
| Runtime composition | PASS | 실제 `PetLoop` 연결 검사 exit 0 |
| DRAGGED presenter angle | PASS | 전용 회귀 exit 0 |
| win-x64 framework-dependent publish | PASS | publish exit 0 |
| 실제 Windows 무테·투명·최상위 표시 | PASS | attempt 2 사용자 관찰 |
| 실제 Windows 캐릭터 원형 | PASS | attempt 3 사용자 관찰 |
| 실제 Windows 최종 몸 선 | PASS | attempt 8 exact artifact + 사용자 `좋다` |
| 닫힌 눈의 기본 위치 | 관찰됨 | attempt 8 정지 화면에서 얼굴 내부에 위치 |
| 닫힌 눈의 표정·애니메이션 품질 | UNVERIFIED | 다음 단계 수정 요청 |
| WALK/CLICK 및 상황별 풍부한 동작 품질 | UNVERIFIED | 다음 단계 구현 대상 |
| 투명 여백 클릭 통과·포커스 보존의 전체 상태 실측 | UNVERIFIED | 남은 실제 Windows 순차 검증 필요 |
| 전체 Milestone 1 | PARTIAL | 위 미검증 항목 때문에 완료로 승격하지 않음 |

## 6. 실제 Windows 시도 이력

| 시도 | 결과 | 핵심 관찰 |
|---|---|---|
| attempt 2 | PARTIAL / 나머지 UNVERIFIED | 임시 갈색 캐릭터가 보이고, 무테·투명·일반 창 위 유지 PASS. 실제 도로롱이 아니라는 정체성 교정 발생. |
| attempt 3 | FAIL | 도로롱 원형 PASS, 몸/머리 선 불일치 FAIL, 닫힌 눈 이탈 FAIL. |
| attempt 4 | FAIL | 몸 선이 아직 두껍고 굵기가 불균일. |
| attempt 5 | FAIL | 손으로 다시 그린 몸 윤곽의 스트로크가 안팎으로 삐져나옴. |
| attempt 6 | FAIL | 돌출 가지는 줄었으나 머리 선 기준의 단일 굵기를 충족하지 못함. |
| attempt 7 | FAIL | candidate F 몸이 여전히 무겁고 내부 C 선이 옅게 남음. |
| attempt 8 | 몸 선 PASS / 전체 PARTIAL | E-only `W=1.5` 산출물을 사용자가 `좋다`로 승인. 닫힌 눈 표현 개선과 상황별 애니메이션은 다음 단계. |

자동 GUI 도구의 캡처/입력 실패는 별도 환경 제한이다. 어느 시도의 제품 PASS나 FAIL도 자동화 실패에서 추론하지 않았다.

## 7. 최종 산출물과 해시

### 7.1 실행 자산

- runtime open: `238AC7F0ACC765ABC40AE3E13543E088BC3F694C0D4FBC99BDFD99648D94B511`
- runtime closed: `F48AB174F6DEE6C92F04E7363F854CC92AA1ED53504A728F7F69E8F1D0A0167E`
- source open after body reconstruction: `D1F0770CBCA95FC79B5E68642D78A5A48077834495C9ECDBCD73B34545AC94FF`
- source closed after body reconstruction: `70BC4C304CA8DF2D38A2FC2A451CCA8BF7A3099B8B4C81551869891511406551`

### 7.2 권위 데이터

- immutable source: `F96EC30CBD18429E6BA1138BFA4EB44F331974C9820D36EE97A02FE518E46504`
- seed: `E256F3DC28929A49624C6308F77C994F061240CB7D2C9E80780AAD4A300C0779`
- body mask: `D08B3A941C662F1CBC55C486C13FD4C6CD8901DA9CD5CF8512509698219FE46F`
- E/C contour: `A29D007B699A16B555FE5133854E832FEFD8409EA85F2FECE3EEA97BF444FD65`
- proxy membership: `2B9CB6D649884DA2DC826963E3258A23DAFAAE9A1B071335B168834746463A54`
- contour records: exposed E 382, continuation C 2, total 384.
- proxy: 6 components, 12 pixels.

### 7.3 publish

- `Dororong.App.exe`: `C0EB20CCEED12E2430F499D6274D219515940C91DED5EB33EBD207580C2A3533`
- `Dororong.App.dll`: `F606F2EB8D46F541EF71298A1AD73C8F658DA41CC08E32A30D82116B6E0930FE`
- `Dororong.Core.dll`: `E0B24FCCA21D23B073A6E569646CF551B2685B6A929B2DDCEA586917BEB24BAE`
- published App DLL에서 추출한 열린 눈/닫힌 눈 리소스는 위 runtime 해시와 바이트 단위로 일치했다.

### 7.4 attempt 8 사용자 증거

- 파일: `docs/verification/evidence/attempt-8-user-phase-1-acceptance-and-closed-eyes-next.png`
- SHA-256: `5DC4871E60F4A5AC7EFC0D9A33392AF19C825A7A51AEC349746E73E4268DCCE1`
- 크기: 127x152
- 실행 PID: `15004`
- 생성 시각: `2026-08-28T19:15:25.7559410+09:00`
- 관찰 뒤 정확한 PID/경로/생성 시각을 대조해 종료했고 동일 경로 생존 프로세스는 0이었다. 자동 재실행하지 않았다.

## 8. 실패한 접근과 남길 교훈

### 8.1 이미지 전체 재생성

몸 선만 고치려 했지만 캐릭터 전체가 다시 그려져 정체성이 달라졌다. 이미지 생성 결과는 이 캐릭터의 정밀 보정 권위로 사용하지 않는다. 향후 새로운 표정/동작 프레임을 만들더라도 원본 보호 영역과 승인된 비율·선 기준을 고정하고, 생성 결과는 자동 채택하지 않는다.

### 8.2 native 96px 사후 패치

일부 좌표를 지우거나 옅게 하면 한 장에서는 나아 보여도 선 굵기가 구간별로 달라지고 다른 프레임과 어긋난다. 몸 선은 source 225px 권위에서 생성하고 마지막에 한 번만 축소한다.

### 8.3 손으로 새 몸 실루엣 그리기

원래 세 다리/두 골짜기와 리본 주변 관계가 바뀌기 쉽고 내부 어두운 픽셀이 생겼다. 승인된 소스 실루엣을 새 곡선으로 대체하지 않는다.

### 8.4 제한된 픽셀만 밝게 만들기

삐져나온 한 부분은 줄지만 몸 전체의 시각적 무게를 머리 선에 맞출 수 없었다. 전체 몸 소유 영역을 일관된 규칙으로 지우고 다시 그려야 했다.

### 8.5 저장소 검사만으로 실제 화면 통과 추정

candidate F는 자동화와 정적 시각 검사를 통과했지만 실제 Windows에서 실패했다. WPF 조합과 실제 배경에서 사용자가 본 결과가 최종 시각 승인이다.

### 8.6 내부 연장선의 낮은 불투명도

`C gain=0.125`는 “약하게 남기는” 결과였고 사용자는 명시적으로 거부했다. 최종 `C gain=0`처럼 출력에 아예 기여하지 않아야 한다.

## 9. 커밋 이력

아래는 1차 마감 전까지의 저장소 커밋을 오래된 순서로 기록한 것이다. 최종 1차 마감 커밋은 이 문서를 포함해 별도로 추가한다.

```text
7776754 docs: define Dororong milestone one
138e9ee docs: record direct interaction priority
2ff76cb docs: add Dororong implementation plan
59ff982 build: scaffold Dororong behavior core
dce4f1a feat: add autonomous idle and walk behavior
86ab947 fix: preserve deterministic autonomous updates
6a81cae test: cover vertical walk boundary reflection
9d1fea0 feat: react to slow and fast pointer approaches
133971a fix: preserve pointer reaction priorities
5bba691 test: cover pointer reaction boundaries
8768369 fix: preserve retreat direction at pointer center
a3496ea feat: add direct interaction and sleep behavior
fec1102 fix: preserve sleep and direct interaction boundaries
8b0b376 feat: add transparent non-activating WPF shell
262706e fix: harden non-activating window interop
a2d01fd feat: add Dororong vector state presentation
be1f570 fix: center dragged pose in presenter coordinates
ae08967 test: harden dragged angle verification
c4e9f18 feat: run and interact with Dororong on Windows
2ead4c3 fix: harden Dororong runtime lifecycle
e855603 release: verify Dororong milestone one
901090f docs: clarify Dororong verification boundary
055f2dd fix: harden Dororong behavior and runtime integration
307e692 docs: refresh Dororong release evidence
41e66b4 Replace placeholder with canonical Dororong art
e509e37 Record canonical art verification
673fea4 Restore alpha-aware presenter input
d86e88a fix: normalize Dororong body stroke and closed eyes
5495212 fix: remove closed-eye rim remnants
2b05788 Generate Dororong runtime art at native 96px
b9d0d8d fix: clear native valley ink spur
226f616 docs: correct runtime96 red configuration
6e9f1bf docs: replace rejected body redraw contract
fc44d22 docs: plan subtractive body correction
40b87ee fix: thin Dororong body from source ink
54485bb test: freeze reviewed Dororong body thinning
b2d782f docs: record subtractive body verification
7f96ce0 docs: replace body outline reconstruction design
866ef56 docs: plan source-derived body outline
b33d7a3 docs: cover body outline occlusion seeds
49f5710 feat: freeze Dororong body outline authority
1dbab91 fix: correct Dororong outline authority scans
c0fa57e docs: specify continuous Dororong outline
c27ab67 docs: plan continuous Dororong outline
fa0c2e0 docs: correct Dororong solution path
cd03851 test: freeze continuous Dororong outline authority
1414174 docs: clarify opaque native authority samples
d005f40 docs: keep body outline tied to hair weight
2169440 test: align Dororong normals to body contour
645a650 test: bind Dororong normals to named contours
f09599b docs: define complete Dororong body ownership
440c309 docs: plan complete Dororong body ownership
ddf0377 docs: verify Dororong art by behavior
386933c feat: own the complete Dororong body outline
38dba59 test: drop historical body endpoint authority
c9bf952 feat: freeze the Dororong body contour
fd28c7a fix: exclude off-canvas contour edges
a8d5431 test: bind Dororong normals to final ownership
be056e6 docs: approve visual body-outline recovery
f45f2a1 docs: clarify continuation side coverage
b1e95ec feat: render one visible Dororong outline width
34dd95b fix: integrate Dororong candidate F
```

## 10. 다음 단계의 명확한 출발점

다음 작업은 “새 캐릭터를 다시 그리는 것”이 아니라 승인된 도로롱을 유지한 표현 확장이다.

### 10.1 다음 단계에 포함

1. 눈을 감을 때 현재 찡그린 듯한 형태와 위치를 도로롱다운 자연스러운 닫힌 눈으로 다시 설계한다.
2. IDLE, WALK, CURIOUS, STARTLED, CLICK_REACTION, DRAGGED, SLEEP마다 구분되는 표정과 동작을 만든다.
3. 느린 접근, 빠른 접근, 클릭, 드래그, 장시간 방치, 깨우기라는 원인을 사용자가 동작만 보고 이해할 수 있게 한다.
4. 캐릭터가 계속 떨거나 반응을 반복하지 않도록 현재 상태 최소 유지 시간과 쿨다운을 보존한다.
5. 프레임이 바뀌어도 알파 기반 클릭 영역, 투명 영역 클릭 통과, 창 경계, 비활성 동작을 유지한다.
6. 실제 Windows에서 한 행동씩 사용자에게 보여주고 관찰 결과를 PASS / FAIL / UNVERIFIED로 갱신한다.

### 10.2 다음 단계 전에 먼저 정할 것

- 각 상태의 표정, 몸 자세, 움직임의 최소 세트.
- 기존 원본에서 변형 가능한 부분과 절대 보호할 부분.
- 프레임 애니메이션과 WPF 변형을 어떤 동작에 쓸지.
- 닫힌 눈 한 장을 먼저 승인받을지, SLEEP 전체 짧은 동작으로 승인받을지.
- 실제 Windows 확인 순서와 한 번에 보여줄 항목 수.

### 10.3 계속 제외

먹이, 호감도, 경험치, 계정, 클라우드, AI 대화, 외부 서비스, 복잡한 설정, 자동 시작, 정교한 멀티 모니터, 사운드는 사용자가 별도로 범위를 바꾸기 전까지 다음 단계에 자동 포함하지 않는다.

## 11. 다음 작업자용 시작 순서

1. `docs/specs/2026-08-26-dororong-m1-design.md`에서 제품·행동·비방해 계약을 읽는다.
2. `docs/verification/2026-08-28-m1-windows-acceptance-manual-attempt-8.md`에서 1차 PASS와 아직 미검증인 범위를 확인한다.
3. 현재 열린 눈/닫힌 눈 런타임 자산과 원본 권위 해시를 검사한다.
4. 닫힌 눈과 상태별 연출의 작은 영속 명세를 작성하고 사용자에게 캐릭터성에 영향을 주는 선택만 묻는다.
5. 승인된 몸 선 생성 파이프라인을 보존한 채 테스트 우선으로 한 상태씩 구현한다.
6. 정적 이미지 검사와 자동화만으로 실제 화면 PASS를 선언하지 않는다.

## 12. 저장 위치와 외부 메모 상태

이 저장소 문서가 1차 인계의 기준 기록이다. Basic Memory에도 동일한 인계 메모를 만들려고 했지만 현재 계정의 Basic Memory 구독 만료로 저장할 수 없었다. 따라서 외부 메모의 존재를 가정하지 말고 이 파일과 저장소의 명세·검증 문서를 기준으로 이어간다.
