using System.Collections.Generic;

namespace AlienInvaders.Core.Records
{
    /// <summary>
    /// Converts the high-score list to text and back. The core does the file reading and
    /// writing; the JSON format is provided by the Unity layer (JsonUtility) so the core
    /// stays free of engine references.
    /// </summary>
    public interface IRecordSerializer
    {
        string Serialize(List<MatchResult> entries);
        List<MatchResult> Deserialize(string text);
    }
}
