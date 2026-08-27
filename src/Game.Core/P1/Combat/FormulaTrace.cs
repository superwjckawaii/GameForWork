namespace GameForWork.Core.P1.Combat;

public sealed record FormulaStep(string Label, string Expression, int Result);

public sealed record CalculatedValue(int Value, IReadOnlyList<FormulaStep> Steps)
{
    public static CalculatedValue Single(string label, string expression, int value) =>
        new(value, [new FormulaStep(label, expression, value)]);
}

public sealed class FormulaTraceBuilder
{
    private readonly List<FormulaStep> _steps = [];

    public void Add(string label, string expression, int result) =>
        _steps.Add(new FormulaStep(label, expression, result));

    public CalculatedValue Build(int value) => new(value, _steps.ToArray());
}
