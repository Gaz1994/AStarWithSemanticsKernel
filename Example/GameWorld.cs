using System.Collections;
using System.Numerics;
using AStarWithSemanticsKernel;

namespace Example;

public class GameWorld : IPathEnvironment<GameTile>
{
    private readonly Dictionary<Vector2, GameTile> _tiles = new();

    private void AddTile(GameTile tile)
    {
        _tiles[tile.Position] = tile;
    }

    public void CreateRectangularMap(int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var position = new Vector2(x, y);
                var tile = new GameTile("ground")
                {
                    Position = position,
                    Height = 0,
                    IsWalkable = true
                };
                AddTile(tile);
            }
        }
    }

    public GameTile GetTile(Vector2 position)
    {
        return _tiles.GetValueOrDefault(position);
    }

    public void SetTileProperty(Vector2 position, string key, object value)
    {
        if (_tiles.TryGetValue(position, out var tile))
        {
            tile.SetProperty(key, value);
        }
    }

    // Add these two required interface implementations:
    public bool CanMove(GameTile from, GameTile to)
    {
        // Basic movement rules: tile must be walkable and height difference should be manageable
        return to.IsWalkable && Math.Abs(to.Height - from.Height) <= 1;
    }
    
    // Implement the new method

    // Fix the return type to specifically be GameTile
    public IEnumerable<GameTile> GetAllTiles()
    {
        return _tiles.Values;
    }

    public IEnumerable<GameTile> GetNeighbors(GameTile tile)
    {
        // Define adjacent positions (4-directional movement)
        var positions = new[]
        {
            new Vector2(tile.Position.X + 1, tile.Position.Y),
            new Vector2(tile.Position.X - 1, tile.Position.Y),
            new Vector2(tile.Position.X, tile.Position.Y + 1),
            new Vector2(tile.Position.X, tile.Position.Y - 1)
        };
        

        // Return all valid neighbors
        return positions
            .Select(GetTile)
            .Where(t => t != null);
    }
}