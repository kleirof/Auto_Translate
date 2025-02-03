using BepInEx;
using UnityEngine;
using HarmonyLib;
using BepInEx.Bootstrap;
using System;
using System.Reflection;
using BepInEx.Configuration;

namespace AutoTranslate
{
    [BepInDependency("etgmodding.etg.mtgapi")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class AutoTranslateModule : BaseUnityPlugin
    {
        public const string GUID = "kleirof.etg.autotranslate";
        public const string NAME = "Auto Translate";
        public const string VERSION = "1.0.5";
        public const string TEXT_COLOR = "#AA3399";

        public static AutoTranslateModule instance;

        private ConfigEntry<bool> AcceptedModDeclaration;
        private ConfigEntry<TranslationAPIType> TranslationAPI;
        private ConfigEntry<string> RegexForFullTextNeedToTranslate;
        private ConfigEntry<string> RegexForEachLineNeedToTranslate;
        private ConfigEntry<string> RegexForIgnoredSubstringWithinText;
        private ConfigEntry<int> RequestBatchSize;
        private ConfigEntry<int> MaxRetryCount;
        private ConfigEntry<int> TranslationCacheCapacity;
        private ConfigEntry<bool> TranslateTextsOfItemTipsMod;
        private ConfigEntry<string> PresetTranslations;
        private ConfigEntry<OverrideFontType> OverrideFont;
        private ConfigEntry<string> FontAssetBundleName;
        private ConfigEntry<string> CustomDfFontName;
        private ConfigEntry<string> CustomTk2dFontName;
        private ConfigEntry<string> RegexForDfTokenizer;
        private ConfigEntry<bool> LogRequestedTexts;
        private ConfigEntry<string> TencentSecretId;
        private ConfigEntry<string> TencentSecretKey;
        private ConfigEntry<string> TencentSourceLanguage;
        private ConfigEntry<string> TencentTargetLanguage;
        private ConfigEntry<string> TencentRegion;
        private ConfigEntry<string> BaiduAppId;
        private ConfigEntry<string> BaiduSecretKey;
        private ConfigEntry<string> BaiduSourceLanguage;
        private ConfigEntry<string> BaiduTargetLanguage;
        private ConfigEntry<string> AzureSubscriptionKey;
        private ConfigEntry<string> AzureSourceLanguage;
        private ConfigEntry<string> AzureTargetLanguage;
        private ConfigEntry<string> AzureRegion;

        private Harmony harmony;

        private bool isConfigValid;

        private GameObject autoTranslateObject;
        internal TranslationManager translateManager;
        internal Type itemTipsModuleType;

        internal FontManager fontManager;

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
            instance = this;

            TranslationManagerConfig config = InitializeConfigs();
            if (!AcceptedModDeclaration.Value)
            {
                Debug.LogError("AutoTranslate: 还未接受Mod声明！Mod declaration not accepted!");
                return;
            }
            else if (!isConfigValid)
            {
                Debug.LogError("AutoTranslate: 翻译配置无效！Invalid Translate Config!");
                return;
            }

            fontManager = new FontManager(OverrideFont.Value, FontAssetBundleName.Value, CustomDfFontName.Value, CustomTk2dFontName.Value, RegexForDfTokenizer.Value);

            harmony = new Harmony(GUID);
            harmony.PatchAll();

            autoTranslateObject = new GameObject("Auto Translate Object");
            DontDestroyOnLoad(autoTranslateObject);
            translateManager = autoTranslateObject.AddComponent<TranslationManager>();

            translateManager.Initialize(config);

            DoOptionalPatches();
        }

        internal static void Log(string text, string color = "FFFFFF")
        {
            ETGModConsole.Log($"<color={color}>{text}</color>");
        }

        internal void GMStart(GameManager g)
        {
            if (!AcceptedModDeclaration.Value)
            {
                Log($"{NAME} v{VERSION} started, but AcceptedModDeclaration not checked!", "#FF0000");
                Log($"(Important!) Please read the declaration on mod website and then check the config in mod manager or manually edit it.", "#FF0000");
                return;
            }
            if (!isConfigValid)
            {
                Log($"{NAME} v{VERSION} started, but config invalid!", "#FF0000");
                Log($"Please check the config in mod manager or manually edit it.", "#FF0000");
                return;
            }
            else
                Log($"{NAME} v{VERSION} started successfully.", TEXT_COLOR);


            fontManager?.InitializeFontAfterGameManager(OverrideFont.Value);
        }

        private TranslationManagerConfig InitializeConfigs()
        {
            AcceptedModDeclaration = Config.Bind(
                "1.General",
                "AcceptedModDeclaration",
                false,
                "作为Mod用户，我已阅读并同意网站上此mod的声明，清楚潜在费用的去向并信任此mod的行为，并对自己的选择负责任。As a mod user, I have read and accepted the declaration on this mod's website, understand the potential destination of the fees, trust the actions of this mod, and take responsibility for my choice."
                );

            TranslationAPI = Config.Bind(
                "1.General",
                "TranslationAPI",
                TranslationAPIType.Tencent,
                "选择使用的翻译API。Choose the translation API to use."
                );

            RegexForFullTextNeedToTranslate = Config.Bind(
                "1.General",
                "RegexForFullTextNeedToTranslate",
                @"^(?!Enter the Gungeon).*$",
                "正则表达式，一个多行文本若匹配为真，则这个多行文本保留以待翻译。用来筛选待翻译的文本以节省翻译额度。Regular expression, if a multiple line text matches true, then this multiple line text is retained for translation. Used to filter the text to be translated to save translation quotas."
                );

            RegexForEachLineNeedToTranslate = Config.Bind(
                "1.General",
                "RegexForEachLineNeedToTranslate",
                @"^(?![@#])(?=\S)(?!^[\d\p{P}]+$)(?!.*[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]).*$",
                "正则表达式，多行文本若存在一行匹配，整个多行文本保留以待翻译。用来筛选待翻译的文本以节省翻译额度。Regular expression, if there is a matching line in multiple lines of text, the entire multiple lines of text are retained for translation. Used to filter the text to be translated to save translation quotas."
                );

            RegexForIgnoredSubstringWithinText = Config.Bind(
                "1.General",
                "RegexForIgnoredSubstringWithinText",
                @"(?:\[[^\]]*\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)",
                "正则表达式，匹配文本中需要忽略的子文本。请使用非捕获组。这通常包括一些要特殊处理的贴图和转义符。Regular expression, matching sub texts that need to be ignored in the text. Please use non capture groups. This usually includes some textures and escape characters that require special handling."
                );

            RequestBatchSize = Config.Bind(
                "1.General",
                "RequestBatchSize",
                1024,
                "发送请求的批量数据总大小。若翻译api提示单次请求过长，请减小此值。The total size of batch data for sending requests. If the translation API prompts that a single request is too long, please reduce this value."
                );

            MaxRetryCount = Config.Bind(
                "1.General",
                "MaxRetryCount",
                3,
                "发生错误时的最大重试次数。The maximum number of retries when an error occurs."
                );

            TranslationCacheCapacity = Config.Bind(
                "1.General",
                "TranslationCacheCapacity",
                1024,
                "最大翻译缓存容量。Maximum translation cache capacity."
                );

            TranslateTextsOfItemTipsMod = Config.Bind(
                "1.General",
                "TranslateTextsOfItemTipsMod",
                true,
                "翻译ItemTipsMod中的文本。Translate the text in ItemTipsMod."
                );

            PresetTranslations = Config.Bind(
                "1.General",
                "PresetTranslations",
                "PresetTranslations.json",
                "预设翻译的文件名。使用预设翻译以减少加载时常见文本的翻译请求，留空表示不使用。预设翻译为位于dll同目录下的json文件。The file name for the preset translation. Use preset translation to reduce translation requests for common text during loading, leaving blank to indicate not using. The preset translation is a JSON file located in the same directory as the DLL."
                );

            OverrideFont = Config.Bind(
                "1.General",
                "OverrideFont",
                OverrideFontType.Chinese,
                "用来覆盖游戏字体的字体。根据你需要的目标语言选择。Font used to override the font of the game. Choose according to the target language you need."
                );

            FontAssetBundleName = Config.Bind(
                "1.General",
                "FontAssetBundleName",
                "",
                "包含自定义字体的AssetBundle名称。位于dll同目录下。AssetBundle name containing custom fonts. Located in the same directory as DLL."
                );

            CustomDfFontName = Config.Bind(
                "1.General",
                "CustomDfFontName",
                "",
                "要使用的自定义df字体。请把它包含于FontAssetBundle。The custom df font to be used. Please include it in FontAssetBundle."
                );

            CustomTk2dFontName = Config.Bind(
                "1.General",
                "CustomTk2dFontName",
                "",
                "要使用的自定义tk2d字体。请把它包含于FontAssetBundle。The custom tk2d font to be used. Please include it in FontAssetBundle."
                );

            RegexForDfTokenizer = Config.Bind(
                "1.General",
                "RegexForDfTokenizer",
                FontManager.defaultRegexForTokenizer,
                "用于df生成token的正则表达式。token用于处理文本的自动换行位置。如每个字换行还是单词后换行。参考默认样例填写。建议只修改Text相关内容。A regular expression used for generating tokens from df. Token is used to handle the automatic line break position of text. Whether to wrap each word or to wrap after each word. Fill in according to the default example. Suggest only modifying content related to Text."
                );

            LogRequestedTexts = Config.Bind(
                "1.General",
                "LogRequestedTexts",
                false,
                "在日志中显示请求翻译的文本。Log the text requested for translation."
                );

            TencentSecretId = Config.Bind(
                "2.TencentTranslation",
                "SecretId",
                "",
                "腾讯翻译的SecretId。The SecretId of Tencent Translate."
                );

            TencentSecretKey = Config.Bind(
                "2.TencentTranslation",
                "SecretKey",
                "",
                "腾讯翻译的SecretKey。The SecretKey of Tencent Translate."
                );

            TencentSourceLanguage = Config.Bind(
                "2.TencentTranslation",
                "SourceLanguage",
                "en",
                "腾讯翻译的源语言，如en。The source language of Tencent Translate, such as en."
                );

            TencentTargetLanguage = Config.Bind(
                "2.TencentTranslation",
                "TargetLanguage",
                "zh",
                "腾讯翻译的目标语言，如zh。Tencent Translate's target language, such as zh."
                );

            TencentRegion = Config.Bind(
                "2.TencentTranslation",
                "Region",
                "ap-beijing",
                "地域，用来标识希望连接哪个地域的服务器，如ap-beijing。Region, used to identify which region's server you want to connect to, such as ap-beijing."
                );

            BaiduAppId = Config.Bind(
                "3.BaiduTranslate",
                "SecretId",
                "",
                "百度翻译的AppId。The AppId of Baidu Translate."
                );

            BaiduSecretKey = Config.Bind(
                "3.BaiduTranslate",
                "SecretKey",
                "",
                "百度翻译的SecretKey。The SecretKey for Baidu Translate."
                );

            BaiduSourceLanguage = Config.Bind(
                "3.BaiduTranslate",
                "SourceLanguage",
                "en",
                "百度翻译的源语言，如en。The source language of Baidu Translate, such as en."
                );

            BaiduTargetLanguage = Config.Bind(
                "3.BaiduTranslate",
                "TargetLanguage",
                "zh",
                "百度翻译的目标语言，如zh。The target language for Baidu Translate, such as zh."
                );

            AzureSubscriptionKey = Config.Bind(
                "4.AzureTranslate",
                "AzureSubscriptionKey",
                "",
                "Azure翻译的SubscriptionKey。Subscription Key for Azure Translation."
                );

            AzureSourceLanguage = Config.Bind(
                "4.AzureTranslate",
                "SourceLanguage",
                "en",
                "Azure翻译的源语言，如en。The source language for Azure translation, such as en."
                );

            AzureTargetLanguage = Config.Bind(
                "4.AzureTranslate",
                "TargetLanguage",
                "zh-Hans",
                "Azure翻译的目标语言，如zh-Hans。The target language for Azure translation, such as zh-Hans."
                );

            AzureRegion = Config.Bind(
                "4.AzureTranslate",
                "AzureRegion",
                "global",
                "地域，用来标识希望连接哪个地域的服务器，如global。Region, used to identify which region's server you want to connect to, such as global."
                );

            TranslationManagerConfig config = new TranslationManagerConfig
            {
                TranslationAPI = TranslationAPI.Value,
                RegexForFullTextNeedToTranslate = RegexForFullTextNeedToTranslate.Value,
                RegexForEachLineNeedToTranslate = RegexForEachLineNeedToTranslate.Value,
                RegexForIgnoredSubstringWithinText = RegexForIgnoredSubstringWithinText.Value,
                RequestBatchSize = RequestBatchSize.Value,
                MaxRetryCount = MaxRetryCount.Value,
                TranslationCacheCapacity = TranslationCacheCapacity.Value,
                PresetTranslations = PresetTranslations.Value,
                LogRequestedTexts = LogRequestedTexts.Value,
                TencentSecretId = TencentSecretId.Value,
                TencentSecretKey = TencentSecretKey.Value,
                TencentSourceLanguage = TencentSourceLanguage.Value,
                TencentTargetLanguage = TencentTargetLanguage.Value,
                TencentRegion = TencentRegion.Value,
                BaiduAppId = BaiduAppId.Value,
                BaiduSecretKey = BaiduSecretKey.Value,
                BaiduSourceLanguage = BaiduSourceLanguage.Value,
                BaiduTargetLanguage = BaiduTargetLanguage.Value,
                AzureSubscriptionKey = AzureSubscriptionKey.Value,
                AzureSourceLanguage = AzureSourceLanguage.Value,
                AzureTargetLanguage = AzureTargetLanguage.Value,
                AzureRegion = AzureRegion.Value,
            };

            isConfigValid = config.CheckConfigValues();
            return config;
        }

        private void DoOptionalPatches()
        {
            if (Chainloader.PluginInfos.TryGetValue("glorfindel.etg.itemtips", out PluginInfo pluginInfo) && TranslateTextsOfItemTipsMod.Value)
            {
                itemTipsModuleType = AccessTools.TypeByName("ItemTipsMod.ItemTipsModule");
                MethodInfo methodInfo1 = AccessTools.Method(itemTipsModuleType, "ShowTip", null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.ShowTipPatchClass), nameof(AutoTranslatePatches.ShowTipPatchClass.ShowTipPostfix), null, null);
                harmony.Patch(methodInfo1, null, new HarmonyMethod(fixMethodInfo1), null, null, null);

                MethodInfo methodInfo2 = AccessTools.Method(itemTipsModuleType, "LoadFont", null, null);
                MethodInfo fixMethodInfo2 = AccessTools.Method(typeof(AutoTranslatePatches.LoadFontPatchClass), nameof(AutoTranslatePatches.LoadFontPatchClass.LoadFontPrefix), null, null);
                harmony.Patch(methodInfo2, new HarmonyMethod(fixMethodInfo2), null, null, null, null);
            }

            if (fontManager.dfFontBase != null && fontManager.dfFontBase is dfDynamicFont)
            {
                Type dynamicFontRendererType = typeof(dfDynamicFont.DynamicFontRenderer);
                MethodInfo methodInfo1 = AccessTools.Method(dynamicFontRendererType, nameof(dfDynamicFont.DynamicFontRenderer.tokenize), null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.DynamicFontRendererTokenizePatchClass), nameof(AutoTranslatePatches.DynamicFontRendererTokenizePatchClass.TokenizePrefix), null, null);
                harmony.Patch(methodInfo1, new HarmonyMethod(fixMethodInfo1), null, null, null, null);

                MethodInfo methodInfo2 = AccessTools.Method(dynamicFontRendererType, nameof(dfDynamicFont.DynamicFontRenderer.calculateLinebreaks), null, null);
                MethodInfo fixMethodInfo2 = AccessTools.Method(typeof(AutoTranslatePatches.DynamicFontRendererCalculateLinebreaksPatchClass), nameof(AutoTranslatePatches.DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatch), null, null);
                harmony.Patch(methodInfo2, null, null, null, null, new HarmonyMethod(fixMethodInfo2));
            }
            else if (fontManager.dfFontBase != null && fontManager.dfFontBase is dfFont)
            {
                Type BitmappedFontRendererType = typeof(dfFont.BitmappedFontRenderer);
                MethodInfo methodInfo1 = AccessTools.Method(BitmappedFontRendererType, nameof(dfFont.BitmappedFontRenderer.tokenize), null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.BitmappedFontRendererTokenizePatchClass), nameof(AutoTranslatePatches.BitmappedFontRendererTokenizePatchClass.TokenizePrefix), null, null);
                harmony.Patch(methodInfo1, new HarmonyMethod(fixMethodInfo1), null, null, null, null);

                MethodInfo methodInfo2 = AccessTools.Method(BitmappedFontRendererType, nameof(dfFont.BitmappedFontRenderer.calculateLinebreaks), null, null);
                MethodInfo fixMethodInfo2 = AccessTools.Method(typeof(AutoTranslatePatches.BitmappedFontRendererCalculateLinebreaksPatchClass), nameof(AutoTranslatePatches.BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatch), null, null);
                harmony.Patch(methodInfo2, null, null, null, null, new HarmonyMethod(fixMethodInfo2));
            }
        }

        public enum TranslationAPIType
        {
            Tencent,
            Baidu,
            Azure,
        }

        public enum OverrideFontType
        {
            None,
            Chinese,
            English,
            Japanese,
            Korean,
            Russian,
            Polish,
            Custom,
        }
    }
}
