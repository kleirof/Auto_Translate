using System;
using UnityEngine;

namespace AutoTranslate
{
    public class AutoTranslateConfig
    {
        internal AutoTranslateModule.TranslationAPIType TranslationAPI;
        internal KeyCode ToggleTranslationKeyBinding;
        internal AutoTranslateModule.FilterForFullTextNeedToTranslateType FilterForFullTextNeedToTranslate;
        internal string RegexForFullTextNeedToTranslate;
        internal AutoTranslateModule.FilterForEachLineNeedToTranslateType FilterForEachLineNeedToTranslate;
        internal string RegexForEachLineNeedToTranslate;
        internal AutoTranslateModule.FilterForIgnoredSubstringWithinTextType FilterForIgnoredSubstringWithinText;
        internal string RegexForIgnoredSubstringWithinText;
        internal int MaxBatchCharacterCount;
        internal int MaxBatchTextCount;
        internal int MaxRetryCount;
        internal float RetryInterval;
        internal int TranslationCacheCapacity;
        internal string PresetTranslations;
        internal string CachedTranslations;
        internal bool AutoSaveCachedTranslationsUponQuit;
        internal bool LogRequestedTexts;

        internal AutoTranslateModule.OverrideFontType OverrideFont;
        internal string FontAssetBundleName;
        internal string CustomDfFontName;
        internal string CustomTk2dFontName;
        internal AutoTranslateModule.OverrideDfTokenizerType OverrideDfTokenizer;
        internal string RegexForDfTokenizer;
        internal float DfTextScaleExpandThreshold;
        internal float DfTextScaleExpandToValue;

        internal bool ShowRequestedCharacterCount;
        internal int RequestedCharacterCountAlertThreshold;
        internal KeyCode ToggleRequestedCharacterCountKeyBinding;
        internal string CountLabelAnchor;
        internal string CountLabelPivot;

        internal bool TranslateTextsOfItemTipsMod;
        internal AutoTranslateModule.OverrideItemTipsTokenizerType OverrideItemTipsTokenizer;
        internal string RegexForItemTipsModTokenizer;
        internal float ItemTipsFontScale;
        internal float ItemTipsBackgroundWidthScale;
        internal float ItemTipsLineHeightScale;
        internal string ItemTipsAnchor;
        internal string ItemTipsPivot;
        internal int ItemTipsSourceBitmapFontBaseLine;

        internal string TencentSecretId;
        internal string TencentSecretKey;
        internal string TencentSourceLanguage;
        internal string TencentTargetLanguage;
        internal string TencentRegion;

        internal string BaiduAppId;
        internal string BaiduSecretKey;
        internal string BaiduSourceLanguage;
        internal string BaiduTargetLanguage;

        internal string AzureSubscriptionKey;
        internal string AzureSourceLanguage;
        internal string AzureTargetLanguage;
        internal string AzureRegion;

        internal string LlmBaseUrl;
        internal string LlmApiKey;
        internal string LlmName;
        internal string LlmPrompt;
        internal int LlmMaxTokens;
        internal float LlmTemperature;
        internal float LlmTopP;
        internal int LlmTopK;
        internal float LlmFrequencyPenalty;
        internal string LlmExtraParametersJson;
        internal AutoTranslateModule.LlmQuotePreprocessType LlmQuotePreprocess;
        internal AutoTranslateModule.LlmDataFormatType LlmDataFormat;
        internal string LlmSplitText;
        internal string LlmPositionText;
        internal string LlmSegmentText;

        internal bool isConfigValid;

        internal void CheckConfig()
        {
            isConfigValid = IsConfigValid();
        }

        internal bool IsConfigValid()
        {
            if (!Enum.IsDefined(typeof(AutoTranslateModule.TranslationAPIType), TranslationAPI))
                return false;
            switch (TranslationAPI)
            {
                case AutoTranslateModule.TranslationAPIType.Tencent:
                    if (IsNullOrEmptyString(TencentSecretId))
                        return false;
                    if (IsNullOrEmptyString(TencentSecretKey))
                        return false;
                    break;
                case AutoTranslateModule.TranslationAPIType.Baidu:
                    if (IsNullOrEmptyString(BaiduAppId))
                        return false;
                    if (IsNullOrEmptyString(BaiduSecretKey))
                        return false;
                    break;
                case AutoTranslateModule.TranslationAPIType.Azure:
                    if (IsNullOrEmptyString(AzureSubscriptionKey))
                        return false;
                    break;
                case AutoTranslateModule.TranslationAPIType.Llm:
                    if (IsNullOrEmptyString(LlmBaseUrl))
                        return false;
                    break;
            }
            return true;
        }

        private static bool IsNullOrEmptyString(string str)
        {
            return str == null || str == string.Empty;
        }
    }
}
