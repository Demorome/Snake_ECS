using System;
using MoonWorks;

namespace RollAndCash.Utility;

/// <summary>
/// Like Optional or Nullable, 
/// but we can specify the HasValue part ourselves,
/// to store a value and indicate if it should be used or not.
/// <para>Simply construct a new instance with the old value,
/// and set HasValue to the toggled state.</para>
/// </summary>
public readonly record struct Toggleable<T> where T : unmanaged
{
    /// <summary>
    /// ONLY use this if you checked <see cref="HasValue"/> beforehand! 
    /// </summary>
    public readonly T Value_Unsafe;
    public readonly T Value 
    {   
        get
        {
            if (!HasValue)
            {
                Logger.LogError("Value shouldn't be accessed!");
                return default;
            }
            return Value_Unsafe;
        }
    }
    public readonly bool HasValue;

    public Toggleable(T value, bool hasValue = true) : this()
    {
        Value_Unsafe = value;
        HasValue = hasValue;
    }

    public static implicit operator T?(Toggleable<T> t)
    {
        if (t.HasValue)
        {
            return new T?(t.Value_Unsafe);
        }
        else
        {
            return default;
        }
    }

    public static implicit operator Toggleable<T>(T? t)
    {
        if (t.HasValue)
        {
            return new Toggleable<T>(t.Value);
        }
        else
        {
            return default;
        }
    }
}