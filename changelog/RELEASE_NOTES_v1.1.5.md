# 나랏말싸미 (Nalasmalssami) v1.1.5

SPT 4.1용 클라이언트 모드입니다. 아이템 태그에 **한글을 쓰고, 인벤토리 칸에서도 한글로 보이게** 합니다.
A client-side SPT 4.1 mod that lets you write item tags in **Korean (Hangul)** and shows them correctly in inventory grid cells.

---

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** FalconPilot (SPT Tag over name) / acidphantasm (More Tag Colours)
**Original Repository:** spt-tag-over-name / acidphantasm-moretagcolours
**Original Link:** https://github.com/FalconPilot/spt-tag-over-name / https://github.com/acidphantasm/acidphantasm-moretagcolours
**License notation:** Nalasmalssami MIT (c) 2026 R_F. Bundled features are based on MIT-licensed code — FalconPilot (c) 2026, acidphantasm (c) 2025. See `THIRD-PARTY-NOTICES.md`.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## 주요 기능 / Features

| 기능 | Feature |
|---|---|
| **한글 태그 입력** — 태그 창에서 한글이 지워지지 않음 (태그 창 입력 칸에만 적용) | **Hangul tag input** — the tag window no longer deletes Korean characters (tag field only) |
| **인벤토리 칸 한글 표시** — 칸 태그에서 한글이 빈칸으로 보이던 문제 해결 | **Hangul in grid cells** — fixes Korean tags showing as an empty bar |
| 조합 중인 마지막 글자 살리기, 입력기(IME) 강제 켜기, 최대 글자 수 조절 | Keep the composing character on save, force IME, adjustable max length |
| **태그를 이름 위에** (FalconPilot 원작) — 태그가 있으면 아이템 짧은 이름을 숨김 | **Tag over name** (by FalconPilot) — hides the short name when a tag exists |
| **태그 색상 확장** (acidphantasm 원작) — 7색 → 18색 + 회색 9단계, 색 직접 지정 | **More tag colours** (by acidphantasm) — 7 → 18 colours + 9 greys, editable |
| 태그 글자색 자동 대비 — 밝은 막대에는 검은 글씨 | Auto text contrast — black text on bright bars |
| 태그 프리셋 버튼 — "팔 것", "퀘템" 등을 한 번에 입력 | Tag preset buttons |
| 태그로 창고 검색 — `#검색어`는 태그만 검색 | Search the stash by tag — `#word` searches tags only |
| 태그 글자 크기·굵기·모양·위치 조절 | Tag font size / weight / look / position |
| F12 디버그 로그 · 상태 기록 | F12 debug log and one-shot state dump |

모든 설정은 F12(ConfigurationManager)에서 바꾸는 즉시 적용됩니다. 인벤토리를 다시 열 필요가 없습니다.
All settings apply immediately from F12 (ConfigurationManager).

## 이번 버전 변경점 / Changes in 1.1.5

- **태그 글자 굵기가 오락가락하던 문제 수정.** 아이템을 옮기면 얇아지고 메인 메뉴를 다녀오면 다시 두꺼워졌습니다. 굵게 설정 때문이 아니라, 한글 태그를 그리는 글꼴이 그때그때 달랐기 때문입니다(아이템 이름이 숨겨지기 전이면 이름 글자의 테두리 모양, 숨겨진 뒤면 테두리 없는 한글 글꼴). 이제 F12 `태그 한글 모양` 설정 하나로만 정해집니다.
  Fixed tag text switching between thick and thin after moving items or returning to the main menu. The Korean font used for tags depended on whether the item name was still visible; it is now decided only by the new `Hangul tag look` setting.
- **새 설정 `태그 한글 모양`**: `깔끔하게 (테두리 없음)`(기본) / `이름 글자처럼 (테두리)`. / New setting: clean (no outline, default) or outlined like item names.
- **`태그 글자 굵게` → `태그 글자 굵기`**: `게임 기본 / 굵게 / 안 굵게`. 예전에는 꺼도 게임 원래 값이 굵으면 굵게 남았습니다. / Bold toggle replaced by a 3-way weight option (game default / bold / not bold).
- **태그 글자 위치 조절 추가**: 좌우·위아래 각각 -10 ~ 10픽셀. 색 막대는 그대로, 글자만 움직입니다. / New X/Y text offset (-10 to 10 px); the colour bar stays put.

## 설치 / Install

1. `Nalasmalssami-1.1.5.zip`을 SPT 폴더에 풉니다 → `BepInEx\plugins\Nalasmalssami\Nalasmalssami.dll`
   Extract into your SPT folder.
2. 클라이언트 전용입니다. 서버 모드는 없습니다. / Client only, no server mod.
3. 1.1.x에서 올리는 경우 그대로 덮어쓰면 됩니다. 설정은 유지되지만, 예전 `태그 글자 굵게` 체크는 `태그 글자 굵기 = 게임 기본`으로 새로 시작합니다.
   Overwrite when updating from 1.1.x; settings are kept, except the old bold toggle, which restarts as `Tag weight = game default`.

## 호환성 / Compatibility

- SPT 4.1.x (4.1.6에서 테스트) / Tested on SPT 4.1.6.
- **원본 모드와 함께 설치해도 충돌하지 않습니다.** TagOverName 또는 MoreTagColours가 있으면 해당 기능은 자동으로 꺼지고 원본이 처리합니다.
  Safe alongside the original mods: the overlapping feature turns itself off and the original handles it.
- 고라니 한글화(Korean Patch Fix 2.1.1)와 함께 쓴 환경에서 확인했습니다. / Checked together with Korean Patch Fix 2.1.1.

## 검증 상태 / Verification

- `dotnet build` 경고 0, 오류 0. / Builds with 0 warnings, 0 errors.
- 인게임 확인(제작자 테스트): 한글 입력·저장, 인벤토리 칸 한글 태그 표시(1.1.4), 아이템 상세 창, 색상 확장·프리셋 등 나머지 기능(1.1.1).
  In-game: Hangul input and save, grid display (1.1.4), inspect window and the other features (1.1.1).
- **1.1.5 변경분(굵기 고정·모양·위치 설정)은 아직 인게임 미확인입니다.** / The 1.1.5 changes are not yet tested in-game.

## 크레딧 / Credits

- **FalconPilot** — [SPT Tag over name](https://github.com/FalconPilot/spt-tag-over-name) (MIT)
- **acidphantasm** — [More Tag Colours](https://github.com/acidphantasm/acidphantasm-moretagcolours) (MIT)
- 나랏말싸미 — R_F
