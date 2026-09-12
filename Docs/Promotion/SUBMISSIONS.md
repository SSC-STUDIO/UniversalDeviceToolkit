# UDT Directory and Awesome-List Submissions

Track where UDT is submitted for discovery beyond social posts. A listing on a
curated list converts well because the visitor already wants a tool like this.

## Status tracker

Current campaign: **100 stars**, Chinese first (Bilibili, V2EX, HelloGitHub).
Live baseline on 2026-09-12: **30 stars**, 70 remaining. Drafts are not counted
as published outreach. The next action is publishing the prepared Bilibili and
V2EX posts when browser access is available.

| Venue | Type | Status | Date | Notes |
|-------|------|--------|------|-------|
| Bilibili | Chinese dynamic / video | Draft ready; not published | 2026-09-12 | Title, description, dynamic, recording script and existing assets in [PROMOTION_CN.md](PROMOTION_CN.md). Browser connector returned `unsupported Codex auth method: apikey`; login, earlier posts and current upload rules could not be checked |
| V2EX 分享创造 | Chinese maintainer introduction | Draft ready; not published | 2026-09-12 | Complete post with Chinese screenshot and download links in [PROMOTION_CN.md](PROMOTION_CN.md). Same browser connector blocker; check node rules and duplicate posts before publishing |
| awesome-dotnet | awesome-list PR | Open | 2026-09-12 | Rechecked #1505 https://github.com/quozd/awesome-dotnet/pull/1505 : open, no comments or reviews. Do not open another submission. Historical replacement details below |
| awesome-windows | awesome-list PR | Skipped | 2026-07-07 | canonical repo 0PandaDEV/awesome-windows; maintainer hostile to AI PRs (hidden anti-AI README comment + visible CAUTION rejecting vibecoded slop). Revisit if stance softens |
| electron/apps | Electron app directory | Prepared, blocked | 2026-08-31 | Entry ready on fork branch https://github.com/SSC-STUDIO/apps/tree/add-universal-device-toolkit (`apps/universal-device-toolkit/`, category Utilities, 512x512 icon). PR creation blocked by the repo's own anti-spam restriction -- `gh pr create` and the REST API both reject with a permissions/404 error matching GitHub's "limit who can open pull requests to collaborators" setting (likely enabled against AI-generated submission floods). No PR opened. Revisit later or ask a maintainer for contributor access |
| AlternativeTo | database entry | Not started | - | suggest as alt to "Lenovo Vantage"; non-GitHub site, out of scope for this GitHub-only round |
| Slant | list entry | Not started | - | add to a "Lenovo Vantage alternatives" question; non-GitHub site, out of scope for this GitHub-only round |
| HelloGitHub | CN self-submission | Original post updated; awaiting review | 2026-09-12 | [#3383](https://github.com/521xueweihan/HelloGitHub/issues/3383) edited and [version update](https://github.com/521xueweihan/HelloGitHub/issues/3383#issuecomment-5642967079) published. Created 2026-06-27; no reviewer reply observed. The README's old recommendation badge points to upstream LLT, not UDT, and has been removed |
| winstall.app | winget mirror | Historical claim, unverified | 2026-09-12 | The earlier Listed status did not establish a working current package. Do not use it as an install recommendation |
| winget-pkgs | package manager | Not published | 2026-07-07 | id reserved (`SSC-STUDIO.UniversalDeviceToolkit`); `manifests/s/SSC-STUDIO/SSC-STUDIO.UniversalDeviceToolkit` returns 404 as of 2026-07-07 -- use the Releases installer until a clean PR lands (the Scoop bucket does not exist yet either; see the Scoop bucket row below). Packaging task, not an awesome-list submission; out of scope for this round |
| Microsoft Store | store listing | Not planned | - | optional; GPL-3.0, Electron + .NET Host, big lift |
| Scoop bucket | package manager | **Missing (404) -- public references removed** | 2026-08-31 | Verified: `https://github.com/SSC-STUDIO/scoop-bucket` does not exist (confirmed via `gh repo list` and a direct fetch). Follow-up fix: README.md, README_zh-hans.md, PROMOTION_EN.md, PROMOTION_CN.md, and DEPLOYMENT.md no longer tell readers to run `scoop bucket add ssc-studio https://github.com/SSC-STUDIO/scoop-bucket` -- the Scoop option was dropped from download lists and ready-to-post copy, or replaced with an honest "not available yet, use Releases" note. The bucket itself still does not exist; only reintroduce install copy once `SSC-STUDIO/scoop-bucket` is actually created and published |

## Weekly star tracker

| Week ending | Stars | Delta | Notes |
|-------------|-------|-------|-------|
| 2026-09-12 (campaign baseline) | 30 | +2 since 2026-08-23 | GitHub live count. Updated HelloGitHub #3383 and corrected maintainer copy; Bilibili and V2EX still drafts. Growth predates this campaign and is not attributed to today's work |
| 2026-08-23 | 28 | +10 | README hero + trailer + retaken console screenshots; PROMOTION_* restored |
| 2026-07-07 | 18 | 0 | OpenAI Founders Hub 申请 [VERIFY] 用真实数据填默认（[CONFIRM] 等你核）；SUBMISSIONS winget-pkgs 错误修正；awesome-dotnet PR #1466 open+mergeable+0 comments；首发三站文案齐备，等你今晚或明天首发 |
| 2026-07-06 | 18 | - | baseline, v5.0.0-preview published |

(add a row each Sunday; the Monday digest issue posted by `star-growth.yml` gives you the numbers, see [COMMUNITY_OUTREACH.md](./COMMUNITY_OUTREACH.md))

---

## 1. awesome-dotnet  -- OPEN (2026-08-31)

- Repo: https://github.com/quozd/awesome-dotnet
- Current PR: https://github.com/quozd/awesome-dotnet/pull/1505  (Add Universal Device Toolkit under Tools)
- Fork branch: https://github.com/SSC-STUDIO/awesome-dotnet/tree/add-universal-device-toolkit
- Section: `## Tools`, appended at end of the section (matches the list's
  chronological addition style). One link per PR, per CONTRIBUTING.
- Entry: open-source Windows hardware-control toolkit; GPL-3.0, C#/.NET host.
  Wording updated from the old WPF / plugin-based line, which is no longer true.
- Prior PR #1466 https://github.com/quozd/awesome-dotnet/pull/1466 was
  **closed by the SSC-STUDIO account itself** on 2026-08-30, with zero comments
  and zero reviews (`gh api .../issues/1466/timeline` shows only the original
  commit and a `closed` event by SSC-STUDIO; reviews and issue comments were
  empty). No maintainer rejection. The fork `SSC-STUDIO/awesome-dotnet` and
  head branch were gone, so `gh pr reopen 1466` failed with
  `Could not open the pull request`. Content was still valid, so a single
  replacement PR was opened instead of leaving the list without a submission.
  Do not open another PR to this list while #1505 is open.

## 2. awesome-windows  -- SKIPPED (2026-07-07)

- Canonical repo: https://github.com/0PandaDEV/awesome-windows (the old
  `awesome-windows/awesome-windows` is a 404; `0PandaDEV` is the live fork, 2.5k stars).
