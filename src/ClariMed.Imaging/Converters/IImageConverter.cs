namespace ClariMed.Imaging.Converters;

/// <summary>
/// Converts raw DICOM pixel data to standard image formats.
/// </summary>
public interface IImageConverter
{
    /// <summary>
    /// Converts raw grayscale pixel data to a PNG file.
    /// </summary>
    /// <param name="pixelData">Raw pixel bytes from DICOM.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="bitsAllocated">Bits per pixel (8 or 16).</param>
    /// <param name="outputPath">Full path for the output PNG file.</param>
    void ConvertToPng(byte[] pixelData, int width, int height, int bitsAllocated, string outputPath);

    /// <summary>
    /// Saves raw pixel data as a BMP file (for printing).
    /// </summary>
    void ConvertToBmp(byte[] pixelData, int width, int height, int bitsAllocated, string outputPath);
}
