namespace diffcalc_comparer;

public class DiffComparer
{
    private DiffComparerOptions options;

    public DiffComparer(DiffComparerOptions options)
    {
        this.options = options;
    }

    public void Compare()
    {
    }
}

public struct DiffComparerOptions
{
    public string? ExportPath;
    public bool? IncludeUrl;
}
