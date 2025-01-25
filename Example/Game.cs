using System.Numerics;
using AStarWithSemanticsKernel;
using Microsoft.SemanticKernel;

namespace Example;

public class Game
{
    private UniversalPathfinder<GameTile> _pathfinder;
    public GameWorld _world;

    public async void Initialize()
    {
        // Setup the Semantic Kernel
        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "", // or gpt-3.5-turbo if you prefer
                apiKey: ""
            );
        var kernel = builder.Build();

        // Initialize game world
        _world = new GameWorld();
        
        // Create pathfinder
        _pathfinder = new UniversalPathfinder<GameTile>(kernel, _world);
        
        // Initialize the pathfinder with precomputed costs
        await _pathfinder.InitializeAsync();
    }

    private async Task<List<GameTile>> FindPath(GameTile start, GameTile end)
    {
        var request = new PathRequest<GameTile>
        {
            StartTile = start,
            EndTile = end,
            MovementRules = new Dictionary<string, object>
            {
                { "movementType", "walk" },
                { "maxJumpHeight", 1.0f },
                { "allowDiagonal", false }
            }
        };

        return await _pathfinder.FindPathAsync(request);
    }

    // Example of using the pathfinder
    public async Task MoveCharacter(Vector2 startPos, Vector2 endPos)
    {
        var startTile = _world.GetTile(startPos);
        var endTile = _world.GetTile(endPos);

        if (startTile == null || endTile == null)
            return;

        var path = await FindPath(startTile, endTile);
        
        if (path.Count > 0)
        {
            foreach (var tile in path)
            {
                Console.WriteLine($"Move to: {tile.Position}");
                // Move your character using the path
            }
        }
        else
        {
            Console.WriteLine("No path found!");
        }
    }
}