/*
Script Audit:
- Purpose: Lists the visual states a Grid node button can show.
- Attached GameObject: None; this enum is used by GridMapController and GridNodeButton.
- Main responsibilities: Distinguish inactive, current, next, target, and visited route nodes.
- Important variables: Inactive, Current, NextAvailable, Target, Visited.
- Inputs: GameManager route state and node availability checks.
- Outputs or effects: Controls colors, labels, and interactability for grid node UI.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Move through a run and confirm nodes change between current, next, target, visited, and inactive.
*/
// Defense note: GridNodeVisualState defines the valid grid node visual state options used by the gameplay systems.
public enum GridNodeVisualState
{
    Inactive,
    Unknown,
    Current,
    NextAvailable,
    Target,
    Visited
}
