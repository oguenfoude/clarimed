using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ClariMed.Imaging.Converters;

/// <summary>
/// Converts raw DICOM pixel data to standard image formats using System.Drawing.
/// Supports 8-bit and 16-bit grayscale DICOM images with automatic windowing.
/// </summary>
public class ImageConverter : IImageConverter
{
    public void ConvertToPng(byte[] pixelData, int width, int height, int bitsAllocated, string outputPath)
    {
        using var bitmap = CreateBitmap(pixelData, width, height, bitsAllocated);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    public void ConvertToBmp(byte[] pixelData, int width, int height, int bitsAllocated, string outputPath)
    {
        using var bitmap = CreateBitmap(pixelData, width, height, bitsAllocated);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, ImageFormat.Bmp);
    }

    /// <summary>
    /// Creates an 8-bit grayscale Bitmap from raw pixel data.
    /// For 16-bit data, applies automatic window/level normalization.
    /// </summary>
    private static Bitmap CreateBitmap(byte[] pixelData, int width, int height, int bitsAllocated)
    {
        byte[] normalized;

        if (bitsAllocated == 16)
        {
            normalized = Normalize16To8(pixelData, width, height);
        }
        else if (bitsAllocated == 8)
        {
            normalized = pixelData;
        }
        else
        {
            throw new ArgumentException($"Unsupported bits allocated: {bitsAllocated}. Only 8 and 16 are supported.");
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

        // Set grayscale palette
        var palette = bitmap.Palette;
        for (int i = 0; i < 256; i++)
        {
            palette.Entries[i] = Color.FromArgb(i, i, i);
        }
        bitmap.Palette = palette;

        // Copy pixel data into bitmap
        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format8bppIndexed);

        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(
                    normalized,
                    y * width,
                    bmpData.Scan0 + y * bmpData.Stride,
                    width);
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return bitmap;
    }

    /// <summary>
    /// Normalizes 16-bit pixel data to 8-bit using min/max windowing.
    /// </summary>
    private static byte[] Normalize16To8(byte[] pixelData, int width, int height)
    {
        int pixelCount = width * height;
        var values = new ushort[pixelCount];

        for (int i = 0; i < pixelCount; i++)
        {
            values[i] = BitConverter.ToUInt16(pixelData, i * 2);
        }

        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;

        for (int i = 0; i < pixelCount; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        var result = new byte[pixelCount];
        double range = max - min;

        if (range < 1) range = 1; // avoid division by zero

        for (int i = 0; i < pixelCount; i++)
        {
            result[i] = (byte)(((values[i] - min) / range) * 255.0);
        }

        return result;
    }
}
