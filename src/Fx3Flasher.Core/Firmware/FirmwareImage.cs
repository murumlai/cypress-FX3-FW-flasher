using System;
using System.IO;

namespace Fx3Flasher.Core.Firmware
{
    /// <summary>A firmware image loaded from disk, retained in memory for validation and programming.</summary>
    public sealed class FirmwareImage
    {
        public string FilePath { get; private set; }
        public byte[] Data { get; private set; }

        public FirmwareImage(string filePath, byte[] data)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path is required.", "filePath");
            }

            FilePath = filePath;
            Data = data ?? throw new ArgumentNullException("data");
        }

        public long Length
        {
            get { return Data.LongLength; }
        }

        public string FileName
        {
            get { return Path.GetFileName(FilePath); }
        }

        public static FirmwareImage Load(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return new FirmwareImage(filePath, bytes);
        }
    }
}
