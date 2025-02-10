# 自动翻译 AutoTranslate


## **1. 声明**（重要）

1. 本 mod 为 EtG 提供对一些线上翻译 api 的支持，可以翻译游戏内文字为翻译 api 支持的语言。
2. 本 mod 的作者**不收取**任何费用，但是**使用网络翻译 api 可能产生费用**，由相应 api 的**服务商**收取。不是所有的翻译请求都会产生费用，多数翻译 api 有**免费额度**，如每月 100 万字符到 500 万字符不等，请查阅使用的 api 的定价标准。如过量使用网络翻译 api 从而产生账单，本 mod 作者**不承担任何责任**。
3. 使用本 mod 需要向翻译 api 服务商申请服务获得密钥，具体请查阅翻译 api 服务商的文档。
4. 本 mod 的网络请求只与相关网络翻译 api 进行，**不会收集、发送或公开用户的任何信息**，包括个人的翻译 api 密钥等。
5. 请从**正规途径**获取本 mod。请自行辨别传播的版本。
6. 本 mod 仅供学习使用。
7. 请保护好个人信息，不要将个人的网络翻译 api 密钥等信息通过**手动截图**、**使用 mod 管理器的共享配置文件**等方式进行共享，以免造成不必要的损失。
8. 使用本 mod 代表**信任本 mod 并同意以上声明**。

## **1. Declaration** (Important)

1. This mod provides support for some online translation APIs in EtG, allowing in-game text to be translated into languages supported by these APIs.
2. The author of this mod **does not charge** any fees, but **using online translation APIs may incur costs**, which will be charged by the respective **API service providers**. Not all translation requests will incur fees; most translation APIs offer **a free quota**, ranging from 1 million to 5 million characters per month for example. Please refer to the pricing standards of the API you are using. If excessive use of the translation APIs results in charges, the author of this mod is **not responsible**.
3. To use this mod, you need to apply for a service and obtain a key from the translation API provider. Please refer to the provider's documentation for details.
4. The mod's network requests are only made to the relevant translation APIs and **will not collect, transmit, or disclose any user information**, including personal translation API keys.
5. Please obtain this mod through **legitimate channels**. Be cautious of distributed versions.
6. This mod is for learning use only.
7. Please protect your personal information and avoid sharing your translation API key through **manual screenshots**, **shared configuration files using mod managers**, etc., to prevent unnecessary losses.
8. By using this mod, you **trust it and agree to the above declaration**.

---
## **2. AutoTranslate 使用方式**

1. 从 mod 管理器安装 AutoTranslate。
2. **第一次**安装时，开启该 mod 启动一次游戏并关闭游戏。
3. 在mod 管理器的配置中找到 AutoTranslate。
4. 在 General 选项卡中选择使用的**翻译服务 api**。
5. 设置**RegexForFullTextNeedToTranslate**、**RegexForEachLineNeedToTranslate**和**RegexForIgnoredSubstringWithinText**。默认为中文设置，如果目标语言是中文不用修改。其他语言自行修改。
6. 在下方 **OverrideFont** 选择需要覆盖的字体。若目标语言是为中文，设为Chinese或**Custom（推荐）**。其他语言自行设置。
7. 如果你的目标语言**不是中文**，把 **PresetTranslations** 置为空，或者用你自定义的PresetTranslations.json。
8. 进入选择的 **api 选项卡**，输入该 api **需要的密钥和其他细节如源语言、目标语言等**。翻译 api 需要的必选项和可选项请查阅服务商官方说明。
9. 启动游戏。

## **2. How to Use AutoTranslate**

1. Install AutoTranslate from the mod manager.
2. When installing **for the first time**, launch the game with this mod enabled, then close the game.
3. In the mod manager, find AutoTranslate in the configuration settings.
4. In the General tab, select **the translation service API** you want to use.
5. Set **RegexForFullTextNeedToTranslation**, **RegexForEachLineNedToTranslate** and **RegexForIgnoreDubstringWithinText**. The default setting is for Chinese. If the target language is Chinese, there is no need to modify it. For other languages, please set on your own.
6. Select the font you want to override in the **OverrideFont** below. If the target language is Chinese, set it as Chinese or **Custom (recommended)**. For other languages, please modify on your own.
7. If your target language is **not Chinese**, set **PresetTranslations** to empty or use your custom PresetTranslations.json.
8. Go to **the tab for the selected API**, and enter **the required key and other details like source language, target language, etc**. For required and optional parameters for the translation API, please refer to the official documentation of the API provider.
9. Launch the game.

---
## **3. 节约翻译额度的方法**

包括设置**RegexForFullTextNeedToTranslate**、**RegexForEachLineNeedToTranslate**和**PresetTranslations**都可以显著减少不必要的翻译。见详细说明。

