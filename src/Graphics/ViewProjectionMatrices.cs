using System.Numerics;

namespace RollAndCash.Rendering;

public readonly record struct ViewProjectionMatrices(Matrix4x4 View, Matrix4x4 Projection);