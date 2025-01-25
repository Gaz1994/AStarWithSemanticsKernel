# AStarWithSemanticsKernel

Crazy & fun demo Pathfinding project using Semantics Kernel under the hood, refer to the ```Example``` folder to see use case. 

I wouldn't recommend using this outside of experimenting as it might be very cost heavy when it comes to using AI, however it also works without it. 


Example Code:


Setup: 

```cs
       // setup semantics kernel
        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "",
                apiKey: ""
            );


        var kernel = builder.Build();

        // Initialize game world (this will be your environment where you want the pathfinder to be operating)
        _world = new GameWorld();
        
        // Create pathfinder
        _pathfinder = new UniversalPathfinder<GameTile>(kernel, _world);
        
        // Initialize the pathfinder
        await _pathfinder.InitializeAsync();


```

Movement: 

```cs 
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

```


TODO List: 
* Look into using ```InMemoryVectorStore```.
* Update pathfinding results on the fly when dynamic map changes happen (like item spawns).
* Use AI for updating pathfinding resutls on the fly instead of on init (My design decision came from wanting to simulate a realistic scenario where this library could be used efficiently in terms of cost and performance). 