## **3. Ways to Save Translation Quotas**  

Setting **RegexForFullTextNeedToTranslate**, **RegexForEachLineNeedToTranslate**, and **PresetTranslations** can significantly reduce unnecessary translations. Please refer to detailed description.

---
## **4. AutoTranslate功能详细说明**

1. **TranslationAPI**  
   选择使用的翻译 API。支持腾讯翻译 api、百度翻译 api和 Microsoft Azure 翻译 api。截止 2025.1.29，三者的免费额度为 Tencent 500万字符/月、Baidu 100万字符/月、Azure 200万字符/月。详见官方说明。

2. **ToggleTranslationKeyBinding**  
   启用或关闭翻译的按键。

3. **RegexForFullTextNeedToTranslate**  
   正则表达式，一个多行文本若匹配为真，则这个多行文本保留以待翻译。用来筛选待翻译的文本以节省翻译额度。
   样例RegexForFullTextNeedToTranslate正则表达式：`^(?!Enter the Gungeon).*$`

4. **RegexForEachLineNeedToTranslate**  
   正则表达式，多行文本若存在一行匹配，整个多行文本保留以待翻译。用来筛选待翻译的文本以节省翻译额度。样例RegexForEachLineNeedToTranslate正则表达式（适配中文）：`^(?![@#])(?=\S)(?!^[\d\p{P}]+$)(?!.*[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]).*$`

5. **RegexForIgnoredSubstringWithinText**  
   正则表达式，匹配文本中需要忽略的子文本。请使用非捕获组。这通常包括一些要特殊处理的贴图和转义符。样例RegexForIgnoredSubstringWithinText正则表达式（适配中文）：`(?:\[[^\]]*\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)|(?:<color=[^>]+>)|(?:</color>)|(?:<|>)`

6. **RequestBatchSize**  
   发送请求的批量数据总大小。若翻译 api 提示单次请求过长，请减小此值。

7. **MaxRetryCount**  
   发生错误时的最大重试次数。

8. **TranslationCacheCapacity**  
   最大翻译缓存容量。

9. **TranslateTextsOfItemTipsMod**  
   翻译 ItemTipsMod 中的文本。

10. **RegexForItemTipsModTokenizer**  
   用于 ItemTipsMod 生成 token 的正则表达式。token 用于处理文本的自动换行位置。如每个字换行还是单词后换行。样例 RegexForItemTipsModTokenizer 正则表达式：`(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)`

11. **PresetTranslations**  
   预设翻译的文件名。使用预设翻译以减少加载时常见文本的翻译请求，留空表示不使用。预设翻译为位于 dll 同目录下的 json 文件。

12. **OverrideFont**  
   用来覆盖游戏字体的字体。根据你需要的目标语言选择。

13. **FontAssetBundleName**  
   包含自定义字体的 AssetBundle 名称。位于 dll 同目录下。

14. **CustomDfFontName**  
   要使用的自定义 df 字体。请把它包含于 FontAssetBundle。

15. **CustomTk2dFontName**  
   要使用的自定义 tk2d 字体。请把它包含于 FontAssetBundle。

16. **RegexForDfTokenizer**  
   用于 df 生成 token 的正则表达式。token 用于处理文本的自动换行位置。如每个字换行还是单词后换行。参考默认样例填写。建议只修改 Text 相关内容。样例RegexForDfTokenizer正则表达式：`(?<StartTag>\[(?<StartTagName>(color|sprite))\s*(?<AttributeValue>[^\]\s]+)?\])|(?<EndTag>\[\/(?<EndTagName>color|sprite)\])|(?<Newline>\r?)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.)`

    - **StartTag**：Tag 起始。
    - **TagName**：Tag 名称。
    - **AttributeValue**：Tag 属性值。
    - **EndTag**：Tag 结束。
    - **Newline**：换行。
    - **Whitespace**：空格。
    - **Text**：一个不可在中间换行的最小文本块。换行只能出现在它的后方。

17. **LogRequestedTexts**  
   在日志中显示请求翻译的文本。

18. **DfTextScaleExpandThreshold**  
   低于这个值的 Df TextScale 会被扩大。为负表示不生效。

19. **DfTextScaleExpandToValue**  
   低于门槛的 Df TextScale 被扩大到多少。

20. **ShowRequestedCharacterCount**  
   显示已翻译字符数。

21. **RequestedCharacterCountAlertThreshold**  
   已请求翻译字符数警报阈值。当翻译请求即将超出时，会暂停翻译。为零表示不设阈值。

22. **ToggleRequestedCharacterCountKeyBinding**  
   开启或关闭已翻译字符数的显示的按键。

