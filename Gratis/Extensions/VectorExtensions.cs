using System.Numerics;

namespace Gratis.Extensions;

public static class VectorExtensions
{
    public static Vector2 AsMapVector(this Vector3 vector) => new(vector.X, vector.Z);
}