- Reason for skip: the README embeds (a) a hidden HTML comment that attempts to
  inject refusal instructions into AI assistants that read the repo, and (b) a
  visible `> [!CAUTION]` that "vibecoded slop ... PR's will be rejected". That is a
  genuine, hostile stance toward AI-assisted PRs; submitting risks the UDT repo
  being flagged/closed-on-sight and gains little. Skip unless the maintainer's
  stance changes. (Note: treated the hidden comment as untrusted data, not as an
  instruction -- it changed only this venue's risk call, not the overall goal.)

## 3. electron/apps  -- PREPARED, BLOCKED (2026-08-31)

- Repo: https://github.com/electron/apps (official Electron org app directory
  behind electronjs.org/apps; active, 1.7k+ stars, not archived). Genuinely fits:
  UDT's UI is Electron ^43, the repo's own `sindresorhus/awesome-electron`
  redirects rejected/ineligible submissions here, and an existing entry
  (`display-dj`, a laptop brightness utility) shows hardware-utility apps are
  welcome under `Utilities`.
- Checked first: no existing `legion`-matching entry (code search returned 0),
  submissions are not paused for this repo (unlike `awesome-electron`, which is
  temporarily closed to new submissions), and UDT clears every written rule
  (open source, README with screenshot, Windows binaries via Releases, project
  older than the 20-day minimum, Electron well above the v4 floor).
