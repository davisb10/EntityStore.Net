using System;

namespace EntityStore.Net.HelperClasses
{
    internal class Metadata
    {
        internal Guid StreamGuid { get; set; }

        internal string StreamDataType { get; set; }

        internal DateTime EventEntryDate { get; set; }
    }
}
