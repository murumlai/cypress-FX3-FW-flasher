using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Fx3Flasher.Core.Models;

namespace Fx3Flasher.Core.Profiles
{
    /// <summary>Loads supported board profiles from external JSON configuration.</summary>
    public sealed class BoardProfileStore
    {
        private readonly List<BoardProfile> _profiles = new List<BoardProfile>();

        public IReadOnlyList<BoardProfile> Profiles
        {
            get { return _profiles; }
        }

        public static BoardProfileStore LoadFromFile(string path)
        {
            var store = new BoardProfileStore();
            if (!File.Exists(path))
            {
                return store;
            }

            string json = File.ReadAllText(path);
            store.LoadFromJson(json);
            return store;
        }

        public void LoadFromJson(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            List<BoardProfile> profiles = JsonSerializer.Deserialize<List<BoardProfile>>(json, options);
            _profiles.Clear();
            if (profiles != null)
            {
                _profiles.AddRange(profiles);
            }
        }

        /// <summary>
        /// Resolve the single profile that matches a device identity. Returns null when no profile
        /// matches, and sets <paramref name="ambiguous"/> when more than one profile claims the device.
        /// </summary>
        public BoardProfile Resolve(int vendorId, int productId, out bool ambiguous)
        {
            ambiguous = false;
            BoardProfile match = null;

            for (int i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i].MatchesAny(vendorId, productId))
                {
                    if (match != null)
                    {
                        ambiguous = true;
                        return null;
                    }

                    match = _profiles[i];
                }
            }

            return match;
        }
    }
}
