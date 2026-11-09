#if DEBUG
namespace ShadeFlow.Development;

public sealed class DevelopmentViewState
{
    public string Page { get; set; } = "Displays";
    public double Width { get; set; } = 1000;
    public double Height { get; set; } = 650;
    public double Left { get; set; }
    public double Top { get; set; }
}
#endif
