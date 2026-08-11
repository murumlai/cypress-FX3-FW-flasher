using Fx3Flasher.Core.Safety;

namespace Fx3Flasher.Core.Services
{
    /// <summary>Outcome of a high-level flasher operation (program/erase), including safety details.</summary>
    public sealed class FlasherOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>The safety decision that gated the operation.</summary>
        public SafetyDecision Safety { get; set; }

        /// <summary>Whether post-operation re-enumeration confirmed the expected device state.</summary>
        public bool Verified { get; set; }

        public static FlasherOperationResult Fail(string message, SafetyDecision safety = null)
        {
            return new FlasherOperationResult { Success = false, Message = message, Safety = safety };
        }
    }
}
