namespace RemasterGuru.Infrastructure.Imaging;

public static class ImageDimensionProbe
{
    public static bool TryGetDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!stream.CanSeek)
        {
            return false;
        }

        var start = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[24];
            var read = stream.Read(header);
            if (read < 24)
            {
                return false;
            }

            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }

            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                stream.Position = start;
                return TryGetJpegDimensions(stream, out width, out height);
            }

            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static bool TryGetJpegDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        Span<byte> buf = stackalloc byte[2];
        while (stream.Read(buf) == 2)
        {
            if (buf[0] != 0xFF)
            {
                continue;
            }

            var marker = buf[1];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                Span<byte> segment = stackalloc byte[7];
                if (stream.Read(segment) != 7)
                {
                    return false;
                }

                height = (segment[3] << 8) | segment[4];
                width = (segment[5] << 8) | segment[6];
                return width > 0 && height > 0;
            }

            if (marker == 0xD9)
            {
                return false;
            }

            if (stream.Read(buf) != 2)
            {
                return false;
            }

            var segmentLength = (buf[0] << 8) | buf[1];
            if (segmentLength < 2)
            {
                return false;
            }

            stream.Seek(segmentLength - 2, SeekOrigin.Current);
        }

        return false;
    }
}
