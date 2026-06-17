# 2026-06-17 - External first-time playtest feedback

- Issue: #52
- Build tested: packaged Windows player build on an external laptop; exact build
  commit was not captured in the chat evidence.
- Feedback collected: 2026-06-16 to 2026-06-17
- Testers: two first-time lay players outside the project, recorded here as
  Tester A and Tester B.
- Evidence source: private Chinese chat screenshots supplied by the project
  author. The screenshots are not committed because they contain personal chat
  context.

## Test Goal

Check whether first-time players with no expected CS background could understand
the route, combat terms, rewards, and overall purpose of AlgoMon without a full
rules explanation from the developer.

## Tester A

Profile: first-time external player / layperson perspective.

### Observations

| Observation | Translated direct comment | Action |
|---|---|---|
| The player wanted more story motivation before or during the run. | "Adding more story would give the player more motivation to move forward." | Fixed now for short-term clarity: System Log now explains the cyber-hacker premise, visible bugs, algorithm sprites, bug data, and saving the network. Larger story scenes are future work. |
| The player wanted a way to leave a route midway instead of being locked into the run. | "I hope the dungeon has a midway exit option, for example if I do not want to keep fighting halfway through." | Fixed now: TheGrid has a Flee/exit control using the battle-style flee button treatment. |
| Credit and progression rewards were unclear after a route ended. | "I do not know what the credit after the level can be used for." | Fixed now: Credit is permanent, shop/use cases are documented, and System Log explains credit and route rewards. |
| Reward values and node difficulty wording were too vague. | "What is the difference between higher and above?" | Fixed now: reward/credit information is expressed with concrete ranges where possible and node text was shortened to avoid panel overflow. |
| Combat terminology and status effects needed explanation. | "The element counter relationship is not that clear." / "Some statuses lack explanation, for example what is the use of computing +10 stacks? What is computing power?" | Fixed now: System Log explains ASD priority, element matchups, statuses, six stats, and node types. |
| Some UI text overflowed or appeared cut off. | "Some places cannot show all of the information; after 'and' it looks like there is more." | Fixed now: right-panel text was shortened and System Log diagrams were adjusted to keep content inside the frame. |

### Direct Comments Captured

The following are direct translations from the Chinese feedback screenshots:

- "Adding more story would give the player more motivation to move forward."
- "I hope the dungeon has a midway exit option."
- "I do not know what the credit after the level can be used for."
- "The element counter relationship is not that clear."
- "Some statuses lack explanation."
- "Some places cannot show all of the information."

## Tester B

Profile: first-time external player / layperson perspective.

### Observations

| Observation | Translated direct comment | Action |
|---|---|---|
| The player immediately noticed there was no way to exit the grid route. | "I cannot get out." | Fixed now: TheGrid now exposes a Flee/exit button during the node map. |
| The player expected stronger collection/progression fantasy, such as capturing or gaining something from play. | "Can you not capture it?" / "It would be better if playing the game gave some kind of gain." | Partly fixed now: System Log frames defeated bugs as bug data / AlgoMon records. Deeper capture presentation and long-term collection rewards are future work. |
| The player asked whether the game had a large special move or stronger late-game payoff. | "Does this game not have a big move?" | Future work: a special-move style payoff could improve player excitement, but it is outside the one-day clarity fixes. |
| The player suggested an educational angle using English/Chinese vocabulary. | "For example, it could teach children vocabulary." / "Or use English words to learn Chinese." | Explain in report / future work: useful product-direction feedback, but outside the current combat-roguelite scope. |
| Difficulty 5 felt hard, especially when combined with no mid-run exit. | "Difficulty 5 is a bit hard, and the game still cannot be exited midway." | Fixed now for exit friction: route Flee was added. Difficulty tuning remains future balancing work. |
| Failure consequences were unclear. | "If the game fails, is there anything?" | Partly fixed now: permanent credit and reward explanations reduce uncertainty. Future work: make defeat rewards/loss state clearer in the result screen. |

### Direct Comments Captured

The following are direct translations from the Chinese feedback screenshots:

- "I cannot get out."
- "Does this game not have a big move?"
- "Can you not capture it?"
- "It would be better if playing the game gave some kind of gain."
- "Difficulty 5 is a bit hard, and the game still cannot be exited midway."
- "If the game fails, is there anything?"

## Action List

| Feedback theme | Resolution |
|---|---|
| Mid-route exit / cannot leave Grid | Fixed now: route Flee button added. |
| Credit purpose unclear | Fixed now: Credit is permanent and explained in System Log. |
| Statuses, stats, ASD, elements, nodes unclear | Fixed now: System Log / Field Manual added with concise pages and diagrams. |
| Story motivation unclear | Fixed now for minimum viable clarity: cyber-hacker bug repair premise added to System Log. Larger narrative onboarding remains future work. |
| Reward / node text vague or overflowing | Fixed now: reward values use concrete ranges where practical, and panel text was shortened to avoid overflow. |
| Difficulty 5 feels hard | Future work: balance pass after more playtest data. |
| Capture / big move / stronger reward fantasy | Future work: useful direction for collection feel and battle excitement, but not needed for the one-day clarity patch. |
| Educational vocabulary suggestion | Explain in report / future work: interesting audience suggestion, outside current scope. |

## Issue #52 Coverage

- Two external first-time lay players were tested.
- At least three observations were recorded for each tester.
- At least three direct comments were recorded for each tester.
- Feedback was converted into fixed-now, report/future, and future-work actions.
- The implemented one-day fixes focused on clarity, exit safety, reward
  permanence, and information overflow rather than expanding game scope.
