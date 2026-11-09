using System.Buffers.Binary;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var output = Path.Combine(root, "Assets", "ShadeFlow.ico");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var sizes = new[] { 16, 20, 24, 32, 48, 64, 128, 256 };
var images = sizes.Select(CreateImage).ToArray();
using var stream = File.Create(output);
using var writer = new BinaryWriter(stream);
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)images.Length);
var offset = 6 + images.Length * 16;
foreach (var image in images)
{
    writer.Write((byte)(image.Size == 256 ? 0 : image.Size)); writer.Write((byte)(image.Size == 256 ? 0 : image.Size));
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(image.Bytes.Length); writer.Write(offset); offset += image.Bytes.Length;
}
foreach (var image in images) writer.Write(image.Bytes);

static IconImage CreateImage(int size)
{
    var xorStride = size * 4;
    var andStride = ((size + 31) / 32) * 4;
    var bytes = new byte[40 + xorStride * size + andStride * size];
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0), 40);
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), size);
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), size * 2);
    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 1);
    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 32);
    const int samplesPerAxis = 4;
    var center = size / 2d;
    var radius = size * .405;
    var ringWidth = Math.Max(1.15, radius * .09);
    var dividerWidth = Math.Max(1.0, radius * .065);

    for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
    {
        var covered = 0;
        var redTotal = 0;
        var greenTotal = 0;
        var blueTotal = 0;

        for (var sampleY = 0; sampleY < samplesPerAxis; sampleY++)
        for (var sampleX = 0; sampleX < samplesPerAxis; sampleX++)
        {
            var pointX = x + (sampleX + .5) / samplesPerAxis;
            var pointY = y + (sampleY + .5) / samplesPerAxis;
            var dx = pointX - center;
            var dy = pointY - center;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > radius) continue;

            var normalizedY = Math.Clamp(dy / radius, -1d, 1d);
            var curve = center - radius * .085 - Math.Sqrt(1d - normalizedY * normalizedY) * radius * .42;
            var isOuterRing = distance >= radius - ringWidth;
            var isDivider = Math.Abs(pointX - curve) <= dividerWidth / 2d && distance < radius - ringWidth * .45;
            var isShadow = pointX < curve && distance < radius - ringWidth;

            if (!isOuterRing && !isDivider && !isShadow) continue;

            var isLight = isOuterRing || isDivider;
            redTotal += isLight ? 245 : 17;
            greenTotal += isLight ? 245 : 18;
            blueTotal += isLight ? 245 : 20;
            covered++;
        }

        if (covered == 0) continue;

        var alpha = (byte)Math.Round(255d * covered / (samplesPerAxis * samplesPerAxis));
        var red = (byte)(redTotal / covered);
        var green = (byte)(greenTotal / covered);
        var blue = (byte)(blueTotal / covered);
        var pixel = 40 + ((size - 1 - y) * xorStride) + x * 4;
        bytes[pixel] = blue;
        bytes[pixel + 1] = green;
        bytes[pixel + 2] = red;
        bytes[pixel + 3] = alpha;
    }
    return new IconImage(size, bytes);
}

sealed record IconImage(int Size, byte[] Bytes);
