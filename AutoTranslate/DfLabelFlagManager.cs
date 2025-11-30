using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTranslate
{
    public class DfLabelFlagManager : MonoBehaviour
    {

        private static readonly HashSet<string> bossCardNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Boss Name Label", "Boss Quote Label", "Boss Subtitle Label"
        };

        private static readonly HashSet<string> deadLeftNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Statbar_Gungeoneer", "Statbar_Area", "Statbar_Time", "Statbar_Money", "Statbar_Kills"
        };

        private static Dictionary<int, DfLabelFlagManager> managerMap = new Dictionary<int, DfLabelFlagManager>(512);

        private static List<int> deadKeysCache = new List<int>(128);

        public int InstanceID { get; set; }

        public bool IsDefaultLabel { get; set; }

        public bool IsTapeLine { get; set; }

        public bool IsTapeLabel { get; set; }

        public bool IsLabelBox { get; set; }

        public bool IsSlicedSprite { get; set; }

        public bool IsKilledByZone { get; set; }

        public bool IsBossLabel { get; set; }

        public bool IsDeadLeft { get; set; }

        public dfSlicedSprite SlicedSprite { get; set; }

        public static DfLabelFlagManager GetOrAddFlagManager(dfLabel dfLabel)
        {
            if (dfLabel == null)
                return null;

            GameObject go = dfLabel.gameObject;
            if (go == null)
                return null;

            int instanceID = dfLabel.GetInstanceID();

            if (Time.frameCount % 3000 == 0)
                CleanupDeadReferences();

            if (managerMap.TryGetValue(instanceID, out DfLabelFlagManager cachedManager))
            {
                if (cachedManager != null)
                    return cachedManager;
                else
                    managerMap.Remove(instanceID);
            }

            DfLabelFlagManager result = go.GetComponent<DfLabelFlagManager>();
            if (result != null)
            {
                result.InstanceID = instanceID;
                managerMap[instanceID] = result;
                return result;
            }

            result = go.AddComponent<DfLabelFlagManager>();
            if (result == null)
                return null;

            string goName = go.name;
            dfControl parent = dfLabel.parent;
            dfControl grandparent = parent?.parent;
            string parentName = parent?.name;
            string grandparentName = grandparent?.name;

            result.IsDefaultLabel = goName == "DefaultLabel";
            result.IsTapeLine = parentName != null && parentName.StartsWith("Tape Line", StringComparison.Ordinal);
            result.IsTapeLabel = parentName != null && parentName.StartsWith("TapeLabel", StringComparison.Ordinal);
            result.IsLabelBox = parentName == "LabelBox";

            bool shouldGetSprite = result.IsTapeLine || result.IsTapeLabel || result.IsLabelBox;

            if (shouldGetSprite)
                result.SlicedSprite = parent.GetComponentInChildren<dfSlicedSprite>();

            result.IsSlicedSprite = result.SlicedSprite != null && result.SlicedSprite.name == "Sliced Sprite";
            result.IsKilledByZone = grandparentName == "KilledByZone";
            result.IsBossLabel = goName != null && bossCardNames.Contains(goName);
            result.IsDeadLeft = grandparentName != null && deadLeftNames.Contains(grandparentName);

            result.InstanceID = instanceID;
            managerMap[instanceID] = result;

            return result;
        }

        private void OnDestroy()
        {
            managerMap.Remove(InstanceID);
        }

        public static void CleanupDeadReferences()
        {
            deadKeysCache.Clear();

            foreach (var kvp in managerMap)
            {
                if (kvp.Value == null)
                    deadKeysCache.Add(kvp.Key);
            }

            foreach (var key in deadKeysCache)
                managerMap.Remove(key);
        }
    }
}