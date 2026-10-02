# Community outreach

How to tell people about UDT without looking like spam. Ready-to-paste posts: [PROMOTION_EN.md](./PROMOTION_EN.md) and [PROMOTION_CN.md](./PROMOTION_CN.md). Directory listings: [SUBMISSIONS.md](./SUBMISSIONS.md).

## Current campaign: 30 to 100 stars

Verified on 2026-09-12: **30 stars**, **70 remaining**. The user chose Chinese outreach first: Bilibili, V2EX and HelloGitHub. The goal is 100 real stars; there is no guaranteed conversion rate or completion date.

Use the maintainer copy in [PROMOTION_CN.md](PROMOTION_CN.md), real screenshots and the current Releases installer. Star growth alone does not identify which post caused it; record published URLs and actual compatibility feedback alongside counts.

1. Update the existing [HelloGitHub submission](https://github.com/521xueweihan/HelloGitHub/issues/3383) with v6.1.1, built-in capabilities and working download links. Its original plugin and package-manager copy is obsolete.
2. Publish one Bilibili dynamic with the Chinese screenshot. The video title, description and a new 30-second recording script are ready; review the existing trailer before uploading it.
3. Read the current V2EX node rules and search for earlier UDT posts, then publish the maintainer introduction in 分享创造 once. Collect full model names, Windows versions and requested controls.
4. Revisit actual replies after publication and document reproducible compatibility issues. Publish follow-ups when there is a concrete fix or new demonstration.
5. At 100 stars, record the milestone and conclude this campaign. A larger target needs a separate decision.

Prepared copy is not a published post. Store the actual URL after publishing; if login or tooling blocks a channel, retain its draft status in [SUBMISSIONS.md](SUBMISSIONS.md).

Do not: buy stars, coordinate mass upvotes, or open duplicate "please star" issues.

## Where to post

| Place | Language | Notes |
|-------|----------|--------|
| r/LenovoLegion | EN | Best English fit. Use the Reddit template. Disclose if you maintain the repo. |
| r/SuggestALaptop, r/thinkpad *only if asked* | EN | Do not cold-post; reply when someone wants a Vantage alternative. |
| V2EX 分享创造 | ZH | Use the V2EX template. One post. Reply to model questions. |
| NGA / Chiphell 笔记本版 | ZH | Same Chinese forum post. Attach the README screenshot, not a phone photo of the screen. |
| Bilibili | ZH | Chinese screenshot + maintainer dynamic first. Review trailer claims before uploading. Link Releases. |
| HelloGitHub | ZH | Existing submission #3383, created 2026-06-27; update it rather than opening another issue. |
| AlternativeTo | EN | List as a Lenovo Vantage alternative. Copy is in SUBMISSIONS.md. |

## Maintainer vs user

- Lead with the maintainer role when posting on behalf of SSC-STUDIO.
- Do not invent ownership stories, imply upstream endorsement or use upstream's HelloGitHub listing as UDT's endorsement.
- Never ask a Discord to raid GitHub stars.

## After you post

Add a row to the weekly table in [SUBMISSIONS.md](./SUBMISSIONS.md) (Sunday is fine): stars, delta, which post went up. That is how we know what worked.

## Weekly star digest (GitHub Actions)

`.github/workflows/star-growth.yml` runs every Monday 06:00 UTC (or on demand from the Actions tab). It finds or creates one open issue titled "Star Growth Tracker" and posts a comment with stars, the delta since the previous digest, forks, actual watchers (`subscribers_count`), latest release, progress to **100 stars**, and the remaining count. It reads all comment pages and uses the issue body as the baseline when there are no comments. Its priorities are Bilibili, V2EX and the existing HelloGitHub submission. At 100 it reports that this campaign is complete; it does not raise the goal. The weekly digest itself remains enabled until disabled from Actions -> "Star Growth Tracker" -> "Disable workflow". It has `issues: write` permission only and never commits.

Validate digest behavior with `node --test .github/tests/star-growth.test.mjs` (Bash required; Windows defaults to Git Bash, or set `UDT_TEST_BASH` to its executable). Tests execute the workflow shell with a fake GitHub CLI and do not publish anything.
