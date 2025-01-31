using System;

namespace AutoTranslate
{
    public class TranslationManagerConfig
    {
        internal AutoTranslateModule.TranslationAPIType TranslationAPI;
        internal string RegexForFullTextNeedToTranslate;
        internal string RegexForEachLineNeedToTranslate;
        internal string RegexForIgnoredSubstringWithinText;
        internal int RequestBatchSize;
        internal int MaxRetryCount;
        internal int TranslationCacheCapacity;
        internal string PresetTranslations;
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
            }
            return true;
        }

        private static bool IsNullOrEmptyString(string str)
        {
            return str == null || str == string.Empty;
        }
    }
}
