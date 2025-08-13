using SGUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static string defaultRegexForTokenizer = @"[a-zA-Z0-9]+|.";
        public static string defaultRegexForItemTipsModTokenizer = @"(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)";

        internal Regex DfTokenizerRegex;
        internal Regex ItemTipsModTokenizerRegex;

        internal static FontManager instance;
        private AutoTranslateConfig config;

        internal Type itemTipsModuleType;
        internal object itemTipsModuleObject;
        internal SLabel itemTipsModuleLabel;
        internal Font itemTipsModuleFont;
        internal Font potentialItemTipsDynamicBaseFont;
        internal string itemTipsModuleText;

        private StringBuilder currentLine = new StringBuilder();
        private List<string> wrappedLines = new List<string>();
        private string[] newLineSymbols = new string[] { "\r\n", "\n" };

        internal readonly Vector2 itemTipsDefaultAnchor = new Vector2(0.1f, 0.5f);
        internal readonly Vector2 itemTipsDefaultPivot = new Vector2(0f, 0.5f);

        internal Vector2 itemTipsAnchor;
        internal Vector2 itemTipsPivot;

        internal int originalLineHeight;

        public FontManager()
        {
            instance = this;
            config = AutoTranslateModule.instance.config;
            if (config.OverrideDfTokenizer == AutoTranslateModule.OverrideDfTokenizerType.CustomRegex)
            {
                regexForDfTokenizer = config.RegexForDfTokenizer == string.Empty ? defaultRegexForTokenizer : config.RegexForDfTokenizer;
                DfTokenizerRegex = new Regex(regexForDfTokenizer, RegexOptions.Compiled | RegexOptions.Multiline);
            }
            if (config.OverrideItemTipsTokenizer == AutoTranslateModule.OverrideItemTipsTokenizerType.CustomRegex)
            {
                regexForItemTipsModTokenizer = config.RegexForItemTipsModTokenizer == string.Empty ? defaultRegexForTokenizer : config.RegexForItemTipsModTokenizer;
                ItemTipsModTokenizerRegex = new Regex(regexForItemTipsModTokenizer, RegexOptions.Compiled | RegexOptions.Multiline);
            }

            try
            {
                itemTipsAnchor = StatusLabelController.ParseVector2(config.ItemTipsAnchor);
                itemTipsPivot = StatusLabelController.ParseVector2(config.ItemTipsPivot);
            }
            catch
            {
                itemTipsAnchor = itemTipsDefaultAnchor;
                itemTipsPivot = itemTipsDefaultPivot;
            }

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
            dfList<dfMarkupToken> tokens = dfList<dfMarkupToken>.Obtain();
            tokens.EnsureCapacity(this.EstimateTokenCount(source));
            tokens.AutoReleaseItems = true;

            int length = source.Length;
            int index = 0;

            while (index < length)
            {
                char c = source[index];

                if (c == '[' && index + 1 < length && source[index + 1] != '/')
                {
                    int tagStart = index++;
                    int tagNameStart = index;

                    while (index < length && !char.IsWhiteSpace(source[index]) && source[index] != ']' && source[index] != '=')
                        index++;

                    if (tagNameStart >= index)
                    {
                        tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, tagStart, tagStart));
                        index = tagStart + 1;
                        continue;
                    }

                    int tagNameEnd = index - 1;

                    bool isColor = (index - tagNameStart == 5 &&
                                    string.Compare(source, tagNameStart, "color", 0, 5) == 0);
                    bool isSprite = (index - tagNameStart == 6 &&
                                     string.Compare(source, tagNameStart, "sprite", 0, 6) == 0);

                    if (!isColor && !isSprite)
                    {
                        index = tagStart;
                        continue;
                    }

                    while (index < length && char.IsWhiteSpace(source[index]))
                        index++;

                    if (index < length && source[index] == '=')
                    {
                        index++;
                        while (index < length && char.IsWhiteSpace(source[index]))
                            index++;
                    }

                    int valStart = -1, valEnd = -1;
                    if (index < length && source[index] == '"')
                    {
                        index++;
                        valStart = index;
                        int quoteClose = source.IndexOf('"', index);
                        if (quoteClose >= 0)
                        {
                            valEnd = quoteClose - 1;
                            index = quoteClose + 1;
                        }
                        else
                        {
                            tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, tagStart, tagStart));
                            index = tagStart + 1;
                            continue;
                        }
                    }
                    else
                    {
                        int vs = index;
                        while (index < length && source[index] != ']' && !char.IsWhiteSpace(source[index]))
                            index++;
                        if (vs < index)
                        {
                            valStart = vs;
                            valEnd = index - 1;
                        }
                    }

                    while (index < length && source[index] != ']') index++;
                    if (index < length && source[index] == ']') index++;

                    dfMarkupToken token = dfMarkupToken.Obtain(source, dfMarkupTokenType.StartTag, tagNameStart, tagNameEnd);
                    if (valStart != -1 && valEnd >= valStart)
                    {
                        dfMarkupToken attrToken = dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, valStart, valEnd);
                        token.AddAttribute(attrToken, attrToken);
                    }
                    tokens.Add(token);
                    continue;
                }

                if (c == '[' && index + 1 < length && source[index + 1] == '/')
                {
                    int tagStart = index;
                    index += 2;

                    int tagNameStart = index;
                    while (index < length && source[index] != ']')
                        index++;

                    if (tagNameStart >= index)
                    {
                        tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, tagStart, tagStart));
                        index = tagStart + 1;
                        continue;
                    }

                    int tagNameEnd = index - 1;
                    bool isColor = (index - tagNameStart == 5 &&
                                    string.Compare(source, tagNameStart, "color", 0, 5) == 0);
                    bool isSprite = (index - tagNameStart == 6 &&
                                     string.Compare(source, tagNameStart, "sprite", 0, 6) == 0);

                    if (!isColor && !isSprite)
                    {
                        index = tagStart;
                        continue;
                    }

                    if (index < length && source[index] == ']') index++;

                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.EndTag, tagNameStart, tagNameEnd));
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    int start = index;
                    if (c == '\r' && index + 1 < length && source[index + 1] == '\n')
                        index += 2;
                    else
                        index++;

                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Newline, start, index - 1));
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    int start = index;
                    while (index < length && char.IsWhiteSpace(source[index]) && source[index] != '\r' && source[index] != '\n')
                        index++;

                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Whitespace, start, index - 1));
                    continue;
                }

                if (config.OverrideDfTokenizer == AutoTranslateModule.OverrideDfTokenizerType.Chinese)
                {
                    int start = index;
                    if (c <= 0x7F && char.IsLetterOrDigit(c))
                    {
                        do { index++; } while (index < length && source[index] <= 0x7F && char.IsLetterOrDigit(source[index]));
                    }
                    else
                    {
                        index++;
                    }
                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, start, index - 1));
                    continue;
                }
                else if (config.OverrideDfTokenizer == AutoTranslateModule.OverrideDfTokenizerType.CustomRegex)
                {
                    if (DfTokenizerRegex != null)
                    {
                        Match m = DfTokenizerRegex.Match(source, index);
                        if (m.Success && m.Index == index)
                        {
                            int start = index;
                            index += m.Length;
                            tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, start, index - 1));
                            continue;
                        }
                    }
                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, index, index));
                    index++;
                    continue;
                }
                else
                {
                    tokens.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, index, index));
                    index++;
                    continue;
                }
            }

            return tokens;
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

        public List<string> ItemTipsTokenize(string text)
        {
            var tokens = new List<string>();
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                if (text[i] == '<')
                {
                    if (i + 6 < len && string.Compare(text, i, "<color=", 0, 7) == 0)
                    {
                        int endIdx = i + 7;
                        while (endIdx < len && text[endIdx] != '>') endIdx++;
                        if (endIdx < len && text[endIdx] == '>')
                        {
                            tokens.Add(text.Substring(i, endIdx - i + 1));
                            i = endIdx + 1;
                            continue;
                        }
                    }
                    else if (i + 7 < len && string.Compare(text, i, "</color>", 0, 8) == 0)
                    {
                        tokens.Add("</color>");
                        i += 8;
                        continue;
                    }

                    tokens.Add(text[i].ToString());
                    i++;
                    continue;
                }

                if (char.IsLetterOrDigit(text[i]))
                {
                    int start = i;
                    while (i < len && char.IsLetterOrDigit(text[i])) i++;
                    tokens.Add(text.Substring(start, i - start));
                    continue;
                }

                if (char.IsWhiteSpace(text[i]))
                {
                    int start = i;
                    while (i < len && char.IsWhiteSpace(text[i])) i++;
                    tokens.Add(text.Substring(start, i - start));
                    continue;
                }

                tokens.Add(text[i].ToString());
                i++;
            }

            return tokens;
        }

        public string WrapText(string text, out Vector2 resultSize)
        {
            string result = WrapTextWithTokenizer(text, out Vector2 sizeVector);
            resultSize = sizeVector;
            return result;
        }

        public string WrapTextWithTokenizer(string text, out Vector2 resultSize)
        {
            wrappedLines.Clear();

            string[] originalLines = text.Split(newLineSymbols, StringSplitOptions.None);
            int maxWidth = Mathf.RoundToInt(500 * config.ItemTipsFontScale * config.ItemTipsBackgroundWidthScale);

            foreach (string origLine in originalLines)
            {
                if (string.IsNullOrEmpty(origLine))
                {
                    wrappedLines.Add("");
                    continue;
                }

                IEnumerable tokens;
                bool isCustomRegex = config.OverrideItemTipsTokenizer == AutoTranslateModule.OverrideItemTipsTokenizerType.CustomRegex;
                if (!isCustomRegex)
                {
                    tokens = ItemTipsTokenize(origLine);
                    if (!(tokens as List<string>).Any())
                    {
                        wrappedLines.Add(origLine);
                        continue;
                    }
                }
                else
                {
                    tokens = ItemTipsModTokenizerRegex.Matches(origLine);
                    if ((tokens as MatchCollection).Count == 0)
                    {
                        wrappedLines.Add(origLine);
                        continue;
                    }
                }

                currentLine.Length = 0;

                foreach (var tokenObject in tokens)
                {
                    string token;
                    if (isCustomRegex)
                        token = (tokenObject as Match).Value;
                    else
                        token = tokenObject as string;

                    float tokenWidth = itemTipsModuleLabel.Backend.MeasureText(token, null, itemTipsModuleFont).x;
                    float currentWidth = itemTipsModuleLabel.Backend.MeasureText(currentLine.ToString(), null, itemTipsModuleFont).x;
                    float spaceWidth = itemTipsModuleLabel.Backend.MeasureText(" ", null, itemTipsModuleFont).x;

                    if (currentLine.Length > 0)
                    {
                        if (currentWidth + spaceWidth + tokenWidth > maxWidth)
                        {
                            wrappedLines.Add(currentLine.ToString());
                            currentLine.Length = 0;

                            if (tokenWidth > maxWidth)
                                SplitAndAddToken(token, itemTipsModuleLabel, itemTipsModuleFont, maxWidth, wrappedLines);
                            else
                                currentLine.Append(token);
                        }
                        else
                            currentLine.Append(token);
                    }
                    else
                    {
                        if (tokenWidth > maxWidth)
                            SplitAndAddToken(token, itemTipsModuleLabel, itemTipsModuleFont, maxWidth, wrappedLines);
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
                Vector2 lineSize = itemTipsModuleLabel.Backend.MeasureText(line, null, itemTipsModuleFont);
                overallWidth = Math.Max(overallWidth, lineSize.x);
                if (potentialItemTipsDynamicBaseFont != null && itemTipsModuleLabel.Font is Font labelFont && labelFont == potentialItemTipsDynamicBaseFont)
                    overallHeight += labelFont.fontSize * config.ItemTipsFontScale * config.ItemTipsLineHeightScale;
                else if (itemTipsModuleFont != null && itemTipsModuleLabel.Font is Font labelFont2 && labelFont2 == itemTipsModuleFont)
                    overallHeight += 1.1f * originalLineHeight * config.ItemTipsFontScale * config.ItemTipsLineHeightScale;
            }
            resultSize = new Vector2(overallWidth, overallHeight);

            return string.Join("\n", wrappedLines.ToArray());
        }

        private static void SplitAndAddToken(string token, SLabel sLabel, Font font, int maxWidth, List<string> wrappedLines)
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

        internal void ItemTipsReposition(SLabel label)
        {
            if (label.Root != null)
            {
                label.Position.x = label.Root.Size.x * itemTipsAnchor.x - label.Size.x * itemTipsPivot.x;
                label.Position.y = label.Root.Size.y * itemTipsAnchor.y - label.Size.y * itemTipsPivot.y;
            }
        }

        public static Font GetFontFromdfFont(dfFont font, float scale, int baseLine)
        {
            Font font2 = new Font(font.name);
            font2.material = new Material(GUI.skin.font.material);
            font2.material.mainTexture = font.Texture;
            font2.material.mainTexture.wrapMode = TextureWrapMode.Repeat;
            CharacterInfo[] array = new CharacterInfo[font.Glyphs.Count];
            for (int i = 0; i < array.Length; i++)
            {
                CharacterInfo characterInfo = default(CharacterInfo);
                dfFont.GlyphDefinition glyphDefinition = font.Glyphs[i];
                dfAtlas.ItemInfo itemInfo = font.Atlas[font.Sprite];
                characterInfo.glyphWidth = Mathf.RoundToInt(glyphDefinition.width * scale);
                characterInfo.glyphHeight = Mathf.RoundToInt(glyphDefinition.height * scale);
                characterInfo.size = 0;
                characterInfo.index = glyphDefinition.id;
                float num = itemInfo.region.x + (float)glyphDefinition.x * font2.material.mainTexture.texelSize.x;
                float x = num + (float)glyphDefinition.width * font2.material.mainTexture.texelSize.x;
                float num2 = itemInfo.region.yMax - (float)glyphDefinition.y * font2.material.mainTexture.texelSize.y;
                float y = num2 - (float)glyphDefinition.height * font2.material.mainTexture.texelSize.y;
                characterInfo.uvTopLeft = new Vector2(num, num2);
                characterInfo.uvTopRight = new Vector2(x, num2);
                characterInfo.uvBottomLeft = new Vector2(num, y);
                characterInfo.uvBottomRight = new Vector2(x, y);
                characterInfo.advance = Mathf.RoundToInt(glyphDefinition.xadvance * scale);
                characterInfo.minY = Mathf.RoundToInt((baseLine - glyphDefinition.yoffset - glyphDefinition.height) * scale);
                characterInfo.maxY = Mathf.RoundToInt((baseLine - glyphDefinition.yoffset) * scale);
                array[i] = characterInfo;
            }
            font2.characterInfo = array;
            return font2;
        }
    }
}
