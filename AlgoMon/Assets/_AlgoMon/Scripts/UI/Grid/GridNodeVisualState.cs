/*
Script Audit:
- Purpose: Lists the visual states a Grid node button can show.
- Attached GameObject: None; this enum is used by GridMapController and GridNodeButton.
- Main responsibilities: Distinguish locked, current, available, and visited route nodes.
- Important variables: Locked, Current, Available, Visited.
- Inputs: GameManager route state and node availability checks.
- Outputs or effects: Controls colors, labels, and interactability for grid node UI.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Move through a run and confirm nodes change between current, visited, available, and locked.
*/
public enum GridNodeVisualState
{
    Locked,
    Current,
    Available,
    Visited
}
