using System;
using System.Collections.Generic;
using AlienInvaders.Core;
using AlienInvaders.Core.Records;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Unity implementation of the record serializer. JsonUtility cannot serialize a bare list,
    /// so the entries are wrapped in a small container class with a version number.
    /// </summary>
    public class JsonUtilityRecordSerializer : IRecordSerializer
    {
        [Serializable]
        private class RecordFile
        {
            public int version = 1;
            public List<MatchResult> entries = new List<MatchResult>();
        }

        public string Serialize(List<MatchResult> entries)
        {
            var file = new RecordFile { entries = entries ?? new List<MatchResult>() };
            return JsonUtility.ToJson(file, prettyPrint: true);
        }

        public List<MatchResult> Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<MatchResult>();
            }

            RecordFile file = JsonUtility.FromJson<RecordFile>(text);
            return file?.entries ?? new List<MatchResult>();
        }
    }
}
