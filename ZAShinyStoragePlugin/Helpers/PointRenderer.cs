using Point = TransformScale;
// Modified from pkNX (Thanks Kurt!)
namespace ZAShinyStoragePlugin;

public static class PointRenderer
{
    public static void RenderPoint(float[] coords, LumioseFieldIndex map, Bitmap img, Color color)
    {       
        using var gr = Graphics.FromImage(img);
        using var brush = new SolidBrush(color);
        var transformMap = map switch
        {
            LumioseFieldIndex.Overworld => TransformUI.TransformLumiose,
            LumioseFieldIndex.LysandreLabs => TransformUI.TransformLysandreLabs,
            LumioseFieldIndex.SewersCh5 => TransformUI.TransformSewersCh5,
            LumioseFieldIndex.SewersCh6 => TransformUI.TransformSewersCh6,
            _ => throw new ArgumentOutOfRangeException(nameof(map)),
        };
        RenderPointOnMap(gr, transformMap, brush, new Point(coords[0], coords[2]));
    }
    private static void RenderPointOnMap(Graphics gr, MapTransform tr, Brush brush, params ReadOnlySpan<Point> coords)
    {
        gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var borderPen = new Pen(Color.Black, 2);

        foreach (var pt in coords)
        {
            const int length = (10 * 2) + 1; // Diameter of the circle
            var x = (float)tr.ConvertX(pt.X) - (length / 2.0f);
            var y = (float)tr.ConvertZ(pt.Z) - (length / 2.0f);
            gr.FillEllipse(brush, x, y, length, length);
            gr.DrawEllipse(borderPen, x, y, length, length);
        }
    }
}