## **4. Detailed Description of AutoTranslate Features**

1. **TranslationAPI**  
   Select the translation API to use. Supported APIs include Tencent Translation API, Baidu Translation API, and Microsoft Azure Translation API. As of January 29, 2025, the free quotas are: Tencent 5 million characters/month, Baidu 1 million characters/month, Azure 2 million characters/month. Please refer to the official documentation for more details.

2. **ToggleTranslationKeyBinding**  
   The key binding of toggling translation.

3. **RegexForFullTextNeedToTranslate**  
   A regular expression that matches multiline text. If the expression evaluates to true, the entire multiline text will be preserved for translation. This helps filter out unnecessary translation requests and save tranlation quotas. Example RegexForFullTextNeedToTranslate regex: `^(?!Enter the Gungeon).*$`

4. **RegexForEachLineNeedToTranslate**  
   A regular expression that checks each line in multiline text. If any line matches, the whole multiline text will be preserved for translation. This also helps filter out unnecessary translation requests and save tranlation quotas. Example RegexForEachLineNeedToTranslate regex (fit for Chinese): `^(?![@#])(?=\S)(?!^[\d\p{P}]+$)(?!.*[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]).*$`

5. **RegexForIgnoredSubstringWithinText**  
   A regular expression that matches substrings within text that should be ignored. Please use non-capturing groups. This typically includes elements like special image tags or escape sequences that need custom handling. Example RegexForIgnoredSubstringWithinText regex (fit for Chinese): `(?:\[[^\]]*\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)|(?:<color=[^>]+>)|(?:</color>)|(?:<|>)`

6. **RequestBatchSize**  
   The total size of the batch data for each translation request. If the translation API indicates that a request is too large, reduce this value.

7. **MaxRetryCount**  
   The maximum number of retry attempts when an error occurs during a translation request.

8. **TranslationCacheCapacity**  
   The maximum cache size for storing translations.

9. **TranslateTextsOfItemTipsMod**  
   Translate the text within the ItemTipsMod.

10. **RegexForItemTipsModTokenizer**  
   A regular expression used for generating tokens from ItemTipsMod. Token is used to handle the automatic line break position of text. Whether to wrap each word or to wrap after each word. Example RegexForItemTipsModTokenizer regex：`(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)`

11. **PresetTranslations**  
   The filename of preset translations. Preset translations reduce translation requests for common texts during game loading. Leave it empty to disable this feature. The preset translations file is a JSON file located in the same directory as the DLL.

12. **OverrideFont**  
   Font used to override the font of the game. Choose according to the target language you need.

13. **FontAssetBundleName**   
   The name of the AssetBundle containing custom fonts. Located in the same directory as the DLL.

14. **CustomDfFontName**  
   The name of the custom DF font to use. Include it within the FontAssetBundle.

15. **CustomTk2dFontName**  
   The name of the custom tk2d font to use. Include it within the FontAssetBundle.

16. **RegexForDfTokenizer**  
   A regular expression used to generate tokens for the DF tokenizer. Tokens help control the automatic line breaks for text. For example, whether to break lines after each character or after each word. Refer to the default example and modify mainly the text-related components. Example RegexForDfTokenizer regex: `(?<StartTag>\[(?<StartTagName>(color|sprite))\s*(?<AttributeValue>[^\]\s]+)?\])|(?<EndTag>\[\/(?<EndTagName>color|sprite)\])|(?<Newline>\r?)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.))`

    - **StartTag**: The start of a tag.
    - **TagName**: The name of the tag.
    - **AttributeValue**: The value of the tag attribute.
    - **EndTag**: The end of a tag.
    - **Newline**: A newline character.
    - **Whitespace**: Spaces or tabs.
    - **Text**: The smallest text block that cannot break in the middle; line breaks can only occur after it.

17. **LogRequestedTexts**  
   Log the text requested for translation.

18. **DfTextScaleExpandThreshold**  
   Df TextScale below this value will be expanded. Negative indicates non effectiveness.

19. **DfTextScaleExpandToValue**  
   How much is the Df TextScale below the threshold expanded to.

20. **ShowRequestedCharacterCount**  
   Show requested character count.

21. **RequestedCharacterCountAlertThreshold**  
   The alert threshold of the count of characters requested for translation. When this count is about to exceed, translation will be paused.

22. **ToggleRequestedCharacterCountKeyBinding**  
   The key binding of toggling the display of requested character count.

---

## **5. 字体相关**

若默认字体不支持所需语言的字符，可以使用自定义字体。详见GitHub页面。

## **5. Font-related**

If the default font does not support the characters required for a specific language, you can use a custom font. For more details, please refer to the GitHub page.
