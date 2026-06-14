/*
Script Audit:
- Purpose: Converts NodeType values into short labels and text icons for TheGrid.
- Attached GameObject: None; this is a static UI formatting helper.
- Main responsibilities: Return display labels, uppercase labels, and fallback text icons for node types.
- Important variables: ToGridLabel, ToGridLabelUpper, ToGridIcon.
- Inputs: NodeType enum values.
- Outputs or effects: Produces text shown by GridNodeButton and GridMapController.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Check every NodeType displays a readable label and icon fallback.
*/
public static class GridNodeTypeDisplay
{
    public static string ToGridLabel(this NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Start:
                return "Start";
            case NodeType.Combat:
                return "Combat";
            case NodeType.Hacker:
                return "Hacker";
            case NodeType.Elite:
                return "Elite";
            case NodeType.Rest:
                return "Legacy";
            case NodeType.Shop:
                return "Shop";
            case NodeType.Reboot:
                return "Reboot";
            case NodeType.Boss:
                return "Boss";
            default:
                return nodeType.ToString();
        }
    }

    public static string ToGridLabelUpper(this NodeType nodeType)
    {
        return nodeType.ToGridLabel().ToUpperInvariant();
    }

    public static string ToGridIcon(this NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Start:
                return ">_";
            case NodeType.Combat:
                return "/";
            case NodeType.Hacker:
                return "H";
            case NodeType.Elite:
                return "/";
            case NodeType.Rest:
                return "?";
            case NodeType.Shop:
                return "#";
            case NodeType.Reboot:
                return "R";
            case NodeType.Boss:
                return "X";
            default:
                return "?";
        }
    }
}
