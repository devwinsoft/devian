// SSOT: skills/devian-unity/30-unity-components/14-table-manager/SKILL.md

namespace Devian
{
    /// <summary>
    /// Table data format for loading.
    /// </summary>
    public enum TableFormat
    {
        /// <summary>NDJSON format (one JSON object per line)</summary>
        Json,
        
        /// <summary>PB64 format (DVGB container for tables, StringChunk for strings)</summary>
        Pb64
    }
}
