using BepInEx;
using UnityEngine;
using HarmonyLib;
using BepInEx.Bootstrap;
using System;
using System.Reflection;
using BepInEx.Configuration;
using System.IO;

namespace AutoTranslate
{
    [BepInDependency("etgmodding.etg.mtgapi")]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class AutoTranslateModule : BaseUnityPlugin
    {
        public const string GUID = "kleirof.etg.autotranslate";
        public const string NAME = "Auto Translate";
        public const string VERSION = "1.2.0";
        public const string TEXT_COLOR = "#AA3399";

        internal static AutoTranslateModule instance;

        private ConfigEntry<bool> AcceptedModDeclaration;
        private ConfigEntry<TranslationAPIType> TranslationAPI;
        private ConfigEntry<KeyCode> ToggleTranslationKeyBinding;
        private ConfigEntry<FilterForFullTextNeedToTranslateType> FilterForFullTextNeedToTranslate;
        private ConfigEntry<string> RegexForFullTextNeedToTranslate;
        private ConfigEntry<FilterForEachLineNeedToTranslateType> FilterForEachLineNeedToTranslate;
        private ConfigEntry<string> RegexForEachLineNeedToTranslate;
        private ConfigEntry<FilterForIgnoredSubstringWithinTextType> FilterForIgnoredSubstringWithinText;
        private ConfigEntry<string> RegexForIgnoredSubstringWithinText;
        private ConfigEntry<int> MaxBatchCharacterCount;
        private ConfigEntry<int> MaxBatchTextCount;
        private ConfigEntry<int> MaxRetryCount;
        private ConfigEntry<float> RetryInterval;
        private ConfigEntry<int> TranslationCacheCapacity;
        private ConfigEntry<string> PresetTranslations;
        private ConfigEntry<string> CachedTranslations;
        private ConfigEntry<bool> AutoSaveCachedTranslationsUponQuit;
        private ConfigEntry<bool> LogRequestedTexts;
        private ConfigEntry<bool> TranslateTextFromDfLabel;
        private ConfigEntry<bool> TranslateTextFromDfButton;
        private ConfigEntry<bool> TranslateTextFromTk2dTextMesh;

        private ConfigEntry<OverrideFontType> OverrideFont;
        private ConfigEntry<string> FontAssetBundleName;
        private ConfigEntry<string> CustomDfFontName;
        private ConfigEntry<string> CustomTk2dFontName;
        private ConfigEntry<string> RegexForDfTokenizer;
        private ConfigEntry<OverrideDfTokenizerType> OverrideDfTokenizer;
        private ConfigEntry<float> DfTextScaleExpandThreshold;
        private ConfigEntry<float> DfTextScaleExpandToValue;

        private ConfigEntry<bool> ShowRequestedCharacterCount;
        private ConfigEntry<int> RequestedCharacterCountAlertThreshold;
        private ConfigEntry<KeyCode> ToggleRequestedCharacterCountKeyBinding;
        private ConfigEntry<string> CountLabelAnchor;
        private ConfigEntry<string> CountLabelPivot;

        private ConfigEntry<bool> TranslateTextsOfItemTipsMod;
        private ConfigEntry<OverrideItemTipsTokenizerType> OverrideItemTipsTokenizer;
        private ConfigEntry<string> RegexForItemTipsModTokenizer;
        private ConfigEntry<float> ItemTipsFontScale;
        private ConfigEntry<float> ItemTipsBackgroundWidthScale;
        private ConfigEntry<float> ItemTipsLineHeightScale;
        private ConfigEntry<string> ItemTipsAnchor;
        private ConfigEntry<string> ItemTipsPivot;
        private ConfigEntry<int> ItemTipsSourceBitmapFontBaseLine;

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

        private ConfigEntry<string> LlmBaseUrl;
        private ConfigEntry<string> LlmApiKey;
        private ConfigEntry<string> LlmName;
        private ConfigEntry<string> LlmPrompt;
        private ConfigEntry<int> LlmMaxTokens;
        private ConfigEntry<float> LlmTemperature;
        private ConfigEntry<float> LlmTopP;
        private ConfigEntry<int> LlmTopK;
        private ConfigEntry<float> LlmFrequencyPenalty;
        private ConfigEntry<string> LlmExtraParametersJson;
        private ConfigEntry<LlmQuotePreprocessType> LlmQuotePreprocess;
        private ConfigEntry<LlmDataFormatType> LlmDataFormat;
        private ConfigEntry<string> LlmSplitText;
        private ConfigEntry<string> LlmPositionText;
        private ConfigEntry<string> LlmSegmentText;

        private Harmony harmony;

        private GameObject autoTranslateObject;
        internal TranslationManager translateManager;

        internal FontManager fontManager;
        internal AutoTranslateConfig config;
        internal StatusLabelController statusLabel;

        private readonly string errorColor = "#FF0000";

        public void Start()
        {
            ETGModMainBehaviour.WaitForGameManagerStart(GMStart);
            instance = this;

            config = InitializeConfigs();
            if (!AcceptedModDeclaration.Value)
            {
                Debug.LogError("AutoTranslate: 还未接受Mod声明！Mod declaration not accepted!");
                return;
            }
            else if (!config.isConfigValid)
            {
                Debug.LogError("AutoTranslate: 翻译配置无效！Invalid Translate Config!");
            }

            fontManager = new FontManager();

            harmony = new Harmony(GUID);
            harmony.PatchAll();

            autoTranslateObject = new GameObject("Auto Translate Object");
            DontDestroyOnLoad(autoTranslateObject);
            translateManager = autoTranslateObject.AddComponent<TranslationManager>();
            translateManager.Initialize();

            DoOptionalPatches();

            statusLabel = new StatusLabelController();
        }

        internal static void Log(string text, string color = "FFFFFF")
        {
            ETGModConsole.Log($"<color={color}>{text}</color>");
        }

        internal void GMStart(GameManager g)
        {
            if (!AcceptedModDeclaration.Value)
            {
                Log($"{NAME} v{VERSION} started, but AcceptedModDeclaration not checked!", errorColor);
                Log($"(Important!) Please read the declaration on mod website and then check the config in mod manager or manually edit it.", errorColor);
                return;
            }
            if (!config.isConfigValid)
            {
                Log($"{NAME} v{VERSION} started, but config is invalid!", errorColor);
                Log($"Please check the config in mod manager or manually edit it.", errorColor);
                return;
            }
            else
                Log($"{NAME} v{VERSION} started successfully.", TEXT_COLOR);
            Log($"   Translate Api: {config.TranslationAPI}", TEXT_COLOR);

            fontManager?.InitializeFontAfterGameManager(OverrideFont.Value);
            statusLabel?.InitializeStatusLabel();

            ETGModConsole.Commands.AddGroup("autotranslate", LogHelp);
            ETGModConsole.Commands.GetGroup("autotranslate").AddUnit("help", LogHelp);
            ETGModConsole.Commands.GetGroup("autotranslate").AddUnit("save_cache", SaveCache);
            ETGModConsole.Commands.GetGroup("autotranslate").AddUnit("load_cache", LoadCache);
        }

        private AutoTranslateConfig InitializeConfigs()
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

            ToggleTranslationKeyBinding = Config.Bind(
                "1.General",
                "ToggleTranslationKeyBinding",
                KeyCode.F10,
                "启用或关闭翻译的按键。The key binding of toggling translation."
                );

            FilterForFullTextNeedToTranslate = Config.Bind(
                "1.General",
                "FilterForFullTextNeedToTranslate",
                FilterForFullTextNeedToTranslateType.Chinese,
                "对整个文本生效。用来筛选待翻译的文本以节省翻译额度。Effective for the full text. Used to filter the text to be translated to save translation quotas."
                );

            RegexForFullTextNeedToTranslate = Config.Bind(
                "1.General",
                "RegexForFullTextNeedToTranslate",
                @"^(?!Enter the Gungeon).*$",
                "正则表达式，一个多行文本若匹配为真，则这个多行文本保留以待翻译。只在FilterForFullTextNeedToTranslate为CustomRegex时生效。Regular expression, if a multiple line text matches true, then this multiple line text is retained for translation. Only effective when FilterForFullTextNeedToTranslate is set to CustomRegex."
                );

            FilterForEachLineNeedToTranslate = Config.Bind(
                "1.General",
                "FilterForEachLineNeedToTranslate",
                FilterForEachLineNeedToTranslateType.Chinese,
                "对文本的每一行生效。用来筛选待翻译的文本以节省翻译额度。Effective for each line of text. Used to filter the text to be translated to save translation quotas."
                );

            RegexForEachLineNeedToTranslate = Config.Bind(
                "1.General",
                "RegexForEachLineNeedToTranslate",
                @"^(?![@#])(?=\S)(?!^[\d\p{P}]+$)(?!.*[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]).*$",
                "正则表达式，多行文本若存在一行匹配，整个多行文本保留以待翻译。只在FilterForEachLineNeedToTranslate为CustomRegex时生效。Regular expression, if there is a matching line in multiple lines of text, the entire multiple lines of text are retained for translation. Only effective when FilterForEachLineNeedToTranslate is set to CustomRegex."
                );

            FilterForIgnoredSubstringWithinText = Config.Bind(
                "1.General",
                "FilterForIgnoredSubstringWithinText",
                FilterForIgnoredSubstringWithinTextType.Chinese,
                "用来过滤文本中需要忽略的子文本。这通常包括一些要特殊处理的贴图和转义符。Used to filter sub texts that need to be ignored in the text. This usually includes some textures and escape characters that require special handling."
                );

            RegexForIgnoredSubstringWithinText = Config.Bind(
                "1.General",
                "RegexForIgnoredSubstringWithinText",
                @"(?:\[color\s+[^\]]+\])|(?:\[sprite\s+[^\]]+\])|(?:\[/color\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)|(?:<color=[^>]+>)|(?:</color>)|(?:^\s*[\d\p{P}]+\s*$)|(?:[<>\[\]])|(?:@[a-fA-F0-9]{6})",
                "正则表达式，匹配文本中需要忽略的子文本。请使用非捕获组。只在FilterForIgnoredSubstringWithinText为CustomRegex时生效。Regular expression, matching sub texts that need to be ignored in the text. Please use non capture groups. Only effective when FilterForIgnoredSubstringWithinText is set to CustomRegex."
                );

            MaxBatchCharacterCount = Config.Bind(
                "1.General",
                "MaxBatchCharacterCount",
                1024,
                "处理的批量数据最大字符数。若翻译api提示单次请求过长，请减小此值。The maximum count of batch data characters for processing. If the translation API prompts that a single request is too long, please reduce this value."
                );

            MaxBatchTextCount = Config.Bind(
                "1.General",
                "MaxBatchTextCount",
                0,
                "处理的批量数据最大项数。为0表示不限制。The maximum count of batch data texts for processing. A value of 0 indicates no restriction."
                );

            MaxRetryCount = Config.Bind(
                "1.General",
                "MaxRetryCount",
                3,
                "发生错误时的最大重试次数。The maximum number of retries when an error occurs."
                );

            RetryInterval = Config.Bind(
                "1.General",
                "RetryInterval",
                2f,
                "发生错误时的重试时间间隔。The interval of retries when an error occurs."
                );

            TranslationCacheCapacity = Config.Bind(
                "1.General",
                "TranslationCacheCapacity",
                1024,
                "最大翻译缓存容量。Maximum translation cache capacity."
                );

            PresetTranslations = Config.Bind(
                "1.General",
                "PresetTranslations",
                "CachedTranslations.json;PresetTranslations.json",
                "预设翻译的文件名。使用预设翻译以减少加载时常见文本的翻译请求，留空表示不使用。预设翻译为位于dll同目录下的JSON文件。用“;”分割，文件会按顺序先后加载。The file name for preset translations. Use preset translation to reduce translation requests for common text during loading, leaving blank to indicate not using. The preset translation is a JSON file located in the same directory as the DLL. Separated by ';', files will be loaded sequentially."
                );
            
            CachedTranslations = Config.Bind(
                "1.General",
                "CachedTranslations",
                "CachedTranslations.json",
                "缓存翻译的文件名。缓存翻译为位于dll同目录下的JSON文件。The file name cached translations. The preset translation is a JSON file located in the same directory as the DLL."
                );

            AutoSaveCachedTranslationsUponQuit = Config.Bind(
                "1.General",
                "AutoSaveCachedTranslationsUponQuit",
                false,
                "是否在退出时自动保存缓存翻译。强制退出时不会生效。Whether to automatically save cached translations upon quit. It will not take effect when quiting abnormally."
                );

            LogRequestedTexts = Config.Bind(
                "1.General",
                "LogRequestedTexts",
                false,
                "是否在日志中显示请求翻译的文本。Whether to log the text requested for translation."
                );

            TranslateTextFromDfLabel = Config.Bind(
                "1.General",
                "TranslateTextFromDfLabel",
                true,
                "翻译DfLabel中的文本。Translate the text in DfLabel."
                );

            TranslateTextFromDfButton = Config.Bind(
                "1.General",
                "TranslateTextFromDfButton",
                true,
                "翻译DfButton中的文本。Translate the text in DfButton."
                );

            TranslateTextFromTk2dTextMesh = Config.Bind(
                "1.General",
                "TranslateTextFromTk2dTextMesh",
                true,
                "翻译Tk2dTextMesh中的文本。Translate the text in Tk2dTextMesh."
                );

            OverrideFont = Config.Bind(
                "2.Font",
                "OverrideFont",
                OverrideFontType.Custom,
                "用来覆盖游戏字体的字体。根据你需要的目标语言选择。Font used to override the font of the game. Choose according to the target language you need."
                );

            FontAssetBundleName = Config.Bind(
                "2.Font",
                "FontAssetBundleName",
                "fusion_pixel_12px_zh_cn",
                "包含自定义字体的AssetBundle名称。位于dll同目录下。AssetBundle name containing custom fonts. Located in the same directory as DLL."
                );

            CustomDfFontName = Config.Bind(
                "2.Font",
                "CustomDfFontName",
                "Fusion_Pixel_12px_Monospaced_dfDynamic",
                "要使用的自定义df字体。请把它包含于FontAssetBundle。The custom df font to be used. Please include it in FontAssetBundle."
                );

            CustomTk2dFontName = Config.Bind(
                "2.Font",
                "CustomTk2dFontName",
                "Fusion_Pixel_12px_Monospaced_tk2d",
                "要使用的自定义tk2d字体。请把它包含于FontAssetBundle。The custom tk2d font to be used. Please include it in FontAssetBundle."
                );

            OverrideDfTokenizer = Config.Bind(
                "2.Font",
                "OverrideDfTokenizer",
                OverrideDfTokenizerType.Chinese,
                "覆盖的Df分词器。Token可以用于处理文本的自动换行位置。如每个字换行还是单词后换行。Override Df tokenizer. Token is used to handle the automatic line break position of text. Whether to wrap each word or to wrap after each word."
                );

            RegexForDfTokenizer = Config.Bind(
                "2.Font",
                "RegexForDfTokenizer",
                FontManager.defaultRegexForTokenizer,
                "用于Df生成token的正则表达式。只在OverrideDfTokenizer为CustomRegex时生效。A regular expression used for generating tokens for Df. Only effective when OverrideDfTokenizer is set to CustomRegex."
                );

            DfTextScaleExpandThreshold = Config.Bind(
                "2.Font",
                "DfTextScaleExpandThreshold",
                1f,
                "低于这个值的Df TextScale会被扩大。为负表示不生效。Df TextScale below this value will be expanded. Negative indicates non effectiveness."
                );

            DfTextScaleExpandToValue = Config.Bind(
                "2.Font",
                "DfTextScaleExpandToValue",
                2f,
                "低于门槛的Df TextScale被扩大到多少。How much is the Df TextScale below the threshold expanded to."
                );

            ShowRequestedCharacterCount = Config.Bind(
                "3.RequestedCharacterCount",
                "ShowRequestedCharacterCount",
                true,
                "显示已翻译字符数。Show requested character count."
                );

            RequestedCharacterCountAlertThreshold = Config.Bind(
                "3.RequestedCharacterCount",
                "RequestedCharacterCountAlertThreshold",
                50000,
                "已请求翻译字符数警报阈值。当翻译请求即将超出时，会暂停翻译。为零表示不设阈值。The alert threshold of the count of characters requested for translation. When this count is about to exceed, translation will be paused. A value of zero indicates no threshold is set."
                );

            ToggleRequestedCharacterCountKeyBinding = Config.Bind(
                "3.RequestedCharacterCount",
                "ToggleRequestedCharacterCountKeyBinding",
                KeyCode.F6,
                "开启或关闭已翻译字符数的显示的按键。The key binding of toggling the display of requested character count."
                );

            CountLabelAnchor = Config.Bind(
                "3.RequestedCharacterCount",
                "CountLabelAnchor",
                "0.5 0",
                "用空格或逗号分隔的一个二维向量，决定计数标签相对于父级的锚点位置。左上角为0 0，右下角是1 1。A two-dimensional vector separated by spaces or commas, which determines where the count label is anchored relative to its parent. The top left corner is 0 0, and the bottom right corner is 1 1."
                );

            CountLabelPivot = Config.Bind(
                "3.RequestedCharacterCount",
                "CountLabelPivot",
                "0.5 0",
                "用空格或逗号分隔的一个二维向量，定义计数标签的定位基准点。左上角为0 0，右下角是1 1。A two-dimensional vector separated by spaces or commas, which defines the pivot point of the count label for positioning. The top left corner is 0 0, and the bottom right corner is 1 1."
                );

            TranslateTextsOfItemTipsMod = Config.Bind(
                "4.Compatibility",
                "TranslateTextsOfItemTipsMod",
                true,
                "翻译ItemTipsMod中的文本。Translate the text in ItemTipsMod."
                );

            OverrideItemTipsTokenizer = Config.Bind(
                "4.Compatibility",
                "OverrideItemTipsTokenizer",
                OverrideItemTipsTokenizerType.Chinese,
                "覆盖的ItemTips分词器。Token可以用于处理文本的自动换行位置。如每个字换行还是单词后换行。Override ItemTips tokenizer. Token is used to handle the automatic line break position of text. Whether to wrap each word or to wrap after each word."
                );

            RegexForItemTipsModTokenizer = Config.Bind(
                "4.Compatibility",
                "RegexForItemTipsModTokenizer",
                FontManager.defaultRegexForItemTipsModTokenizer,
                "用于ItemTips生成token的正则表达式。只在OverrideItemTipsTokenizer为CustomRegex时生效。A regular expression used for generating tokens for ItemTips. Only effective when OverrideItemTipsTokenizer is set to CustomRegex."
                );

            ItemTipsFontScale = Config.Bind(
                "4.Compatibility",
                "ItemTipsFontScale",
                1.5f,
                "ItemTips的字体缩放大小。The font scale of ItemTips."
                );

            ItemTipsBackgroundWidthScale = Config.Bind(
                "4.Compatibility",
                "ItemTipsBackgroundWidthScale",
                1f,
                "ItemTips的背景宽度缩放大小。The width scale of ItemTips background."
                );

            ItemTipsLineHeightScale = Config.Bind(
                "4.Compatibility",
                "ItemTipsLineHeightScale",
                1.4f,
                "ItemTips的行高缩放大小。The width scale of ItemTips line height."
                );

            ItemTipsAnchor = Config.Bind(
                "4.Compatibility",
                "ItemTipsAnchor",
                "0.1 0.5",
                "用空格或逗号分隔的一个二维向量，决定ItemTips相对于父级的锚点位置。左上角为0 0，右下角是1 1。A two-dimensional vector separated by spaces or commas, which determines where ItemTips is anchored relative to its parent. The top left corner is 0 0, and the bottom right corner is 1 1."
                );

            ItemTipsPivot = Config.Bind(
                "4.Compatibility",
                "ItemTipsPivot",
                "0 0.5",
                "用空格或逗号分隔的一个二维向量，定义ItemTips的定位基准点。左上角为0 0，右下角是1 1。A two-dimensional vector separated by spaces or commas, which defines the pivot point of ItemTips for positioning. The top left corner is 0 0, and the bottom right corner is 1 1."
                );

            ItemTipsSourceBitmapFontBaseLine = Config.Bind(
                "4.Compatibility",
                "ItemTipsSourceBitmapFontBaseLine",
                10,
                "ItemTips的字体如果由位图字体生成，可以在此控制位图字体的基准线。If the font used by ItemTips is generated from a bitmap font, you can adjust the baseline of the bitmap font here."
                );

            TencentSecretId = Config.Bind(
                "5.TencentTranslation",
                "SecretId",
                "",
                "腾讯翻译的SecretId。The SecretId of Tencent Translate."
                );

            TencentSecretKey = Config.Bind(
                "5.TencentTranslation",
                "SecretKey",
                "",
                "腾讯翻译的SecretKey。The SecretKey of Tencent Translate."
                );

            TencentSourceLanguage = Config.Bind(
                "5.TencentTranslation",
                "SourceLanguage",
                "en",
                "腾讯翻译的源语言，如en。The source language of Tencent Translate, such as en."
                );

            TencentTargetLanguage = Config.Bind(
                "5.TencentTranslation",
                "TargetLanguage",
                "zh",
                "腾讯翻译的目标语言，如zh。Tencent Translate's target language, such as zh."
                );

            TencentRegion = Config.Bind(
                "5.TencentTranslation",
                "Region",
                "ap-beijing",
                "地域，用来标识希望连接哪个地域的服务器，如ap-beijing。Region, used to identify which region's server you want to connect to, such as ap-beijing."
                );

            BaiduAppId = Config.Bind(
                "6.BaiduTranslate",
                "SecretId",
                "",
                "百度翻译的AppId。The AppId of Baidu Translate."
                );

            BaiduSecretKey = Config.Bind(
                "6.BaiduTranslate",
                "SecretKey",
                "",
                "百度翻译的SecretKey。The SecretKey for Baidu Translate."
                );

            BaiduSourceLanguage = Config.Bind(
                "6.BaiduTranslate",
                "SourceLanguage",
                "en",
                "百度翻译的源语言，如en。The source language of Baidu Translate, such as en."
                );

            BaiduTargetLanguage = Config.Bind(
                "6.BaiduTranslate",
                "TargetLanguage",
                "zh",
                "百度翻译的目标语言，如zh。The target language for Baidu Translate, such as zh."
                );

            AzureSubscriptionKey = Config.Bind(
                "7.AzureTranslate",
                "AzureSubscriptionKey",
                "",
                "Azure翻译的SubscriptionKey。Subscription Key for Azure Translation."
                );

            AzureSourceLanguage = Config.Bind(
                "7.AzureTranslate",
                "SourceLanguage",
                "en",
                "Azure翻译的源语言，如en。The source language for Azure translation, such as en."
                );

            AzureTargetLanguage = Config.Bind(
                "7.AzureTranslate",
                "TargetLanguage",
                "zh-Hans",
                "Azure翻译的目标语言，如zh-Hans。The target language for Azure translation, such as zh-Hans."
                );

            AzureRegion = Config.Bind(
                "7.AzureTranslate",
                "AzureRegion",
                "global",
                "地域，用来标识希望连接哪个地域的服务器，如global。Region, used to identify which region's server you want to connect to, such as global."
                );

            LlmBaseUrl = Config.Bind(
                "8.Llm",
                "LlmBaseUrl",
                "",
                "大模型API的基础URL，用来连接到目标大模型服务的接口地址。Base URL of the large model API, used to connect to the target large model service endpoint."
            );

            LlmApiKey = Config.Bind(
                "8.Llm",
                "LlmApiKey",
                "",
                "大模型API的访问密钥，用来进行身份验证并访问API服务。API key for the large model, used for authentication and accessing the API service."
            );

            LlmName = Config.Bind(
                "8.Llm",
                "LlmName",
                "",
                "大模型的名称，指定要使用的模型版本或名称。The name of the large model, specifying which model version or name to use."
            );

            LlmPrompt = Config.Bind(
                "8.Llm",
                "LlmPrompt",
                "英文翻译为中文，仅返回JSON的内容。输出格式和输入一致。输入：[{\"id\":1,\"text\":\"Hello\"}, {\"id\":2,\"text\":\"World\"}] 输出：[{\"id\":1,\"text\":\"你好\"}, {\"id\":2,\"text\":\"世界\"}]",
                "Prompt是向语言模型提供指示或请求的输入文本，帮助模型理解任务并生成响应。A prompt is the input text given to a language model to guide it in understanding the task and generating a response."
            );

            LlmMaxTokens = Config.Bind(
                "8.Llm",
                "LlmMaxTokens",
                1024,
                "大模型API允许的最大token数，控制生成内容的最大长度。Maximum number of tokens allowed by the large model API, controlling the maximum length of generated content."
            );

            LlmTemperature = Config.Bind(
                "8.Llm",
                "LlmTemperature",
                1.3f,
                "控制大模型生成文本的创造性，值越高，生成的文本越随机，越低则越确定。Controls the creativity of the large model's text generation, with higher values leading to more random and lower values leading to more deterministic output."
            );

            LlmTopP = Config.Bind(
                "8.Llm",
                "LlmTopP",
                0.7f,
                "控制大模型生成文本时的采样范围，值越低，结果越集中。Controls the sampling range of the large model's text generation, with lower values leading to more focused results."
            );

            LlmTopK = Config.Bind(
                "8.Llm",
                "LlmTopK",
                30,
                "控制大模型生成文本时考虑的最高概率词的数量，值越高，结果越多样化。Controls the number of highest probability words considered by the large model during text generation, with higher values leading to more diverse results."
            );

            LlmFrequencyPenalty = Config.Bind(
                "8.Llm",
                "LlmFrequencyPenalty",
                0f,
                "控制生成文本中词汇重复的概率。值越高，生成内容越多样化；值越低，生成内容越集中和重复。Controls the likelihood of token repetition in generated text.Higher values promote diversity, while lower values result in more focused and repetitive outputs."
            );

            LlmExtraParametersJson = Config.Bind(
                "8.Llm",
                "LlmExtraParametersJson",
                @"{""enable_thinking"": false}",
                "一个JSON，列举了大模型需要的额外参数。A JSON listing the additional parameters required by the large language model."
            );

            LlmQuotePreprocess = Config.Bind(
                "8.Llm",
                "LlmQuotePreprocess",
                LlmQuotePreprocessType.Chinese,
                "用于设置引号预处理的类型。Sets the quote preprocessing type."
            );

            LlmDataFormat = Config.Bind(
                "8.Llm",
                "LlmDataFormat",
                LlmDataFormatType.Json,
                "用于设置收发数据的格式类型。Set the format type for sending and receiving data."
            );

            LlmSplitText = Config.Bind(
                "8.Llm",
                "LlmSplitText",
                "<tspl>",
                "仅当LlmDataFormat为Split时生效。分割形式收发数据时的分隔符。Only takes effect when LlmDataFormat is set to Split. The split text for sending and receiving data in the format of splitted."
            );

            LlmPositionText = Config.Bind(
                "8.Llm",
                "LlmPositionText",
                "<!pos!>",
                "仅当LlmDataFormat为PositionTagged时生效。位置编号的起始标记。Only takes effect when LlmDataFormat is set to PositionTagged. The starting tag of the position number."
            );
                       
            LlmSegmentText = Config.Bind(
                "8.Llm",
                "LlmSegmentText",
                "<!seg!>",
                "仅当LlmDataFormat为Positioned时生效。被定位的段的起始标记。Only takes effect when LlmDataFormat is set to Positioned. The starting tag of the positioned segment."
            );

            AutoTranslateConfig config = new AutoTranslateConfig
            {
                TranslationAPI = TranslationAPI.Value,
                ToggleTranslationKeyBinding = ToggleTranslationKeyBinding.Value,
                FilterForFullTextNeedToTranslate = FilterForFullTextNeedToTranslate.Value,
                RegexForFullTextNeedToTranslate = RegexForFullTextNeedToTranslate.Value,
                FilterForEachLineNeedToTranslate = FilterForEachLineNeedToTranslate.Value,
                RegexForEachLineNeedToTranslate = RegexForEachLineNeedToTranslate.Value,
                FilterForIgnoredSubstringWithinText = FilterForIgnoredSubstringWithinText.Value,
                RegexForIgnoredSubstringWithinText = RegexForIgnoredSubstringWithinText.Value,
                MaxBatchCharacterCount = MaxBatchCharacterCount.Value,
                MaxBatchTextCount = MaxBatchTextCount.Value,
                MaxRetryCount = MaxRetryCount.Value,
                RetryInterval = RetryInterval.Value > 0 ? RetryInterval.Value : 2f,
                TranslationCacheCapacity = TranslationCacheCapacity.Value,
                PresetTranslations = PresetTranslations.Value,
                CachedTranslations = CachedTranslations.Value,
                AutoSaveCachedTranslationsUponQuit = AutoSaveCachedTranslationsUponQuit.Value,
                LogRequestedTexts = LogRequestedTexts.Value,
                TranslateTextFromDfLabel = TranslateTextFromDfLabel.Value,
                TranslateTextFromDfButton = TranslateTextFromDfButton.Value,
                TranslateTextFromTk2dTextMesh = TranslateTextFromTk2dTextMesh.Value,

                OverrideFont = OverrideFont.Value,
                FontAssetBundleName = FontAssetBundleName.Value,
                CustomDfFontName = CustomDfFontName.Value,
                CustomTk2dFontName = CustomTk2dFontName.Value,
                OverrideDfTokenizer = OverrideDfTokenizer.Value,
                RegexForDfTokenizer = RegexForDfTokenizer.Value,
                DfTextScaleExpandThreshold = DfTextScaleExpandThreshold.Value,
                DfTextScaleExpandToValue = DfTextScaleExpandToValue.Value,

                ShowRequestedCharacterCount = ShowRequestedCharacterCount.Value,
                RequestedCharacterCountAlertThreshold = RequestedCharacterCountAlertThreshold.Value,
                ToggleRequestedCharacterCountKeyBinding = ToggleRequestedCharacterCountKeyBinding.Value,
                CountLabelAnchor = CountLabelAnchor.Value,
                CountLabelPivot = CountLabelPivot.Value,

                TranslateTextsOfItemTipsMod = TranslateTextsOfItemTipsMod.Value,
                OverrideItemTipsTokenizer = OverrideItemTipsTokenizer.Value,
                RegexForItemTipsModTokenizer = RegexForItemTipsModTokenizer.Value,
                ItemTipsFontScale = ItemTipsFontScale.Value > 0 ? ItemTipsFontScale.Value : 1f,
                ItemTipsBackgroundWidthScale = ItemTipsBackgroundWidthScale.Value > 0 ? ItemTipsBackgroundWidthScale.Value : 1f,
                ItemTipsLineHeightScale = ItemTipsLineHeightScale.Value > 0 ? ItemTipsLineHeightScale.Value : 1f,
                ItemTipsAnchor = ItemTipsAnchor.Value,
                ItemTipsPivot = ItemTipsPivot.Value,
                ItemTipsSourceBitmapFontBaseLine = ItemTipsSourceBitmapFontBaseLine.Value,

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

                LlmBaseUrl = LlmBaseUrl.Value,
                LlmApiKey = LlmApiKey.Value,
                LlmName = LlmName.Value,
                LlmPrompt = LlmPrompt.Value,
                LlmMaxTokens = LlmMaxTokens.Value,
                LlmTemperature = LlmTemperature.Value,
                LlmTopP = LlmTopP.Value,
                LlmTopK = LlmTopK.Value,
                LlmFrequencyPenalty = LlmFrequencyPenalty.Value,
                LlmExtraParametersJson = LlmExtraParametersJson.Value,
                LlmQuotePreprocess = LlmQuotePreprocess.Value,
                LlmDataFormat = LlmDataFormat.Value,
                LlmSplitText = LlmSplitText.Value,
                LlmPositionText = LlmPositionText.Value,
                LlmSegmentText = LlmSegmentText.Value
            };

            config.CheckConfig();
            return config;
        }

