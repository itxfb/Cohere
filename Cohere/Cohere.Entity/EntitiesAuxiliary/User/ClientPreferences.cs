using System.Collections.Generic;

using Cohere.Entity.Enums.User;

namespace Cohere.Entity.EntitiesAuxiliary.User
{
    public class ClientPreferences
    {
        public Dictionary<string, PreferenceLevels> Experiences { get; set; }

        public Dictionary<string, PreferenceLevels> Interests { get; set; }

        public Dictionary<string, PreferenceLevels> Curiosities { get; set; }
    }
}
