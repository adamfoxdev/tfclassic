using System.Numerics;
using Raylib_cs;
using TFClassic.Core;

namespace TFClassic.Desktop;

/// <summary>Immediate-mode helpers: face-shaded boxes so the unlit world still reads as 3D.</summary>
static class Draw3D
{
    const float Top = 1.0f, Bottom = 0.45f, PosX = 0.80f, NegX = 0.62f, PosZ = 0.90f, NegZ = 0.70f;

    public static void Box(Vector3 min, Vector3 max, Color c, bool outline = false)
    {
        Rlgl.CheckRenderBatchLimit(24);
        Rlgl.Begin(DrawMode.Quads);
        Face(c, Top, min.X, max.Y, min.Z, min.X, max.Y, max.Z, max.X, max.Y, max.Z, max.X, max.Y, min.Z);
        Face(c, Bottom, min.X, min.Y, min.Z, max.X, min.Y, min.Z, max.X, min.Y, max.Z, min.X, min.Y, max.Z);
        Face(c, PosX, max.X, min.Y, min.Z, max.X, max.Y, min.Z, max.X, max.Y, max.Z, max.X, min.Y, max.Z);
        Face(c, NegX, min.X, min.Y, min.Z, min.X, min.Y, max.Z, min.X, max.Y, max.Z, min.X, max.Y, min.Z);
        Face(c, PosZ, min.X, min.Y, max.Z, max.X, min.Y, max.Z, max.X, max.Y, max.Z, min.X, max.Y, max.Z);
        Face(c, NegZ, min.X, min.Y, min.Z, min.X, max.Y, min.Z, max.X, max.Y, min.Z, max.X, min.Y, min.Z);
        Rlgl.End();

        if (outline)
            Raylib.DrawCubeWiresV((min + max) * 0.5f, max - min, Palette.Shade(c, 0.55f));
    }

    /// <summary>
    /// A box drawn with counter-clockwise faces so back-face culling alone hides the far side. Used by the first-person
    /// view model, which is drawn without a depth buffer (so the gun can never be hidden by a wall).
    /// </summary>
    public static void SolidBox(Vector3 min, Vector3 max, Color c)
    {
        Rlgl.CheckRenderBatchLimit(24);
        Rlgl.Begin(DrawMode.Quads);
        Color4(c, PosZ); Quad(min.X, min.Y, max.Z, max.X, min.Y, max.Z, max.X, max.Y, max.Z, min.X, max.Y, max.Z);
        Color4(c, NegZ); Quad(max.X, min.Y, min.Z, min.X, min.Y, min.Z, min.X, max.Y, min.Z, max.X, max.Y, min.Z);
        Color4(c, PosX); Quad(max.X, min.Y, max.Z, max.X, min.Y, min.Z, max.X, max.Y, min.Z, max.X, max.Y, max.Z);
        Color4(c, NegX); Quad(min.X, min.Y, min.Z, min.X, min.Y, max.Z, min.X, max.Y, max.Z, min.X, max.Y, min.Z);
        Color4(c, Top); Quad(min.X, max.Y, max.Z, max.X, max.Y, max.Z, max.X, max.Y, min.Z, min.X, max.Y, min.Z);
        Color4(c, Bottom); Quad(min.X, min.Y, min.Z, max.X, min.Y, min.Z, max.X, min.Y, max.Z, min.X, min.Y, max.Z);
        Rlgl.End();
    }

    static void Color4(Color c, float shade) => Rlgl.Color4ub((byte)(c.R * shade), (byte)(c.G * shade), (byte)(c.B * shade), c.A);

    static void Quad(float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3)
    {
        Rlgl.Vertex3f(x0, y0, z0);
        Rlgl.Vertex3f(x1, y1, z1);
        Rlgl.Vertex3f(x2, y2, z2);
        Rlgl.Vertex3f(x3, y3, z3);
    }

    static void Face(Color c, float shade, float x0, float y0, float z0, float x1, float y1, float z1,
        float x2, float y2, float z2, float x3, float y3, float z3)
    {
        Rlgl.Color4ub((byte)(c.R * shade), (byte)(c.G * shade), (byte)(c.B * shade), c.A);
        Rlgl.Vertex3f(x0, y0, z0);
        Rlgl.Vertex3f(x1, y1, z1);
        Rlgl.Vertex3f(x2, y2, z2);
        Rlgl.Vertex3f(x3, y3, z3);
    }
}
