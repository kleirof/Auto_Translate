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

            DfLabelFlagManager result = go.GetComponent<DfLabelFlagManager>();
            if (result != null)
                return result;

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

            return result;
        }
    }
}
