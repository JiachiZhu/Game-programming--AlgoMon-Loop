/*
Script Audit:
- Purpose: Lists the visual states a Grid node button can show.
- Attached GameObject: None; this enum is used by GridMapController and GridNodeButton.
- Main responsibilities: Distinguish inactive, current, next, target, and visited route nodes.
- Important variables: Inactive, Current, NextAvailable, Target, Visited.
- Inputs: GameManager route state and node availability checks.
- Outputs or effects: Controls colors, labels, and interactability for grid node UI.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Move through a run and confirm nodes change between current, next, target, visited, and inactive.
*/
public enum GridNodeVisualState
{
    Inactive,
    Unknown,
    Current,
    NextAvailable,
    Target,
    Visited
}
