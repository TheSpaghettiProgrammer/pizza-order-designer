# #<N> — <Story title>

| | |
|---|---|
| **Story** | https://github.com/TheSpaghettiProgrammer/pizza-order-designer/issues/<N> |
| **Branch** | `story/<N>-<slug>` |
| **Status** | Planning · Plan review · Awaiting user approval · In development · Code review · PR open · Done |
| **Depends on** | #… |
| **Decisions** | D-… |

> Workflow and rules: [docs/SUBAGENTS.md](../SUBAGENTS.md). Each role writes only its own section.
> Later rounds are appended as `### Round 2`, never overwritten.

---

## 1. Plan  *(pizza-planner)*

### 1.1 Goal and scope
- What this story delivers, in 2–3 sentences.
- **In scope:** acceptance criteria covered.
- **Out of scope / deferred:** each one with the reason.

### 1.2 Approach
- Design summary: the types, endpoints and components, and how they fit the existing code.
- Why this approach (alternatives considered, in one line each).

### 1.3 Changes, file by file
| File | New / Modify | What changes |
|---|---|---|
| `server/src/...` | New | ... |

### 1.4 Steps, in order (each step leaves the build green and becomes roughly one commit)
1. ...

### 1.5 Tests
| Test | Level (unit/integration/component) | Invariant or AC it proves |
|---|---|---|

### 1.6 Acceptance criteria mapping
| AC (from the issue) | Step(s) | Test(s) |
|---|---|---|

### 1.7 Risks, assumptions, open questions
- Assumptions (proposed `D-xxx` entries / README assumptions).
- Questions that need the user (these block approval).

---

## 2. Plan review  *(pizza-plan-reviewer)*

### Round 1
**Verdict:** APPROVE | REVISE

| # | Severity | Finding | Suggested change |
|---|---|---|---|

---

## 3. Implementation notes  *(pizza-developer)*

### Round 1
- **Commits:** `<sha> <subject>`, …
- **Deviations from the plan, and why:** …
- **Checks run:** command → result
- **Follow-ups / proposed decisions:** …

---

## 4. Code review  *(pizza-reviewer)*

### Round 1
**Verdict:** PASS | CHANGES REQUESTED

**Traceability**
| AC | Implemented in | Proven by | Status |
|---|---|---|---|

**Plan conformance:** …

**Checks re-run:** command → result

| # | Severity | File:line | Finding and failure scenario | Suggested fix |
|---|---|---|---|---|

---

## 5. Outcome  *(orchestrator)*
- PR: …
- Decisions added: D-…
- Merged: <date>
