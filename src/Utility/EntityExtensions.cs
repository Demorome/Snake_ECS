
using MoonTools.ECS;

/// <summary>
/// ECS Entity extension methods.
/// </summary>
public static class EntityExt
{
    public static string EntityToString(World world, Entity e)
    {
        var tag = world.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
    }

    public static string EntityComponentsToString(World world, Entity e)
    {
        string result = new("");
        foreach (var type in world.Debug_GetAllComponentTypes(e))
        {
            result += "\n*\t" + type.Name;
        }
        return result;
    }
}