#if DEBUG

using MoonTools.ECS;

namespace RollAndCash.Editor;

public static class DynamicComponentManip
{
    // For some reason, can't directly pass a `dynamic` value to an "in" param, so we use this.
    public static void Set<T>(World world, Entity entity, T component) where T : unmanaged
    {
        world.Set(entity, component);
    }

    // Need to pass dummy typed component to extract the T type from the `dynamic` value.
    // FIXME: Might be more efficient to use "MakeGenericMethod" instead, though idk if it would leak.
    public static void Remove<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        world.Remove<T>(entity);
    }
    public static T Get<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        return world.Get<T>(entity);
    }
    public static bool Has<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        return world.Has<T>(entity);
    }
}

#endif