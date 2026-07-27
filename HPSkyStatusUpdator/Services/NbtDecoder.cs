using System.IO.Compression;
using fNbt;

namespace HPSkyStatusUpdator.Services;

public static class NbtDecoder
{
    public static NbtCompound Decode(string itemBytes)
    {
        byte[] compressed =
            Convert.FromBase64String(itemBytes);

        using var ms = new MemoryStream(compressed);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);

        NbtFile file = new();
        file.LoadFromStream(gzip, NbtCompression.None);

        return file.RootTag;
    }
}