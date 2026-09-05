namespace GameForWork.Core.Presentation;

public readonly record struct ProjectedPoint(float X, float Y);

public readonly record struct ProjectedSquare(float X, float Y, float Side);

/// <summary>
/// Shared world/source/screen projection for every baked talent-tree backdrop.
/// The source canvas is square and represents [-extent, +extent] on both axes.
/// </summary>
public static class TreeProjection
{
    public const int PassiveSourceSize = 2_048;
    public const int AtlasSourceSize = 2_048;
    public const int AscendancySourceSize = 768;

    public static float Normalize(float coordinate, float extent)
    {
        RequireExtent(extent);
        return coordinate / extent;
    }

    public static float SourcePixel(float coordinate, float extent, int sourceSize)
    {
        RequireSourceSize(sourceSize);
        return (Normalize(coordinate, extent) + 1f) * sourceSize / 2f;
    }

    public static ProjectedPoint WorldToScreen(float worldX, float worldY,
        float centerX, float centerY, float zoom)
    {
        RequireZoom(zoom);
        return new(centerX + worldX * zoom, centerY + worldY * zoom);
    }

    public static ProjectedPoint ScreenToWorld(float screenX, float screenY,
        float centerX, float centerY, float zoom)
    {
        RequireZoom(zoom);
        return new((screenX - centerX) / zoom, (screenY - centerY) / zoom);
    }

    public static ProjectedSquare BackdropSquare(float centerX, float centerY,
        float extent, float zoom)
    {
        RequireExtent(extent);
        RequireZoom(zoom);
        float half = extent * zoom;
        return new(centerX - half, centerY - half, half * 2f);
    }

    public static ProjectedPoint SourcePixelToScreen(float pixelX, float pixelY, int sourceSize,
        float centerX, float centerY, float extent, float zoom)
    {
        RequireSourceSize(sourceSize);
        ProjectedSquare square = BackdropSquare(centerX, centerY, extent, zoom);
        return new(square.X + pixelX / sourceSize * square.Side,
            square.Y + pixelY / sourceSize * square.Side);
    }

    private static void RequireExtent(float extent)
    {
        if (!float.IsFinite(extent) || extent <= 0) throw new ArgumentOutOfRangeException(nameof(extent));
    }

    private static void RequireSourceSize(int sourceSize)
    {
        if (sourceSize <= 0) throw new ArgumentOutOfRangeException(nameof(sourceSize));
    }

    private static void RequireZoom(float zoom)
    {
        if (!float.IsFinite(zoom) || zoom <= 0) throw new ArgumentOutOfRangeException(nameof(zoom));
    }
}
