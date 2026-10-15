namespace Dowlume.App.ViewModels;

public sealed record HistoryFilter<T>(string Label, T? Value) where T : struct
{
    public override string ToString() => Label;
}
