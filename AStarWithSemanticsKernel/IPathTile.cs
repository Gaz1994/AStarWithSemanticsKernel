using System.Numerics;

namespace AStarWithSemanticsKernel;

public interface IPathTile
{
    Vector2 Position { get; }
    float Height { get; }  // Can be 0 for 2D games
    bool IsWalkable { get; }
    
    object GetProperty(string key);
    IEnumerable<string> GetPropertyKeys();
}