        private void DoOptionalPatches()
        {
            if (Chainloader.PluginInfos.ContainsKey("glorfindel.etg.itemtips"))
            {
                FontManager.instance.itemTipsModuleType = AccessTools.TypeByName("ItemTipsMod.ItemTipsModule");
                MethodInfo methodInfo1 = AccessTools.Method(FontManager.instance.itemTipsModuleType, "GmStart", null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.GmStartPatchClass), nameof(AutoTranslatePatches.GmStartPatchClass.GmStartPostfix), null, null);
                harmony.Patch(methodInfo1, null, new HarmonyMethod(fixMethodInfo1), null, null, null);

                MethodInfo methodInfo2 = AccessTools.Method(FontManager.instance.itemTipsModuleType, "LoadFont", null, null);
                MethodInfo fixMethodInfo2 = AccessTools.Method(typeof(AutoTranslatePatches.LoadFontPatchClass), nameof(AutoTranslatePatches.LoadFontPatchClass.LoadFontPrefix), null, null);
                harmony.Patch(methodInfo2, new HarmonyMethod(fixMethodInfo2), null, null, null, null);

                MethodInfo methodInfo3 = AccessTools.Method(FontManager.instance.itemTipsModuleType, "ConvertStringToFixedWidthLines", null, null);
                MethodInfo fixMethodInfo3 = AccessTools.Method(typeof(AutoTranslatePatches.ConvertStringToFixedWidthLinesPatchClass), nameof(AutoTranslatePatches.ConvertStringToFixedWidthLinesPatchClass.ConvertStringToFixedWidthLinesPrefix), null, null);
                harmony.Patch(methodInfo3, new HarmonyMethod(fixMethodInfo3), null, null, null, null);

                MethodInfo methodInfo4 = AccessTools.Method(FontManager.instance.itemTipsModuleType, "GenerateTextInfo", null, null);
                MethodInfo fixMethodInfo4 = AccessTools.Method(typeof(AutoTranslatePatches.GenerateTextInfoPatchClass), nameof(AutoTranslatePatches.GenerateTextInfoPatchClass.GenerateTextInfoPatch), null, null);
                harmony.Patch(methodInfo4, null, null, null, null, new HarmonyMethod(fixMethodInfo4));

                MethodInfo methodInfo5 = AccessTools.Method(FontManager.instance.itemTipsModuleType, "ShowTip", null, null);
                MethodInfo fixMethodInfo5 = AccessTools.Method(typeof(AutoTranslatePatches.ShowTipPatchClass), nameof(AutoTranslatePatches.ShowTipPatchClass.ShowTipPostfix), null, null);
                harmony.Patch(methodInfo5, null, new HarmonyMethod(fixMethodInfo5), null, null, null);

                Type labelType = AccessTools.TypeByName("ItemTipsMod.Label");
                MethodInfo methodInfo6 = AccessTools.Method(labelType, "Reposition", null, null);
                MethodInfo fixMethodInfo6 = AccessTools.Method(typeof(AutoTranslatePatches.RepositionPatchClass), nameof(AutoTranslatePatches.RepositionPatchClass.RepositionPostfix), null, null);
                harmony.Patch(methodInfo6, null, new HarmonyMethod(fixMethodInfo6), null, null, null);
            }

