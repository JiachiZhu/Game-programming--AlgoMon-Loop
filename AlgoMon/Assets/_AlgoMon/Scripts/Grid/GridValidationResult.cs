using System.Collections.Generic;

// Defense note: GridValidationResult is the main grid validation result type used by this part of the project.
public class GridValidationResult
{
    public readonly List<string> errors = new List<string>();

    public bool IsValid => errors.Count == 0;

    // Defense note: Adds the error entry into the target collection or UI.
    public void AddError(string error)
    {
        if (!string.IsNullOrEmpty(error))
            errors.Add(error);
    }
}
