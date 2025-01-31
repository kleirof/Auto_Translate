using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AutoTranslate
{
    public class FontManager
    {
        public string regexForTokenizer;
        public AssetBundle assetBundle;
        public dfFontBase dfFontBase;
        public tk2dFontData tk2dFont;
        public AutoTranslateModule.OverrideFontType overrideFont;

        public bool customFontLoaded = false;

        public static string defaultRegexForTokenizer = @"(?<StartTag>\[(?<TagName>(color|sprite))\s*(?:""(?<AttributeValue>[^""]*)"")?\])|(?<EndTag>\[/\k<TagName>\])|(?<Newline>\r?\n)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.)";

        public FontManager(AutoTranslateModule.OverrideFontType fontType, string assetBundleName, string dfFontName, string tk2dFontName, string regex)
        {
            regexForTokenizer = regex == string.Empty ? defaultRegexForTokenizer : regex;
            overrideFont = fontType;
            if (fontType != AutoTranslateModule.OverrideFontType.Custom)
            {
                if (fontType == AutoTranslateModule.OverrideFontType.None || fontType == AutoTranslateModule.OverrideFontType.English || fontType == AutoTranslateModule.OverrideFontType.Polish)
                    return;
                else if (fontType == AutoTranslateModule.OverrideFontType.Chinese)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/SimSun12_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/SimSun12_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (fontType == AutoTranslateModule.OverrideFontType.Japanese)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/JackeyFont12_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/JackeyFont_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (fontType == AutoTranslateModule.OverrideFontType.Korean)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/NanumGothic16_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/NanumGothic16TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                else if (fontType == AutoTranslateModule.OverrideFontType.Russian)
                {
                    dfFontBase = (ResourceCache.Acquire("Alternate Fonts/PixelaCYR_15_DF") as GameObject).GetComponent<dfFont>();
                    tk2dFont = (ResourceCache.Acquire("Alternate Fonts/PixelaCYR_15_TK2D") as GameObject).GetComponent<tk2dFont>().data;
                }
                return;
            }

            if (assetBundleName == string.Empty || (dfFontName == string.Empty && tk2dFontName == string.Empty))
                return;

            string assetBundlePath = Path.Combine(ETGMod.FolderPath(AutoTranslateModule.instance), assetBundleName);
            try
            {
                assetBundle = AssetBundleLoader.LoadAssetBundle(assetBundlePath);
                dfFontBase = assetBundle.LoadAsset<GameObject>(dfFontName).GetComponent<dfFontBase>();
                tk2dFont = assetBundle.LoadAsset<GameObject>(tk2dFontName).GetComponent<tk2dFont>().data;
            }
            catch { }

            customFontLoaded = true;
        }

        private int estimateTokenCount(string source)
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
            dfList.EnsureCapacity(this.estimateTokenCount(source));
            dfList.AutoReleaseItems = true;

            Regex regex = new Regex(regexForTokenizer, RegexOptions.Compiled | RegexOptions.Multiline);
            MatchCollection matches = regex.Matches(source);

            foreach (Match match in matches)
            {
                if (match.Groups["StartTag"].Success)
                {
                    Capture tagName = match.Groups["TagName"];
                    Capture attributeValue = match.Groups["AttributeValue"];

                    dfMarkupToken token = dfMarkupToken.Obtain(source, dfMarkupTokenType.StartTag, tagName.Index, tagName.Index + tagName.Length - 1);

                    if (attributeValue.Value != string.Empty)
                    {
                        dfMarkupToken attributeToken = dfMarkupToken.Obtain(source, dfMarkupTokenType.Text, attributeValue.Index, attributeValue.Index + attributeValue.Length - 1);

                        token.AddAttribute(attributeToken, attributeToken);
                    }
                    dfList.Add(token);
                }
                else if (match.Groups["EndTag"].Success)
                {
                    dfList.Add(dfMarkupToken.Obtain(source, dfMarkupTokenType.EndTag, match.Index, match.Index + match.Length - 1));
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
    }
}