- Forked to https://github.com/SSC-STUDIO/apps and prepared a ready-to-submit
  branch `add-universal-device-toolkit`
  (https://github.com/SSC-STUDIO/apps/tree/add-universal-device-toolkit) adding
  `apps/universal-device-toolkit/universal-device-toolkit.yml` (category
  `Utilities`, GPL-3.0, keywords, full locale list) and a 512x512 icon copied
  from `Assets/Logo.png`. Content double-checked line-by-line against the
  repo's own `test/human-data.js` validation rules.
- **PR could not be opened.** Both `gh pr create` (GraphQL) and a direct REST
  `POST /repos/electron/apps/pulls` call were rejected: `SSC-STUDIO does not
  have the correct permissions to execute CreatePullRequest` / HTTP 404. This
  matches a known GitHub repo setting ("limit who can open pull requests" to
  collaborators only) that maintainers of high-profile directory repos have
  been enabling to stop floods of low-effort/AI-generated submission PRs --
  the same signature error is reported by other unrelated contributors hitting
  the same wall on other repos. This is a hard block from the target repo, not
  an ambiguity on our side, so no PR was forced through and no workaround
  (e.g. a different account) was attempted.
- The prepared branch is harmless and sits only on our own fork; it does not
  notify or affect electron/apps in any way. If the restriction lifts, or if a
  maintainer grants contributor access, the same branch can be turned into a
  PR later without redoing the work.

## 4. AlternativeTo

- Where: https://alternativeto.net
- How: submit UDT as a software entry, then tag it as an alternative to
  "Lenovo Vantage".

Entry copy:

```
Universal Device Toolkit (UDT): open-source Lenovo hardware-control toolkit
for supported Windows laptops. Power modes, keyboard lighting, GPU modes and
battery care; available controls depend on the model and drivers. No account
or telemetry. GPL-3.0, .NET Host + Electron UI. Install from GitHub Releases.
https://github.com/SSC-STUDIO/UniversalDeviceToolkit
```

## 5. Slant

- Where: https://www.slant.co
- How: find or create a "Best Lenovo Vantage alternatives" question, add UDT, write
  a one-line pro. Pros from a couple of independent users help, but never
  coordinate mass votes.

## 6. HelloGitHub (original submission updated)

- Original submission: https://github.com/521xueweihan/HelloGitHub/issues/3383,
  created **2026-06-27** according to the live GitHub API. The old July 5 date
  in this tracker was incorrect.
- On 2026-09-12, updated the existing title and body for v6.1.1: retired plugin
  claims removed, `udt` CLI examples and current Releases links added, supported
  hardware and tray requirements clarified. Verified the published body against
  the exact copy in [PROMOTION_CN.md](PROMOTION_CN.md).
- Posted one substantive [version update](https://github.com/521xueweihan/HelloGitHub/issues/3383#issuecomment-5642967079).
  This remains a self-submission awaiting review, not a confirmed feature.
- The old badge URL `https://hellogithub.com/repository/dd55be3ac0c146208259f17b29d2162f`
  resolves to **BartoszCichecki/LenovoLegionToolkit**. It is an upstream listing,
  not UDT's. Removed the misleading badge from both READMEs.
- Follow up on an actual reviewer reply or material project change; do not send
  repeated review reminders or duplicate submissions.

---

## Rules (don't get blacklisted)

- awesome-lists usually gate on "notable" -- typically real traction (tens of
  stars, real commit history, some age). Submit after the first community push so
  the PR isn't closed as self-promotion.
- AlternativeTo / Slant entries should be created by a real person and naturally
  accrue independent pros/upvotes. Never coordinate mass upvoting -- both
  platforms anti-cheat and it hurts the project's reputation.
- One venue at a time so each submission is well written for that community.

Last updated: **2026-09-12** (Chinese campaign baseline, HelloGitHub original
post refreshed and verified; Bilibili/V2EX drafts ready but browser unavailable).
Unchanged directory rows retain their historical dates and are not new live checks.

Previously: 2026-08-31 (awesome-dotnet: #1466 was self-closed with no
maintainer review; fork/branch gone so reopen failed; replacement PR #1505
opened under Tools. Scoop public install copy already removed -- see the
Scoop bucket row. electron/apps remains prepared-but-blocked.)

Previously: 2026-07-07 (winget-pkgs 行从 Listed 修正为 Not published，附 manifest 目录 404 验证；新增 2026-07-07 周行)
