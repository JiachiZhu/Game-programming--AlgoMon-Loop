using System.Collections.Generic;

public class GridValidationResult
{
    public readonly List<string> errors = new List<string>();

    public bool IsValid => errors.Count == 0;

    public void AddError(string error)
    {
        if (!string.IsNullOrEmpty(error))
            errors.Add(error);
    }
}
