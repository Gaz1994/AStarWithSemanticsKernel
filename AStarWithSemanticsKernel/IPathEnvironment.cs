using System.Numerics;

namespace AStarWithSemanticsKernel;


// In your library:
public interface IPathEnvironment<T> where T : IPathTile
{
    T GetTile(Vector2 position);
    IEnumerable<T> GetNeighbors(T tile);
    bool CanMove(T from, T to);
    IEnumerable<T> GetAllTiles();
}
