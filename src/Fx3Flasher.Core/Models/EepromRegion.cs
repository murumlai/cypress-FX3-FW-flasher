namespace Fx3Flasher.Core.Models
{
    /// <summary>A byte range within the EEPROM address space.</summary>
    public sealed class EepromRegion
    {
        public string Name { get; set; }
        public long Start { get; set; }
        public long Length { get; set; }

        public long EndExclusive
        {
            get { return Start + Length; }
        }

        public bool Overlaps(long start, long length)
        {
            long end = start + length;
            return start < EndExclusive && Start < end;
        }
    }
}