            if (Chainloader.PluginInfos.ContainsKey("lazymo3_and_NilT_PL.etg.NoBrain"))
            {
                MethodInfo methodInfo1 = AccessTools.Method(AccessTools.TypeByName("NBInteractableBehaviour"), "onNewShopItemController", null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.NewShopItemControllerPatchClass), nameof(AutoTranslatePatches.NewShopItemControllerPatchClass.NewShopItemControllerPrefix), null, null);
                harmony.Patch(methodInfo1, new HarmonyMethod(fixMethodInfo1), null, null, null, null);
            }

            if (RegexForDfTokenizer.Value != string.Empty)
            {
                Type dynamicFontRendererType = typeof(dfDynamicFont.DynamicFontRenderer);
                MethodInfo methodInfo1 = AccessTools.Method(dynamicFontRendererType, nameof(dfDynamicFont.DynamicFontRenderer.tokenize), null, null);
                MethodInfo fixMethodInfo1 = AccessTools.Method(typeof(AutoTranslatePatches.DynamicFontRendererTokenizePatchClass), nameof(AutoTranslatePatches.DynamicFontRendererTokenizePatchClass.TokenizePrefix), null, null);
                harmony.Patch(methodInfo1, new HarmonyMethod(fixMethodInfo1), null, null, null, null);

                MethodInfo methodInfo2 = AccessTools.Method(dynamicFontRendererType, nameof(dfDynamicFont.DynamicFontRenderer.calculateLinebreaks), null, null);
                MethodInfo fixMethodInfo2 = AccessTools.Method(typeof(AutoTranslatePatches.DynamicFontRendererCalculateLinebreaksPatchClass), nameof(AutoTranslatePatches.DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatch), null, null);
                harmony.Patch(methodInfo2, null, null, null, null, new HarmonyMethod(fixMethodInfo2));

                Type BitmappedFontRendererType = typeof(dfFont.BitmappedFontRenderer);
                MethodInfo methodInfo3 = AccessTools.Method(BitmappedFontRendererType, nameof(dfFont.BitmappedFontRenderer.tokenize), null, null);
                MethodInfo fixMethodInfo3 = AccessTools.Method(typeof(AutoTranslatePatches.BitmappedFontRendererTokenizePatchClass), nameof(AutoTranslatePatches.BitmappedFontRendererTokenizePatchClass.TokenizePrefix), null, null);
                harmony.Patch(methodInfo3, new HarmonyMethod(fixMethodInfo3), null, null, null, null);

                MethodInfo methodInfo4 = AccessTools.Method(BitmappedFontRendererType, nameof(dfFont.BitmappedFontRenderer.calculateLinebreaks), null, null);
                MethodInfo fixMethodInfo4 = AccessTools.Method(typeof(AutoTranslatePatches.BitmappedFontRendererCalculateLinebreaksPatchClass), nameof(AutoTranslatePatches.BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatch), null, null);
                harmony.Patch(methodInfo4, null, null, null, null, new HarmonyMethod(fixMethodInfo4));
            }
        }

        private void LogHelp(string[] args)
        {
            ETGModConsole.Log($"");
            ETGModConsole.Log($"Command list:");
            ETGModConsole.Log($"  -- <color={TEXT_COLOR}>autotranslate save_cache [cache_json_name]</color>");
            ETGModConsole.Log($"     - Save the translation cache to [cache_json_name], which defaults to CachedTranslations.json.");
            ETGModConsole.Log($"  -- <color={TEXT_COLOR}>autotranslate load_cache [cache_json_name]</color>");
            ETGModConsole.Log($"     - Load the translation cache from [cache_json_name], which defaults to CachedTranslations.json.");
        }

        private void SaveCache(string[] args)
        {
            string fileName;
            if (args != null && args.Length > 0)
                fileName = args[0];
            else if (!string.IsNullOrEmpty(config.CachedTranslations))
                fileName = config.CachedTranslations;
            else
                fileName = "CachedTranslations.json";

            string fullPath = Path.Combine(ETGMod.FolderPath(instance), fileName);
            translateManager.SaveTranslationCache(fullPath, config.TranslationCacheCapacity);
            ETGModConsole.Log($"Translation cache saved successfully to {fullPath}");
        }

        private void LoadCache(string[] args)
        {
            string fileName;
            if (args != null && args.Length > 0)
                fileName = args[0];
            else if (!string.IsNullOrEmpty(config.CachedTranslations))
                fileName = config.CachedTranslations;
            else
                fileName = "CachedTranslations.json";
            string fullPath = Path.Combine(ETGMod.FolderPath(instance), fileName);
            translateManager.ReadAndRestoreTranslationCache(fullPath);
            ETGModConsole.Log($"Translation cache loaded successfully from {fullPath}");
        }

        public void OnApplicationQuit()
        {
            if (config.AutoSaveCachedTranslationsUponQuit)
            {
                SaveCache(null);
            }
        }

        public enum FilterForFullTextNeedToTranslateType
        {
            Chinese,
            CustomRegex,
        }

        public enum FilterForEachLineNeedToTranslateType
        {
            Chinese,
            CustomRegex,
        }

        public enum FilterForIgnoredSubstringWithinTextType
        {
            Chinese,
            CustomRegex,
        }

        public enum TranslationAPIType
        {
            Tencent,
            Baidu,
            Azure,
            Llm,
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

        public enum OverrideDfTokenizerType
        {
            None,
            Chinese,
            CustomRegex,
        }

        public enum OverrideItemTipsTokenizerType
        {
            Chinese,
            CustomRegex,
        }

        public enum LlmQuotePreprocessType
        {
            None,
            Chinese,
        }

        public enum LlmDataFormatType
        {
            Json,
            Split,
            Positioned,
            Parallel,
        }
    }
}
