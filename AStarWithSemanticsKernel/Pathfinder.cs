using System.Collections.Concurrent;
using System.Numerics;
using AStarWithSemanticsKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;

public class UniversalPathfinder<T>(Kernel kernel, IPathEnvironment<T> environment)
    where T : IPathTile
{
    private readonly InMemoryVectorStore _vectorStore = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, float>> _movementCosts = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _validMoves = new();
    private readonly ConcurrentDictionary<string, List<Vector2>> _pathCache = new();
    private bool _isInitialized = false;
    
    // Add initialization method
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await PrecomputeTerrainCosts();
        _isInitialized = true;
    }
    
    private async Task PrecomputeTerrainCosts()
    {
        var processedTerrains = new HashSet<string>();
        var terrainAnalysisTasks = new List<Task>();

        // Batch process terrain types
        foreach (var tile in environment.GetAllTiles())
        {
            var terrainType = tile.GetProperty("terrain")?.ToString() ?? "ground";
            if (processedTerrains.Add(terrainType))
            {
                terrainAnalysisTasks.Add(AnalyzeTerrainType(terrainType));
            }
        }

        await Task.WhenAll(terrainAnalysisTasks);
    }
    
    private async Task AnalyzeTerrainType(string terrainType)
    {
        try
        {
            var function = kernel.CreateFunctionFromPrompt("""
                                                           Analyze terrain type for pathfinding:
                                                           {{$terrain}}
                                                           Return a JSON object with base movement cost and valid movement types.
                                                           Example: { "baseCost": 1.0, "validMovements": ["walk", "run"] }
                                                           """);

            var result = await function.InvokeAsync(kernel, new KernelArguments { { "terrain", terrainType } });
            var resultString = result.GetValue<string>();

            // Cache the analysis results
            var terrainKey = $"terrain_{terrainType}";
            _movementCosts.TryAdd(terrainKey, new Dictionary<string, float>());
        }
        catch
        {
            // Fallback to default values if AI analysis fails
            var terrainKey = $"terrain_{terrainType}";
            _movementCosts.TryAdd(terrainKey, new Dictionary<string, float> { { "default", 1.0f } });
        }
    }
    

    public async Task<List<T>> FindPathAsync(PathRequest<T> request)
    {
        // Try cached path first
        var pathKey = GetPathKey(request);
        
        if (!_pathCache.TryGetValue(pathKey, out var cachedPath)) 
            return await CalculatePath(request);
        
        var validPath = ValidateCachedPath(cachedPath, request);
        if (validPath != null) 
            return validPath;
        
        
        _pathCache.TryRemove(pathKey, out _);

        return await CalculatePath(request);
    }

    private async Task<List<T>> CalculatePath(PathRequest<T> request)
    {
        var openSet = new PriorityQueue<(T tile, float g), float>();
        var closedSet = new HashSet<Vector2>();
        var gScores = new Dictionary<Vector2, float>();
        var cameFrom = new Dictionary<Vector2, T>();

        openSet.Enqueue((request.StartTile, 0), 0);
        gScores[request.StartTile.Position] = 0;

        while (openSet.Count > 0)
        {
            var (current, gScore) = openSet.Dequeue();

            if (current.Position == request.EndTile.Position)
            {
                var path = ReconstructPath(current, cameFrom);
                LearnFromPath(path, request.MovementRules);
                return path;
            }

            closedSet.Add(current.Position);

            foreach (var neighbor in GetViableNeighbors(current, request))
            {
                if (closedSet.Contains(neighbor.Position))
                    continue;

                var tentativeG = gScore + await GetMovementCost(current, neighbor, request);

                if (gScores.TryGetValue(neighbor.Position, out var value) && !(tentativeG < value)) 
                    continue;
                
                cameFrom[neighbor.Position] = current;
                value = tentativeG;
                gScores[neighbor.Position] = value;
                var fScore = tentativeG + CalculateHeuristic(neighbor, request.EndTile, request);
                openSet.Enqueue((neighbor, tentativeG), fScore);
            }
        }

        return [];
    }

    private IEnumerable<T> GetViableNeighbors(T current, PathRequest<T> request)
    {
        var neighbors = environment.GetNeighbors(current);
        var patternKey = GetPatternKey(current);

        if (_validMoves.TryGetValue(patternKey, out var validMoves))
        {
            return neighbors.Where(n => 
                IsValidMove(current, n, validMoves) && 
                environment.CanMove(current, n));
        }

        return neighbors.Where(n => environment.CanMove(current, n));
    }

    private async Task<float> GetMovementCost(T from, T to, PathRequest<T> request)
    {
        var moveKey = GetMoveKey(from, to);
        var ruleSet = request.MovementRules.GetValueOrDefault("movementType", "default").ToString();

        if (_movementCosts.TryGetValue(moveKey, out var costs) && 
            costs.TryGetValue(ruleSet, out var cost))
        {
            return cost;
        }

        // If we don't have a cached cost, analyze the movement
        return await AnalyzeMovement(from, to, request);
    }

    private async Task<float> AnalyzeMovement(T from, T to, PathRequest<T> request)
    {
        var fromTerrain = from.GetProperty("terrain")?.ToString() ?? "ground";
        var toTerrain = to.GetProperty("terrain")?.ToString() ?? "ground";
    
        // First check if we have this in our precomputed costs
        var fromKey = $"terrain_{fromTerrain}";
        var toKey = $"terrain_{toTerrain}";
    
        if (_movementCosts.TryGetValue(fromKey, out var fromCosts) && 
            _movementCosts.TryGetValue(toKey, out var toCosts))
        {
            // Average the costs from both terrains
            var fromCost = fromCosts.GetValueOrDefault("default", 1.0f);
            var toCost = toCosts.GetValueOrDefault("default", 1.0f);
            var heightFactor = Math.Abs(to.Height - from.Height) * 0.5f;
        
            return (fromCost + toCost) / 2 + heightFactor;
        }

        // If we somehow don't have the precomputed costs, fall back to basic calculation
        return CalculateBasicCost(from, to);
    }

    private static string CreateMovementDescription(T from, T to, PathRequest<T> request)
    {
        var desc = new System.Text.StringBuilder();
        desc.AppendLine($"From Position: ({from.Position.X}, {from.Position.Y}, Height: {from.Height})");
        desc.AppendLine($"To Position: ({to.Position.X}, {to.Position.Y}, Height: {to.Height})");
        
        // Add all properties from both tiles
        foreach (var key in from.GetPropertyKeys())
        {
            desc.AppendLine($"From {key}: {from.GetProperty(key)}");
        }
        foreach (var key in to.GetPropertyKeys())
        {
            desc.AppendLine($"To {key}: {to.GetProperty(key)}");
        }

        // Add movement rules
        foreach (var rule in request.MovementRules)
        {
            desc.AppendLine($"Rule {rule.Key}: {rule.Value}");
        }

        return desc.ToString();
    }

    private static float CalculateBasicCost(T from, T to)
    {
        var distance = Vector2.Distance(from.Position, to.Position);
        var heightDiff = Math.Abs(to.Height - from.Height);
        return distance * (1 + heightDiff * 0.5f);
    }

    private float CalculateHeuristic(T current, T end, PathRequest<T> request)
    {
        var distance = Vector2.Distance(current.Position, end.Position);
        var heightDiff = Math.Abs(end.Height - current.Height);
        
        var moveKey = GetMoveKey(current, end);
        var ruleSet = request.MovementRules.GetValueOrDefault("movementType", "default").ToString();

        if (_movementCosts.TryGetValue(moveKey, out var costs) && 
            costs.TryGetValue(ruleSet, out var cost))
        {
            return distance * cost;
        }

        return distance * (1 + heightDiff * 0.5f);
    }

    private static List<T> ReconstructPath(T current, Dictionary<Vector2, T> cameFrom)
    {
        var path = new List<T> { current };
        while (cameFrom.ContainsKey(current.Position))
        {
            current = cameFrom[current.Position];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private void LearnFromPath(List<T> path, Dictionary<string, object> rules)
    {
        if (path.Count < 2) 
            return;

        // Cache successful path
        var pathKey = GetPathKey(new PathRequest<T> 
        { 
            StartTile = path[0], 
            EndTile = path[^1], 
            MovementRules = rules 
        });
        
        _pathCache.TryAdd(pathKey, path.Select(t => t.Position).ToList());

        // Learn movement patterns
        for (var i = 0; i < path.Count - 1; i++)
        {
            var from = path[i];
            var to = path[i + 1];
            var patternKey = GetPatternKey(from);

            _validMoves.AddOrUpdate(
                patternKey,
                [GetMovePattern(from, to)],
                (_, patterns) =>
                {
                    patterns.Add(GetMovePattern(from, to));
                    return patterns;
                });
        }
    }

    private List<T> ValidateCachedPath(List<Vector2> cachedPositions, PathRequest<T> request)
    {
        var validPath = new List<T>();
        var previous = default(T);

        foreach (var current in cachedPositions.Select(environment.GetTile))
        {
            if (current == null || !current.IsWalkable) 
                return null;

            if (previous != null && !environment.CanMove(previous, current))
                return null;

            validPath.Add(current);
            previous = current;
        }

        return validPath;
    }

    private static string GetPathKey(PathRequest<T> request) =>
        $"{request.StartTile.Position}-{request.EndTile.Position}-{string.Join(",", request.MovementRules)}";

    private static string GetPatternKey(T tile) =>
        $"{tile.Position}-{string.Join(",", tile.GetPropertyKeys().Select(k => $"{k}={tile.GetProperty(k)}"))}";

    private static string GetMoveKey(T from, T to) =>
        $"{GetPatternKey(from)}-{GetPatternKey(to)}";

    private static string GetMovePattern(T from, T to) =>
        $"{to.Position.X - from.Position.X},{to.Position.Y - from.Position.Y}";

    private static bool IsValidMove(T from, T to, HashSet<string> validMoves) =>
        validMoves.Contains(GetMovePattern(from, to));
}