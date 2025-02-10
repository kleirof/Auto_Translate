using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AutoTranslate
{
    public class FontManager
    {
        public string regexForDfTokenizer;
        public string regexForItemTipsModTokenizer;
        public AssetBundle assetBundle;
        public dfFontBase dfFontBase;
        public tk2dFontData tk2dFont;
        public AutoTranslateModule.OverrideFontType overrideFont;

        public static string defaultRegexForTokenizer = @"(?<StartTag>\[(?<StartTagName>(color|sprite))\s*(?<AttributeValue>[^\]\s]+)?\])|(?<EndTag>\[\/(?<EndTagName>color|sprite)\])|(?<Newline>\r?\n)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.)";
        public static string defaultRegexForItemTipsModTokenizer = @"(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)";

        internal Regex DfTokenizerRegex;
        internal Regex ItemTipsModTokenizerRegex;

        public static FontManager instance;
        private AutoTranslateConfig config;

        internal Type itemTipsModuleType;
        internal object itemTipsModuleObject;
        internal SGUI.SLabel itemTipsModuleLabel;
        internal Font itemTipsModuleFont;
        internal string itemTipsModuleText;

        private static StringBuilder currentLine = new StringBuilder();

        public FontManager()
        {
            instance = this;
            config = AutoTranslateModule.instance.config;
            regexForDfTokenizer = config.RegexForDfTokenizer == string.Empty ? defaultRegexForTokenizer : config.RegexForDfTokenizer;
            regexForItemTipsModTokenizer = config.RegexForItemTipsModTokenizer == string.Empty ? defaultRegexForTokenizer : config.RegexForItemTipsModTokenizer;
            DfTokenizerRegex = new Regex(regexForDfTokenizer, RegexOptions.Compiled | RegexOptions.Multiline);
            ItemTipsModTokenizerRegex = new Regex(regexForItemTipsModTokenizer, RegexOptions.Compiled | RegexOptions.Multiline);

            overrideFont = config.OverrideFont;
            if (overrideFont != AutoTranslateModule.OverrideFontType.Custom)
            {
                if (overrideFont == AutoTranslateModule.OverrideFontType.None || overrideFont == AutoTranslateModule.OverrideFontType.English || overrideFont == AutoTranslateModule.OverrideFontType.Polish)
                    return;
                else if (overrideFont == AutoTranslateModule.OverrideFontType.Chinese)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/SimSun12_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/SimSun12_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (overrideFont == AutoTranslateModule.OverrideFontType.Japanese)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/JackeyFont12_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/JackeyFont_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (overrideFont == AutoTranslateModule.OverrideFontType.Korean)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/NanumGothic16_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/NanumGothic16TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (overrideFont == AutoTranslateModule.OverrideFontType.Russian)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/PixelaCYR_15_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/PixelaCYR_15_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                return;
            }

            if (config.FontAssetBundleName == string.Empty || (config.CustomDfFontName == string.Empty && config.CustomTk2dFontName == string.Empty))
                return;

            string assetBundlePath = Path.Combine(ETGMod.FolderPath(AutoTranslateModule.instance), config.FontAssetBundleName);
            try
            {
                assetBundle = AssetBundleLoader.LoadAssetBundle(assetBundlePath);
                dfFontBase = assetBundle.LoadAsset<GameObject>(config.CustomDfFontName).GetComponent<dfFontBase>();
                tk2dFont = assetBundle.LoadAsset<GameObject>(config.CustomTk2dFontName).GetComponent<tk2dFont>().data;
            }
            catch { }
        }

        private int EstimateTokenCount(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return 0;
            }
            return source.Length;
        }

        public dfList<dfMarkupToken> Tokenize(string source)
        {
            dfList<dfMarkupToken> dfList = dfList<dfMarkupToken>.Obtain();
            dfList.EnsureCapacity(this.EstimateTokenCount(source));
            dfList.AutoReleaseItems = true;

            MatchCollection matches = DfTokenizerRegex.Matches(source);

            foreach (Match match in matches)
            {
                if (match.Groups["StartTag"].Success)
                {
                    Capture startTagName = match.Groups["StartTagName"];
                    Capture attributeValue = match.Groups["AttributeValue"];

                    dfMarkupToken token = dfMarkupToken.Obtain(source, dfMarkupTokenType.StartTag, startTagName.Index, startTagName.Index + startTagName.Length - 1);

                    if (attributeValue.Value != string.Empty)
                    {
                        string attribute = match.Groups["AttributeValue"].Value;
                        bool quoted = attribute.StartsWith("\"") && attribute.EndsWith("\"");

                        dfMarkupToken attributeToken = dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, quoted ? attributeValue.Index + 1 : attributeValue.Index, quoted ? attributeValue.Index + attributeValue.Length - 2 : attributeValue.Index + attributeValue.Length - 1);

                        token.AddAttribute(attributeToken, attributeToken);
                    }
                    dfList.Add(token);
                }
                else if (match.Groups["EndTag"].Success)
                {
                    Capture endTagName = match.Groups["EndTagName"];
                    if (endTagName.Value != string.Empty)
                        dfList.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.EndTag, endTagName.Index, endTagName.Index + endTagName.Length - 1));
                }
                else if (match.Groups["Text"].Success)
                {
                    dfList.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, match.Index, match.Index + match.Length - 1));
                }
                else if (match.Groups["Whitespace"].Success)
                {
                    dfList.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Whitespace, match.Index, match.Index + match.Length - 1));
                }
                else if (match.Groups["Newline"].Success)
                {
                    dfList.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Newline, match.Index, match.Index + match.Length - 1));
                }
            }
            return dfList;
        }

        internal static dfFont GetGameFont()
        {
            dfFont dfFont = null;
            StringTableManager.GungeonSupportedLanguages languages = GameManager.Options.CurrentLanguage;
            switch (languages)
            {
                case StringTableManager.GungeonSupportedLanguages.CHINESE:
                    dfFont = (ResourceCache.Acquire("Alternate Fonts/SimSun12_DF") as GameObject).GetComponent<dfFont>();
                    break;
                case StringTableManager.GungeonSupportedLanguages.ENGLISH:
                    dfFont = GameUIRoot.Instance.Manager.defaultFont as dfFont;
                    break;
                case StringTableManager.GungeonSupportedLanguages.JAPANESE:
                    dfFont = (ResourceCache.Acquire("Alternate Fonts/JackeyFont12_DF") as GameObject).GetComponent<dfFont>();
                    break;
                case StringTableManager.GungeonSupportedLanguages.KOREAN:
                    dfFont = (ResourceCache.Acquire("Alternate Fonts/NanumGothic16_DF") as GameObject).GetComponent<dfFont>();
                    break;
                case StringTableManager.GungeonSupportedLanguages.RUSSIAN:
                    dfFont = (ResourceCache.Acquire("Alternate Fonts/PixelaCYR_15_DF") as GameObject).GetComponent<dfFont>();
                    break;
                case StringTableManager.GungeonSupportedLanguages.POLISH:
                    dfFont = GameUIRoot.Instance.Manager.defaultFont as dfFont;
                    break;
            }
            return dfFont;
        }

        internal void InitializeFontAfterGameManager(AutoTranslateModule.OverrideFontType fontType)
        {
            if (fontType == AutoTranslateModule.OverrideFontType.English || fontType == AutoTranslateModule.OverrideFontType.Polish)
            {
                dfFontBase = GameUIRoot.Instance.Manager.defaultFont as dfFont;
                tk2dFont = GameManager.Instance.DefaultNormalConversationFont;
            }
        }

        internal static void SetTextMeshFont(tk2dTextMesh textMesh, tk2dFontData font)
        {
            textMesh.UpgradeData();
            textMesh.data.font = font;
            textMesh._fontInst = textMesh.data.font.inst;
            textMesh.SetNeedUpdate(tk2dTextMesh.UpdateFlags.UpdateText);
            textMesh.UpdateMaterial();
        }

        public string WrapText(string text, out Vector2 resultSize)
        {
            string result = WrapTextWithTokenizer(itemTipsModuleLabel, text, ItemTipsModTokenizerRegex, itemTipsModuleFont, 500, out Vector2 sizeVector);
            resultSize = sizeVector;
            return result;
        }

        public static string WrapTextWithTokenizer(SGUI.SLabel sLabel, string text, Regex tokenizer, Font font, int maxWidth, out Vector2 resultSize)
        {
            List<string> wrappedLines = new List<string>();

            string[] originalLines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string origLine in originalLines)
            {
                if (string.IsNullOrEmpty(origLine))
                {
                    wrappedLines.Add("");
                    continue;
                }

                MatchCollection tokens = tokenizer.Matches(origLine);
                if (tokens.Count == 0)
                {
                    wrappedLines.Add(origLine);
                    continue;
                }

                currentLine.Length = 0;

                foreach (Match tokenMatch in tokens)
                {
                    string token = tokenMatch.Value;
                    float tokenWidth = sLabel.Backend.MeasureText(token, null, font).x;
                    float currentWidth = sLabel.Backend.MeasureText(currentLine.ToString(), null, font).x;
                    float spaceWidth = sLabel.Backend.MeasureText(" ", null, font).x;

                    if (currentLine.Length > 0)
                    {
                        if (currentWidth + spaceWidth + tokenWidth > maxWidth)
                        {
                            wrappedLines.Add(currentLine.ToString());
                            currentLine.Length = 0;

                            if (tokenWidth > maxWidth)
                                SplitAndAddToken(token, sLabel, font, maxWidth, wrappedLines);
                            else
                                currentLine.Append(token);
                        }
                        else
                            currentLine.Append(token);
                    }
                    else
                    {
                        if (tokenWidth > maxWidth)
                            SplitAndAddToken(token, sLabel, font, maxWidth, wrappedLines);
                        else
                            currentLine.Append(token);
                    }
                }

                if (currentLine.Length > 0)
                    wrappedLines.Add(currentLine.ToString());
            }

            float overallWidth = 0f;
            float overallHeight = 0f;
            foreach (string line in wrappedLines)
            {
                Vector2 lineSize = sLabel.Backend.MeasureText(line, null, font);
                overallWidth = Math.Max(overallWidth, lineSize.x);
                overallHeight += lineSize.y;
            }
            resultSize = new Vector2(overallWidth, overallHeight);

            return string.Join("\n", wrappedLines.ToArray());
        }

        private static void SplitAndAddToken(string token, SGUI.SLabel sLabel, Font font, int maxWidth, List<string> wrappedLines)
        {
            int startIndex = 0;
            while (startIndex < token.Length)
            {
                int length = 1;
                while (startIndex + length <= token.Length &&
                       sLabel.Backend.MeasureText(token.Substring(startIndex, length), null, font).x <= maxWidth)
                {
                    length++;
                }
                length = Math.Max(1, length - 1);
                string part = token.Substring(startIndex, length);
                wrappedLines.Add(part);
                startIndex += length;
            }
        }
    }
}
