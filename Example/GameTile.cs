using System.Numerics;
using AStarWithSemanticsKernel;

namespace Example;

public class GameTile : IPathTile
{
    public Vector2 Position { get; set; }
    public float Height { get; set; }
    public bool IsWalkable { get; set; }
    private string TerrainType { get; set; }
    
    private readonly Dictionary<string, object> _properties = new();
    
    public GameTile(string terrainType = "ground")
    {
        TerrainType = terrainType;
        _properties["terrain"] = TerrainType;
        _properties["walkable"] = IsWalkable;
    }

    public void SetProperty(string key, object value)
    {
        _properties[key] = value;
        
        // Special handling for walkable property
        if (key == "walkable" && value is bool walkable)
        {
            IsWalkable = walkable;
        }
    }

    public object GetProperty(string key) => _properties.GetValueOrDefault(key);
    public IEnumerable<string> GetPropertyKeys() => _properties.Keys;
}