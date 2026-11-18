# What Kyle did

The README's [AI usage note](../README.md#ai-usage-note) explains how AI was used. This page answers the
other question a reviewer may have: **where did the human come in?**

Every claim below can be checked in the repo:
- decisions in [DECISIONS.md](DECISIONS.md),
- the session-by-session history in [WORKLOG.md](WORKLOG.md),
- the pull requests and issues on GitHub.

In short: **Claude Code wrote nearly all of the code, tests and docs. Kyle set the direction, made the
decisions, redesigned the process when it went wrong, set up independent checks on the AI's work, and
insisted the record be honest.**

---

## 1. He made the decisions only a human could make

- **Kyle chose the stack and architecture:** C# .NET 10, EF Core with PostgreSQL and migrations, and a
  Vue front end (D-001). He decided on three separate containers for database, API and web (D-003), and
  later switched the front end to the latest Quasar on Node 24 (D-020, D-021). He picked a stack he works
  in every day, so the time went into the problem rather than the tools.
- **Kyle set the product vision:** a modern self-order kiosk, "click a button to begin, scroll through
  options, complete or cancel, send to the kitchen". He also required that errors appear as plain language
  for the customer and as detailed logs for developers. That shaped the kiosk flow and the error contract
  (issues #22 and #32).
- **Kyle answered every open question in the brief** (questions #1–#7, decisions D-037 to D-043). Several
  answers went **against the AI's recommendation**:
  - **Cancel deletes the order (D-040).** Claude recommended a "Cancelled" status. Kyle's choice meant the
    idempotency records had to be designed to outlive the orders they belong to, so a late retry can never
    resurrect a cancelled order. That became one of the main guarantees in the design write-up.
  - **The tip base (D-038).** Kyle didn't accept Claude's first reading of "percentage of the total bill".
    He asked for research into how real POS systems handle it, then challenged the conclusion directly:
    *"Are we sure percentage of the total bill means after tax?"* He chose the industry default (before
    tax), required it to be configurable, and the ambiguity is documented in the README's Assumptions.
  - **Scope (D-037, D-041).** He initially put the full kiosk vision and a simulated payment step in
    scope, then cut both deliberately when the timebox required it (D-046).
- **Kyle set the engineering hygiene:** no direct pushes to `main`, every change through a feature branch
  and a pull request, merge commits only, and branches deleted on merge (D-033; PRs #39 and #40). Few
  solo take-homes bother with this. It's why the commit history reads as one commit per story.

## 2. He designed the way the work was done

- **Kyle required every action and decision to be tracked**, in his very first message. That's why there
  are 48 numbered decisions (each with its reasoning and alternatives) and a work log covering 11
  sessions (D-028, D-029). The record later made an honest AI usage note possible, because it showed who
  did what.
- **Kyle defined the subagent roles:** a planner, a plan reviewer, a developer and a code reviewer, each
  with a written contract ([SUBAGENTS.md](SUBAGENTS.md), D-030).
- **Kyle set up the GitHub project:** a private repo, a project board, and a 38-issue backlog reviewed
  before any code was written (D-027, D-028).

## 3. He corrected course when the process went wrong

- **Kyle reset the process at 21:13 (D-046).** He recognised the planning had become heavier than the
  problem: four opus runs and an 871-line plan for a single screen. He re-planned the rest of the work:
  - **four batched PRs** (pricing, order service, UI, README), one commit per story,
  - **one independent review per PR**,
  - **a cut list** of seven stories,
  - **a time budget per phase**, with the rule "cut the UI before the order service", because the brief
    weights pricing and lifecycle correctness over UI breadth.

  The review that prompted this was written in a separate session. Kyle commissioned it, adopted it,
  and enforced it, and it superseded his own earlier process (D-030) and scope (D-037). This is the main
  reason the build finished.
- **Kyle consolidated parallel work.** He had run a second session on the UI and Playwright tests. When
  the two collided (both used the same decision numbers, and both planned the same story), he decided one
  session would drive and absorb the other's work (WORKLOG Session 4).
- **Kyle stopped work that wasn't worth it.** He cancelled the #31 "edit a placed order" UI partway
  through. The code was discarded, but the story stays open as a documented next step (WORKLOG
  Session 11).

## 4. He had the AI's work checked independently

Kyle ran **separate review sessions that examined `main` cold**, with no stake in the code, and brought
their findings back for the main session to fix. Those reviews caught real problems the main session had
missed:

| Finding | Fixed in |
|---|---|
| Through nginx, the `Location` header of a created order lost its port, and none of the tests covered that path (#48) | PR #51, plus a compose smoke test that failed before the fix and passes after it |
| The `.http` file missed an acceptance criterion of #48 | PR #52 |
| **The AI usage note claimed, in Kyle's voice, verification work that Claude had done** | PR #52 |
| The time section of the README didn't match the repo's own timestamps | PR #53 |

He also asked directly *"Did we answer all the brief questions?"* That final acceptance check found the
last gap: the order-description format was missing from the Assumptions (PR #54).

## 5. He insisted on honesty

- **He verified the work himself rather than claim the AI's checks.** When the AI usage note turned out
  to over-attribute, Kyle didn't just reword it: he ran the check himself. He built the brief's worked
  example in the browser against the running Docker stack (2 × Large with pepperoni and bacon, ILOVEPIZZA,
  a 15% tip) and confirmed **$31.23** against the hand calculation. He placed the order and watched
  **Cancel** disappear the moment the kitchen started it. That's the "What I verified myself" in the
  README (WORKLOG Session 10).
- **He corrected the time spent to the truth.** He gave his real start time. When the repo's history
  showed the session ran about 3 h 10 m, slightly over the 3-hour box, he chose to report that rather
  than a flattering number (PR #53).

## 6. He did the hands-on and gatekeeping work

- **He kept the environment running.** He installed the Docker Desktop update that fixed a recurring
  startup crash, and completed the GitHub sign-in that the project board needed (WORKLOG Sessions 1–2).
- **He merged every pull request.** All 13 merged PRs (#39–#54) went into `main` through him.
- **He questioned choices as they happened:** whether the front end, API and database should be separate
  containers before anything was scaffolded, why a branch was named `chore/`, and how modern ordering
  systems really handle tips and tax.

---

## For balance: what the AI did

- **Claude Code (Claude Opus 5.5) and its subagents** wrote the code, tests, migrations, Docker setup and
  documentation. They did the debugging, and found and fixed most of their own bugs through independent
  review passes and mutation checks.
- **Some of Kyle's early choices added cost before he reversed them:**
  - the four-role subagent workflow,
  - the full kiosk scope,
  - running a second session in parallel, which caused the decision-numbering clash.

  Each was recognised and corrected, and the corrections are recorded as decisions.

**The pattern a reader should take from this:** Kyle set the direction and the standards, noticed when
the process was failing, re-planned with explicit trade-offs, checked the AI's work independently, and
kept the record honest about who did what.
