using System.Buffers.Binary;
using System.IO.Compression;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Constants;
using Harekat.Telemetry.Domain.Entities;

namespace Harekat.Telemetry.Infrastructure.Heatmap;

/// <summary>
/// Kuzgun Vadisi (-512..512) ısı haritasını bağımlılıksız PNG olarak üretir.
/// </summary>
public sealed class HeatmapPngRenderer : IHeatmapImageRenderer
{
    public byte[] RenderPng(IReadOnlyList<HeatmapCell> cells, HeatmapRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var size = Math.Clamp(options.PixelSize, 64, 2048);
        var grid = MapBounds.GridDimension();
        var pixels = new byte[size * size * 4]; // RGBA

        // koyu zemin
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 18;
            pixels[i + 1] = 22;
            pixels[i + 2] = 28;
            pixels[i + 3] = 255;
        }

        var max = 1;
        if (cells.Count > 0)
        {
            max = Math.Max(1, cells.Max(c =>
            {
                var v = 0;
                if (options.Deaths) v += c.DeathCount;
                if (options.Landings) v += c.LandingCount;
                return v;
            }));
        }

        foreach (var cell in cells)
        {
            var value = 0;
            if (options.Deaths) value += cell.DeathCount;
            if (options.Landings) value += cell.LandingCount;
            if (value <= 0) continue;

            var t = Math.Clamp((double)value / max, 0, 1);
            var (r, g, b) = HeatColor(t, options.Deaths, options.Landings, cell);

            var x0 = (int)(cell.GridX / (double)grid * size);
            var z0 = (int)(cell.GridZ / (double)grid * size);
            var x1 = (int)((cell.GridX + 1) / (double)grid * size);
            var z1 = (int)((cell.GridZ + 1) / (double)grid * size);

            for (var z = z0; z < Math.Max(z0 + 1, z1); z++)
            for (var x = x0; x < Math.Max(x0 + 1, x1); x++)
            {
                if ((uint)x >= (uint)size || (uint)z >= (uint)size) continue;
                // PNG satırları üstten; harita Z artışı yukarı
                var row = size - 1 - z;
                var idx = (row * size + x) * 4;
                pixels[idx] = r;
                pixels[idx + 1] = g;
                pixels[idx + 2] = b;
                pixels[idx + 3] = 255;
            }
        }

        return EncodeRgbaPng(pixels, size, size);
    }

    private static (byte R, byte G, byte B) HeatColor(double t, bool deaths, bool landings, HeatmapCell cell)
    {
        // ölüm=kırmızı-turuncu, iniş=cyan-yeşil, ikisi=karışım
        if (deaths && !landings)
            return ((byte)(80 + 175 * t), (byte)(20 + 80 * t), (byte)(10 + 20 * t));
        if (landings && !deaths)
            return ((byte)(10 + 40 * t), (byte)(80 + 140 * t), (byte)(90 + 140 * t));

        var deathW = cell.DeathCount / (double)Math.Max(1, cell.Total);
        var r = (byte)(30 + 200 * t * deathW + 40 * t * (1 - deathW));
        var g = (byte)(40 + 80 * t * deathW + 160 * t * (1 - deathW));
        var b = (byte)(20 + 30 * t * deathW + 160 * t * (1 - deathW));
        return (r, g, b);
    }

    /// <summary>Minimal RGBA PNG encoder (IHDR + IDAT + IEND).</summary>
    public static byte[] EncodeRgbaPng(byte[] rgba, int width, int height)
    {
        using var ms = new MemoryStream();
        // signature
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        WriteChunk(ms, "IHDR"u8, ihdr);

        var raw = new byte[(width * 4 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (width * 4 + 1);
            raw[rowStart] = 0; // filter None
            Buffer.BlockCopy(rgba, y * width * 4, raw, rowStart + 1, width * 4);
        }

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw);
            WriteChunk(ms, "IDAT"u8, compressed.ToArray());
        }

        WriteChunk(ms, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        s.Write(type);
        s.Write(data);
        var crc = Crc32.Compute(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }
}

internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (var b in type) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
