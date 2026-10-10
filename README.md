# 나랏말싸미 (Nalasmalssami) — SPT 4.1 아이템 태그 한글 입력 모드

---

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** FalconPilot (SPT Tag over name) / acidphantasm (More Tag Colours)
**Original Repository:** spt-tag-over-name / acidphantasm-moretagcolours
**Original Link:** https://github.com/FalconPilot/spt-tag-over-name / https://github.com/acidphantasm/acidphantasm-moretagcolours
**License notation:** 나랏말싸미 MIT (Copyright (c) 2026 R_F). 가져온 코드의 원본도 둘 다 MIT — FalconPilot (c) 2026, acidphantasm (c) 2025. 원문은 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## 변경 이력

- 2026-10-10 16:38 — 1.0.0 첫 버전. 태그 입력 칸에서 한글이 바로 지워지던 문제를 고침(게임이 허용 문자 목록에 없는 글자를 지우는데, 그 목록에 한글을 추가). 한글이 네모로 보일 때 쓸 예비 글꼴, 조합 중인 마지막 글자 살리기, 입력기 강제 켜기를 넣음. "태그를 이름 위에"·"태그 색상 확장" 기능을 같이 넣고, F12에서 바꾸면 인벤토리를 다시 열지 않아도 바로 적용되게 함.

---

## 이 모드가 하는 일

