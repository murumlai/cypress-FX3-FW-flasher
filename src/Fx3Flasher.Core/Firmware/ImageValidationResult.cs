using System.Collections.Generic;

namespace Fx3Flasher.Core.Firmware
{
    /// <summary>Outcome of validating a firmware image against the FX3 format and a board profile.</summary>
    public sealed class ImageValidationResult
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public bool IsValid
        {
            get { return _errors.Count == 0; }
        }

        public IReadOnlyList<string> Errors
        {
            get { return _errors; }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>Total number of payload bytes described by the image sections.</summary>
        public long PayloadBytes { get; internal set; }

        /// <summary>Firmware entry address parsed from the image trailer.</summary>
        public uint EntryAddress { get; internal set; }

        /// <summary>Whether the trailing checksum matched the computed value.</summary>
        public bool ChecksumValid { get; internal set; }

        internal void AddError(string message)
        {
            _errors.Add(message);
        }

        internal void AddWarning(string message)
        {
            _warnings.Add(message);
        }
    }
}
