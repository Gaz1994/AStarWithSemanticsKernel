namespace AStarWithSemanticsKernel;

public class PathRequest<T> where T : IPathTile
{
    public T StartTile { get; set; }
    public T EndTile { get; set; }
    public Dictionary<string, object> MovementRules { get; set; } = new();
}