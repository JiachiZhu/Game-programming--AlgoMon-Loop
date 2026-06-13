# 2026-06-13 - External first-time playtest plan

- Build tested: pending Windows player build
- Tester profile: someone who has not worked on AlgoMon and has not studied this
  game programming module
- Goal: find what is unclear to a first-time player

## Setup

Give the tester the packaged build folder or zip. Let them launch `AlgoMon.exe`
themselves after a short setup note:

> This is a short tactical roguelite prototype. Try to start a run, choose route
> nodes, win a battle, and tell me what you think the UI is communicating.

Avoid explaining the full combat rules before play. The point is to see what
the game communicates by itself.

## Observation Checklist

| Moment | What to watch | Notes |
|---|---|---|
| MainTerminal first load | Do they know how to start? | |
| Depth selection | Do they understand 1F-5F as difficulty/depth? | |
| TheGrid | Do they know which nodes can be selected? | |
| Battle start | Can they identify their AlgoMon, enemy, HP/Battery, and CP? | |
| Skill choice | Do they hover/read details or click blindly? | |
| Counter/status/subroutine feedback | Can they explain what changed? | |
| Victory/defeat | Do they understand what they earned or why they lost? | |

## Questions After Play

1. What did you think the main goal of the game was?
2. Which screen was clearest?
3. Which screen was most confusing?
4. What did you think Battery and CP meant?
5. Did the battle feedback explain why damage, counters, or passive effects
   happened?
6. What would you change first if you had one small fix?

## Feedback Record

| Observation / quote | Where it happened | Action |
|---|---|---|
|  |  | Fixed now / explain in report / future work |

## Follow-ups

- Keep the notes short and specific. One useful misunderstanding is better than
  a long vague compliment.
- If the feedback changes the build, link the fix here and in the final report.