| 기능 | 설명 | 출처 |
|---|---|---|
| **한글 태그 입력** | 아이템 태그에 한글을 쓸 수 있게 함 (핵심 기능) | 오리지널 |
| 한글 글꼴 보강 | 한글이 네모(□)로 보이면 한글 글꼴을 예비 글꼴로 붙임 | 오리지널 |
| 조합 중인 글자 살리기 | 마지막 글자가 밑줄(조합 중) 상태일 때 저장해도 안 사라지게 | 오리지널 |
| 입력기 강제 켜기 | 태그 칸에서 한/영 전환이 안 될 때를 위한 안전장치 | 오리지널 |
| 최대 글자 수 조절 | 태그 글자 수 한도를 바꿈(0 = 게임 기본) | 오리지널 |
| **태그를 이름 위에** | 태그가 있으면 아이템 짧은 이름을 숨겨서 태그가 잘리지 않게 | [FalconPilot / spt-tag-over-name](https://github.com/FalconPilot/spt-tag-over-name) |
| **태그 색상 확장** | 태그 색 7개 → 18개 + 회색 9단계, 색은 F12에서 직접 지정 | [acidphantasm / acidphantasm-moretagcolours](https://github.com/acidphantasm/acidphantasm-moretagcolours) |
| 색 선택 창 안전장치 | 색이 많은 모드를 쓰다 뺐을 때 태그 창이 안 열리는 문제 방지 | 오리지널 |

### 왜 한글이 안 써졌나 (원인)

게임의 태그 입력 칸(`ValidationInputField` — 입력값을 검사하는 입력 칸)은 글자가 바뀔 때마다
"허용 문자 목록"에 없는 글자를 지워 버립니다. 태그 칸의 목록은 영문·숫자 위주라 한글을 치면 바로 사라졌습니다.
나랏말싸미는 **태그 창 입력 칸 하나에만** 이 목록에 한글을 추가합니다(닉네임 칸 등 다른 입력 칸은 그대로).

## 설치

1. `Nalasmalssami-1.0.0.zip`을 SPT 폴더(`E:\SPT 4.1`)에 풀기 → `BepInEx\plugins\Nalasmalssami\Nalasmalssami.dll`
2. 서버 모드는 없습니다(클라이언트 전용).

## F12 설정 (바꾸면 바로 적용)

| 분류 | 설정 | 기본값 | 설명 |
|---|---|---|---|
| 0. 정보 | 원본 모드 감지 | — | 원본 두 모드 설치 여부(읽기 전용) |
| 1. 한글 입력 | 한글 태그 입력 허용 | 켬 | 끄면 게임 기본 규칙 |
| | 허용 문자 범위 | 한글 | `한글` / `한글 + 일본어·한자` / `모든 문자`(제어 문자만 제외) |
| | 최대 글자 수 | 0 | 0 = 게임 기본, 최대 64 |
| | 입력기(IME) 강제 켜기 | 켬 | 태그 칸에 커서가 있을 때만 |
| | 저장 시 조합 중인 글자 살리기 | 켬 | |
| | 한글 글꼴 보강 | 켬 | 게임 안 한글 글꼴 → 없으면 윈도우 '맑은 고딕' |
| 2. 태그를 이름 위에 | 태그 있으면 아이템 이름 숨기기 | 켬 | 원본은 인벤토리를 다시 열어야 적용됐지만, 여기선 바로 적용 |
| 3. 태그 색상 확장 | 태그 색상 확장 사용 | 켬 | 끄면 게임 기본 7색 |
| | 회색 9단계 포함 | 켬 | 색 선택 창에 회색 표시 여부 |
| | 태그 창 크게 | 켬 | 450×260 |
| | 색 01 ~ 색 18 | 원본과 같음 | 색 바꾸면 인벤토리 태그 색도 바로 바뀜 |

- 태그 창이 열린 상태에서 한글 설정을 바꿔도 바로 적용됩니다. 색 선택 창의 색 목록은 태그 창을 다시 열면 바뀝니다.

## 원본 모드와 같이 쓸 때

원본 모드를 따로 설치해도 충돌하지 않게 했습니다.

- **TagOverName이 설치되어 있으면** → 나랏말싸미의 "태그를 이름 위에"는 자동으로 꺼지고 원본이 처리합니다.
- **MoreTagColours가 설치되어 있으면** → 나랏말싸미의 "태그 색상 확장"은 자동으로 꺼지고 원본이 처리합니다.
- 꺼진 기능은 F12에 `(원본 모드 사용 중 — 꺼짐)`으로 표시되고 바꿀 수 없습니다. `0. 정보 → 원본 모드 감지`에서도 확인할 수 있습니다.
- 한글 입력·글꼴 보강·색 선택 창 안전장치는 원본에 없는 기능이라 항상 동작합니다.
- 색 번호 순서와 기본 색을 MoreTagColours와 똑같이 맞춰서, 두 모드를 바꿔 써도 같은 태그가 같은 색으로 보입니다.

## 주의

- 태그 색은 프로필에 "번호"로 저장됩니다. 색상 확장을 끄거나 모드를 빼면 8번 이후 색으로 단 태그는 1번 색으로 보입니다(태그 글자와 번호는 그대로 남아 있어서, 다시 켜면 원래 색).
- 한글 태그는 프로필에 그대로 저장됩니다. 모드를 빼도 태그 글자는 남고 표시도 되지만, 그 태그 창을 다시 열면 게임 기본 규칙이 한글을 지울 수 있습니다.
- `모든 문자`에서 이모지처럼 글꼴에 없는 글자는 네모로 보일 수 있습니다.

## 검증 상태

- `dotnet build` 성공 (경고 0) — 리눅스 컨테이너, BepInEx 5.4.23.2 + Unity 2021.3 참조.
- 게임 클래스·멤버 이름은 SPT 4.1 `Assembly-CSharp.dll` 디컴파일로 대조.
- 허용 문자 정규식은 게임 플래그 조합 2,000가지로 패턴 오류 0건, 한글(입력 중 낱자 포함) 통과를 확인.
- **인게임 동작은 아직 미검증** — 코드상 그래야 함. 문제가 있으면 `BepInEx/LogOutput.log`의
  `[한글 입력]`, `[한글 글꼴]` 줄을 같이 알려 주세요(태그 칸의 게임 기본 설정값이 기록됩니다).

## 직접 빌드

```
dotnet build -c Release            # 기본 SPT 경로: E:\SPT 4.1
dotnet build -c Release -p:SPTPath="D:\SPT"
```

빌드하면 `BepInEx\plugins\Nalasmalssami\`에 바로 복사되고 `release\Nalasmalssami-1.0.0.zip`이 만들어집니다.
게임 어셈블리(`Assembly-CSharp`, TextMeshPro)는 참조하지 않고 이름으로 찾습니다 — 게임 업데이트로 이름이 바뀌면
그 기능만 꺼지고 로그에 남습니다.

## 크레딧

- **FalconPilot** — [SPT Tag over name](https://github.com/FalconPilot/spt-tag-over-name) (MIT). `src/Features/TagOverName.cs`의 동작 원리와 코드.
- **acidphantasm** — [More Tag Colours](https://github.com/acidphantasm/acidphantasm-moretagcolours) (MIT). `src/Features/TagColours.cs`의 색상표 확장 방식과 기본 색상값.
- 나랏말싸미 — R_F
