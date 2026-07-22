using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Software-rasterizes Spine region/mesh attachments into a WPF WriteableBitmap.
/// </summary>
public sealed class WpfSkeletonRenderer : IDisposable
{
    private static readonly int[] QuadTriangles = [0, 1, 2, 2, 3, 0];

    private readonly SkeletonClipping _clipper = new();
    private WriteableBitmap? _bitmap;
    private int[]? _buffer;
    private float[] _worldVertices = new float[8];

    public ImageSource? ImageSource => _bitmap;

    public void EnsureSize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_bitmap is not null && _bitmap.PixelWidth == width && _bitmap.PixelHeight == height)
        {
            return;
        }

        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _buffer = new int[width * height];
    }

    public void Render(Skeleton skeleton, float scale, float offsetX, float offsetY)
    {
        if (_bitmap is null || _buffer is null)
        {
            return;
        }

        Array.Clear(_buffer);

        var drawOrder = skeleton.DrawOrder.AppliedPose;
        var slots = drawOrder.Items;
        for (var i = 0; i < drawOrder.Count; i++)
        {
            var slot = slots[i];
            if (!slot.Bone.Active)
            {
                continue;
            }

            var attachment = slot.AppliedPose.Attachment;
            if (attachment is RegionAttachment region)
            {
                DrawRegion(skeleton, slot, region, scale, offsetX, offsetY);
            }
            else if (attachment is MeshAttachment mesh)
            {
                DrawMesh(skeleton, slot, mesh, scale, offsetX, offsetY);
            }
            else if (attachment is ClippingAttachment clip)
            {
                _clipper.ClipStart(skeleton, slot, clip);
                continue;
            }

            _clipper.ClipEnd(slot);
        }

        _clipper.ClipEnd();
        Blit();
    }

    private void DrawRegion(
        Skeleton skeleton,
        Slot slot,
        RegionAttachment region,
        float scale,
        float offsetX,
        float offsetY)
    {
        var pose = slot.AppliedPose;
        var index = region.Sequence.ResolveIndex(pose);
        if (region.Sequence.GetRegion(index) is not AtlasRegion textureRegion ||
            textureRegion.page.rendererObject is not AtlasTexture atlas)
        {
            return;
        }

        var offsets = region.GetOffsets(pose);
        var uvs = region.Sequence.GetUVs(index);
        EnsureVertexCapacity(8);
        region.ComputeWorldVertices(slot, offsets, _worldVertices, 0, 2);

        float[] vertices = _worldVertices;
        int[] triangles = QuadTriangles;
        float[] drawUvs = uvs;
        var vertexCount = 8;
        var triangleCount = triangles.Length;

        if (_clipper.IsClipping)
        {
            if (!_clipper.ClipTriangles(vertices, triangles, triangles.Length, uvs))
            {
                return;
            }

            vertices = _clipper.ClippedVertices.Items;
            triangles = _clipper.ClippedTriangles.Items;
            drawUvs = _clipper.ClippedUVs.Items;
            vertexCount = _clipper.ClippedVertices.Count;
            triangleCount = _clipper.ClippedTriangles.Count;
        }

        var tint = Multiply(Multiply(skeleton.GetColor(), pose.GetColor()), region.GetColor());
        Rasterize(atlas, vertices, vertexCount, drawUvs, triangles, triangleCount, tint, scale, offsetX, offsetY);
    }

    private void DrawMesh(
        Skeleton skeleton,
        Slot slot,
        MeshAttachment mesh,
        float scale,
        float offsetX,
        float offsetY)
    {
        var pose = slot.AppliedPose;
        var index = mesh.Sequence.ResolveIndex(pose);
        if (mesh.Sequence.GetRegion(index) is not AtlasRegion textureRegion ||
            textureRegion.page.rendererObject is not AtlasTexture atlas)
        {
            return;
        }

        var verticesLength = mesh.WorldVerticesLength;
        EnsureVertexCapacity(verticesLength);
        mesh.ComputeWorldVertices(skeleton, slot, 0, verticesLength, _worldVertices, 0, 2);

        float[] vertices = _worldVertices;
        float[] uvs = mesh.Sequence.GetUVs(index);
        int[] triangles = mesh.Triangles;
        var vertexCount = verticesLength;
        var triangleCount = triangles.Length;

        if (_clipper.IsClipping)
        {
            if (!_clipper.ClipTriangles(vertices, triangles, triangles.Length, uvs))
            {
                return;
            }

            vertices = _clipper.ClippedVertices.Items;
            triangles = _clipper.ClippedTriangles.Items;
            uvs = _clipper.ClippedUVs.Items;
            vertexCount = _clipper.ClippedVertices.Count;
            triangleCount = _clipper.ClippedTriangles.Count;
        }

        var tint = Multiply(Multiply(skeleton.GetColor(), pose.GetColor()), mesh.GetColor());
        Rasterize(atlas, vertices, vertexCount, uvs, triangles, triangleCount, tint, scale, offsetX, offsetY);
    }

    private void Rasterize(
        AtlasTexture atlas,
        float[] vertices,
        int vertexFloatCount,
        float[] uvs,
        int[] triangles,
        int triangleCount,
        Color32F tint,
        float scale,
        float offsetX,
        float offsetY)
    {
        if (_bitmap is null || _buffer is null || triangleCount < 3)
        {
            return;
        }

        var width = _bitmap.PixelWidth;
        var height = _bitmap.PixelHeight;
        var atlasW = atlas.Width;
        var atlasH = atlas.Height;
        var atlasPixels = atlas.Pixels;

        for (var t = 0; t < triangleCount; t += 3)
        {
            var i0 = triangles[t];
            var i1 = triangles[t + 1];
            var i2 = triangles[t + 2];

            // Spine Y-up -> screen Y-down
            var x0 = vertices[i0 * 2] * scale + offsetX;
            var y0 = -vertices[i0 * 2 + 1] * scale + offsetY;
            var x1 = vertices[i1 * 2] * scale + offsetX;
            var y1 = -vertices[i1 * 2 + 1] * scale + offsetY;
            var x2 = vertices[i2 * 2] * scale + offsetX;
            var y2 = -vertices[i2 * 2 + 1] * scale + offsetY;

            var u0 = uvs[i0 * 2];
            var v0 = uvs[i0 * 2 + 1];
            var u1 = uvs[i1 * 2];
            var v1 = uvs[i1 * 2 + 1];
            var u2 = uvs[i2 * 2];
            var v2 = uvs[i2 * 2 + 1];

            var minX = (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2)));
            var maxX = (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2)));
            var minY = (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2)));
            var maxY = (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2)));

            minX = Math.Clamp(minX, 0, width - 1);
            maxX = Math.Clamp(maxX, 0, width - 1);
            minY = Math.Clamp(minY, 0, height - 1);
            maxY = Math.Clamp(maxY, 0, height - 1);

            var area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
            if (Math.Abs(area) < 0.0001f)
            {
                continue;
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var w0 = ((x1 - x) * (y2 - y) - (x2 - x) * (y1 - y)) / area;
                    var w1 = ((x2 - x) * (y0 - y) - (x0 - x) * (y2 - y)) / area;
                    var w2 = 1f - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0)
                    {
                        continue;
                    }

                    var u = w0 * u0 + w1 * u1 + w2 * u2;
                    var v = w0 * v0 + w1 * v1 + w2 * v2;
                    var tx = Math.Clamp((int)(u * atlasW), 0, atlasW - 1);
                    var ty = Math.Clamp((int)(v * atlasH), 0, atlasH - 1);
                    var srcIndex = (ty * atlasW + tx) * 4;

                    var sb = atlasPixels[srcIndex];
                    var sg = atlasPixels[srcIndex + 1];
                    var sr = atlasPixels[srcIndex + 2];
                    var sa = atlasPixels[srcIndex + 3];
                    if (sa == 0)
                    {
                        continue;
                    }

                    var a = (sa / 255f) * tint.a;
                    if (a <= 0.001f)
                    {
                        continue;
                    }

                    var r = (sr / 255f) * tint.r;
                    var g = (sg / 255f) * tint.g;
                    var b = (sb / 255f) * tint.b;

                    var dstIndex = y * width + x;
                    var dst = _buffer[dstIndex];
                    var da = ((dst >> 24) & 0xff) / 255f;
                    var dr = ((dst >> 16) & 0xff) / 255f;
                    var dg = ((dst >> 8) & 0xff) / 255f;
                    var db = (dst & 0xff) / 255f;

                    // Source-over
                    var outA = a + da * (1 - a);
                    var outR = outA > 0 ? (r * a + dr * da * (1 - a)) / outA : 0;
                    var outG = outA > 0 ? (g * a + dg * da * (1 - a)) / outA : 0;
                    var outB = outA > 0 ? (b * a + db * da * (1 - a)) / outA : 0;

                    _buffer[dstIndex] =
                        ((int)(outA * 255) << 24) |
                        ((int)(outR * 255) << 16) |
                        ((int)(outG * 255) << 8) |
                        (int)(outB * 255);
                }
            }
        }
    }

    private void EnsureVertexCapacity(int count)
    {
        if (_worldVertices.Length < count)
        {
            _worldVertices = new float[count];
        }
    }

    private static Color32F Multiply(Color32F a, Color32F b) =>
        new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

    private void Blit()
    {
        if (_bitmap is null || _buffer is null)
        {
            return;
        }

        _bitmap.WritePixels(
            new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight),
            _buffer,
            _bitmap.PixelWidth * 4,
            0);
    }

    public void Dispose()
    {
        _bitmap = null;
        _buffer = null;
    }
}
