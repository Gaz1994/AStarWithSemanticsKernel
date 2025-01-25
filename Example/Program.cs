using System.Numerics;

namespace Example;

public abstract class Program
{
    public static async Task Main()
    {
        // Initialize the game
        var game = new Game();
        game.Initialize();

        // Create a test map first (10x10)
        game._world.CreateRectangularMap(10, 10);

        // Add some obstacles for testing
        game._world.SetTileProperty(new Vector2(2, 2), "walkable", false);
        game._world.SetTileProperty(new Vector2(2, 3), "walkable", false);
        game._world.SetTileProperty(new Vector2(2, 4), "walkable", false);

        // Add some terrain variation
        game._world.SetTileProperty(new Vector2(5, 5), "terrain", "water");
        game._world.SetTileProperty(new Vector2(6, 6), "terrain", "mountain");

        // Test pathfinding
        await game.MoveCharacter(
            new Vector2(0, 0),  // Start at corner
            new Vector2(9, 9)   // Try to reach opposite corner
        );
    }
}