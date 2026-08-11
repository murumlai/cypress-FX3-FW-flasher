using System.Collections.Generic;

namespace Fx3Flasher.Core.Safety
{
    /// <summary>The requested destructive operation the safety gate is evaluating.</summary>
    public enum FlashOperation
    {
        Program = 0,
        EraseToBlank = 1,
        FullErase = 2,
        Verify = 3
    }

    /// <summary>Aggregated result of the fail-closed EEPROM safety evaluation.</summary>
    public sealed class SafetyDecision
    {
        private readonly List<string> _blockers = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        /// <summary>The operation is only permitted when there are no blockers.</summary>
        public bool IsAllowed
        {
            get { return _blockers.Count == 0; }
        }

        public IReadOnlyList<string> Blockers
        {
            get { return _blockers; }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        public void Block(string reason)
        {
            _blockers.Add(reason);
        }

        public void Warn(string reason)
        {
            _warnings.Add(reason);
        }
    }
}
