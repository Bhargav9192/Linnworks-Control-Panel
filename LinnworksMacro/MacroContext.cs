using System.Collections.Generic;

namespace Linnworks.Abstractions
{
    public class MacroContext
    {
        // ahi tame tamari API / helpers pass karso
        public object Api { get; set; }

        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}