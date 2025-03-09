using System;
using UnityEngine;

namespace AutoTranslate
{
    public class AutoTranslateConfig
    {
        internal AutoTranslateModule.TranslationAPIType TranslationAPI;
        internal KeyCode ToggleTranslationKeyBinding;
        internal string RegexForFullTextNeedToTranslate;
        internal string RegexForEachLineNeedToTranslate;
        internal string RegexForIgnoredSubstringWithinText;
        internal int RequestBatchSize;
        internal int MaxRetryCount;
        internal int TranslationCacheCapacity;
        internal string PresetTranslations;
        internal bool LogRequestedTexts;

        internal AutoTranslateModule.OverrideFontType OverrideFont;
        internal string FontAssetBundleName;
        internal string CustomDfFontName;
        internal string CustomTk2dFontName;
        internal string RegexForDfTokenizer;
        internal float DfTextScaleExpandThreshold;
        internal float DfTextScaleExpandToValue;

        internal bool ShowRequestedCharacterCount;
        internal int RequestedCharacterCountAlertThreshold;
        internal KeyCode ToggleRequestedCharacterCountKeyBinding;
        internal string CountLabelAnchor;
        internal string CountLabelPivot;

        internal string RegexForItemTipsModTokenizer;

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

        internal string LargeModelBaseUrl;
        internal string LargeModelApiKey;
        internal string LargeModelName;
        internal string LargeModelPrompt;
        internal int LargeModelMaxTokens;
        internal float LargeModelTemperature;
        internal float LargeModelTopP;
        internal float LargeModelFrequencyPenalty;

        internal bool CheckConfigValues()
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
                case AutoTranslateModule.TranslationAPIType.LargeModel:
                    if (IsNullOrEmptyString(LargeModelBaseUrl))
                        return false;
                    if (IsNullOrEmptyString(LargeModelApiKey))
                        return false;
                    if (IsNullOrEmptyString(LargeModelBaseUrl))
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
