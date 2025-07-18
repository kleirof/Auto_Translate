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
3. 在 mod 管理器的配置中找到 AutoTranslate。
4. 在 **General 选项卡**中选择使用的**翻译服务 api**。
5. 在 **General 选项卡**中，设置 **RegexForFullTextNeedToTranslate**、**RegexForEachLineNeedToTranslate** 和 **RegexForIgnoredSubstringWithinText**。默认为中文设置，如果目标语言是中文不用修改。其他语言自行修改。
6. 在 **General 选项卡**中，如果你的目标语言**不是中文**，把 **PresetTranslations** 置为空，或者用你自定义的 PresetTranslations.json。
7. 在 **Font 选项卡**中 **OverrideFont** 选择需要覆盖的字体。若目标语言是为中文，设为 Chinese 或 **Custom（推荐）**。其他语言自行设置。
8. 进入选择的 **api 选项卡**，输入该 api **需要的密钥和其他细节如源语言、目标语言等**。翻译 api 需要的必选项和可选项请查阅服务商官方说明。
9. 启动游戏。

## **2. How to Use AutoTranslate**

1. Install AutoTranslate from the mod manager.
2. When installing **for the first time**, launch the game with this mod enabled, then close the game.
3. In the mod manager, find AutoTranslate in the configuration settings.
4. In **the General tab**, select **the translation service API** you want to use.
5. In **the General tab**, Set **RegexForFullTextNeedToTranslation**, **RegexForEachLineNedToTranslate** and **RegexForIgnoreDubstringWithinText**. The default setting is for Chinese. If the target language is Chinese, there is no need to modify it. For other languages, please set on your own.
6. In **the General tab**, If your target language is **not Chinese**, set **PresetTranslations** to empty or use your custom PresetTranslations.json.
7. In **the Font tab**, Select the font you want to override in the **OverrideFont** below. If the target language is Chinese, set it as Chinese or **Custom (recommended)**. For other languages, please modify on your own.
8. Go to **the tab for the selected API**, and enter **the required key and other details like source language, target language, etc**. For required and optional parameters for the translation API, please refer to the official documentation of the API provider.
9. Launch the game.

---
## **3. 节约翻译额度的方法**

包括设置 **RegexForFullTextNeedToTranslate**、**RegexForEachLineNeedToTranslate** 和 **PresetTranslations** 都可以显著减少不必要的翻译。见详细说明。

## **3. Ways to Save Translation Quotas**  

Setting **RegexForFullTextNeedToTranslate**, **RegexForEachLineNeedToTranslate**, and **PresetTranslations** can significantly reduce unnecessary translations. Please refer to detailed description.

---
## **4. AutoTranslate功能详细说明**

### 命令 Command
- autotranslate save_cache \[cache_json_name\]
将翻译缓存保存为\[cache_json_name\]，默认为CachedTranslations.json。
- autotranslate load_cache \[cache_json_name\]
加载翻译缓存自\[cache_json_name\]，默认为CachedTranslations.json。

### 总体 General

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
   正则表达式，匹配文本中需要忽略的子文本。请使用非捕获组。这通常包括一些要特殊处理的贴图和转义符。样例RegexForIgnoredSubstringWithinText正则表达式（适配中文）：`(?:\[color\s+[^\]]+\])|(?:\[sprite\s+[^\]]+\])|(?:\[/color\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)|(?:<color=[^>]+>)|(?:</color>)|(?:^\s*[\d\p{P}]+\s*$)|(?:[<>\[\]])|(?:@[a-fA-F0-9]{6})`

6. **MaxBatchCharacterCount**  
   处理的批量数据最大字符数。若翻译api提示单次请求过长，请减小此值。

7. **MaxBatchTextCount**  
   处理的批量数据最大项数。为0表示不限制。

8. **MaxRetryCount**  
   发生错误时的最大重试次数。

9. **RetryInterval**  
   发生错误时的重试时间间隔。

10. **TranslationCacheCapacity**  
   最大翻译缓存容量。

11. **PresetTranslations**  
   预设翻译的文件名。使用预设翻译以减少加载时常见文本的翻译请求，留空表示不使用。预设翻译为位于dll同目录下的JSON文件。用“;”分割，文件会按顺序先后加载。

12. **CachedTranslations**  
   缓存翻译的文件名。缓存翻译为位于dll同目录下的JSON文件。

13. **AutoSaveCachedTranslationsUponQuit**  
   是否在退出时自动保存缓存翻译。强制退出时不会生效。

14. **LogRequestedTexts**  
   是否在日志中显示请求翻译的文本。

### 字体 Font

1. **OverrideFont**  
   用来覆盖游戏字体的字体。根据你需要的目标语言选择。

2. **FontAssetBundleName**  
   包含自定义字体的 AssetBundle 名称。位于 dll 同目录下。

3. **CustomDfFontName**  
   要使用的自定义 df 字体。请把它包含于 FontAssetBundle。

4. **CustomTk2dFontName**  
   要使用的自定义 tk2d 字体。请把它包含于 FontAssetBundle。

5. **RegexForDfTokenizer**  
   用于 df 生成 token 的正则表达式。token 用于处理文本的自动换行位置。如每个字换行还是单词后换行。参考默认样例填写。建议只修改 Text 相关内容。样例RegexForDfTokenizer正则表达式：`(?<StartTag>\[(?<StartTagName>(color|sprite))\s*(?<AttributeValue>[^\]\s]+)?\])|(?<EndTag>\[\/(?<EndTagName>color|sprite)\])|(?<Newline>\r?\n)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.)`

    - **StartTag**：Tag 起始。
    - **TagName**：Tag 名称。
    - **AttributeValue**：Tag 属性值。
    - **EndTag**：Tag 结束。
    - **Newline**：换行。
    - **Whitespace**：空格。
    - **Text**：一个不可在中间换行的最小文本块。换行只能出现在它的后方。

6. **DfTextScaleExpandThreshold**  
   低于这个值的 Df TextScale 会被扩大。为负表示不生效。

7. **DfTextScaleExpandToValue**  
   低于门槛的 Df TextScale 被扩大到多少。

### 请求字符数 Requested Character Count

1. **ShowRequestedCharacterCount**  
   显示已翻译字符数。

2. **RequestedCharacterCountAlertThreshold**  
   已请求翻译字符数警报阈值。当翻译请求即将超出时，会暂停翻译。为零表示不设阈值。

3. **ToggleRequestedCharacterCountKeyBinding**  
   开启或关闭已翻译字符数的显示的按键。

4. **CountLabelAnchor**  
   用空格或逗号分隔的一个二维向量，决定计数标签相对于父级的锚点位置。左上角为0 0，右下角是1 1。

5. **CountLabelPivot**  
   用空格或逗号分隔的一个二维向量，定义计数标签的定位基准点。左上角为0 0，右下角是1 1。

### 兼容 Compatibility

1. **TranslateTextsOfItemTipsMod**  
   翻译 ItemTipsMod 中的文本。

2. **RegexForItemTipsModTokenizer**  
   用于 ItemTipsMod 生成 token 的正则表达式。token 用于处理文本的自动换行位置。如每个字换行还是单词后换行。样例 RegexForItemTipsModTokenizer 正则表达式：`(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)`
   
3. **ItemTipsFontScale**  
   ItemTips的字体缩放大小。
   
4. **ItemTipsBackgroundWidthScale**  
   ItemTips的背景宽度缩放大小。
   
5. **ItemTipsLineHeightScale**  
   ItemTips的行高缩放大小。
   
6. **ItemTipsAnchor**  
   用空格或逗号分隔的一个二维向量，决定ItemTips相对于父级的锚点位置。左上角为0 0，右下角是1 1。
   
7. **ItemTipsPivot**  
   用空格或逗号分隔的一个二维向量，定义ItemTips的定位基准点。左上角为0 0，右下角是1 1。
   
8. **ItemTipsSourceBitmapFontBaseLine**  
   ItemTips的字体如果由位图字体生成，可以在此控制位图字体的基准线。

## **4. Detailed Description of AutoTranslate Features**

### Command
- autotranslate save_cache \[cache_json_name\]
Save the translation cache to \[cache_json_name\], which defaults to CachedTranslations.json.
- autotranslate load_cache \[cache_json_name\]
Load the translation cache from \[cache_json_name\], which defaults to CachedTranslations.json.

### General

1. **TranslationAPI**  
   Select the translation API to use. Supported APIs include Tencent Translation API, Baidu Translation API, and Microsoft Azure Translation API. As of January 29, 2025, the free quotas are: Tencent 5 million characters/month, Baidu 1 million characters/month, Azure 2 million characters/month. Please refer to the official documentation for more details.

2. **ToggleTranslationKeyBinding**  
   The key binding of toggling translation.

3. **RegexForFullTextNeedToTranslate**  
   A regular expression that matches multiline text. If the expression evaluates to true, the entire multiline text will be preserved for translation. This helps filter out unnecessary translation requests and save tranlation quotas. Example RegexForFullTextNeedToTranslate regex: `^(?!Enter the Gungeon).*$`

4. **RegexForEachLineNeedToTranslate**  
   A regular expression that checks each line in multiline text. If any line matches, the whole multiline text will be preserved for translation. This also helps filter out unnecessary translation requests and save tranlation quotas. Example RegexForEachLineNeedToTranslate regex (fit for Chinese): `^(?![@#])(?=\S)(?!^[\d\p{P}]+$)(?!.*[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]).*$`

5. **RegexForIgnoredSubstringWithinText**  
   A regular expression that matches substrings within text that should be ignored. Please use non-capturing groups. This typically includes elements like special image tags or escape sequences that need custom handling. Example RegexForIgnoredSubstringWithinText regex (fit for Chinese): `(?:\[color\s+[^\]]+\])|(?:\[sprite\s+[^\]]+\])|(?:\[/color\])|(?:\{[^}]*\})|(?:\^[\w\d]{9})|(?:[\u4e00-\u9fa5\u3000-\u303F\uFF00-\uFFEF]+)|(?:<color=[^>]+>)|(?:</color>)|(?:^\s*[\d\p{P}]+\s*$)|(?:[<>\[\]])|(?:@[a-fA-F0-9]{6})`

6. **MaxBatchCharacterCount**  
   The maximum count of batch data characters for processing. If the translation API prompts that a single request is too long, please reduce this value.

7. **MaxBatchTextCount**  
   The maximum count of batch data texts for processing. A value of 0 indicates no restriction.

8. **MaxRetryCount**  
   The maximum number of retry attempts when an error occurs during a translation request.

9. **RetryInterval**  
   The interval of retries when an error occurs.

10. **TranslationCacheCapacity**  
   The maximum cache size for storing translations.

11. **PresetTranslations**  
   The file name for preset translations. Use preset translation to reduce translation requests for common text during loading, leaving blank to indicate not using. The preset translation is a JSON file located in the same directory as the DLL. Separated by ';', files will be loaded sequentially.

12. **CachedTranslations**  
   The file name cached translations. The preset translation is a JSON file located in the same directory as the DLL.

13. **AutoSaveCachedTranslationsUponQuit**  
   Whether to automatically save cached translations upon quit. It will not take effect when quiting abnormally.

14. **LogRequestedTexts**  
   Whether to log the text requested for translation.

### Font

1. **OverrideFont**  
   Font used to override the font of the game. Choose according to the target language you need.

2. **FontAssetBundleName**   
   The name of the AssetBundle containing custom fonts. Located in the same directory as the DLL.

3. **CustomDfFontName**  
   The name of the custom DF font to use. Include it within the FontAssetBundle.

4. **CustomTk2dFontName**  
   The name of the custom tk2d font to use. Include it within the FontAssetBundle.

5. **RegexForDfTokenizer**  
   A regular expression used to generate tokens for the DF tokenizer. Tokens help control the automatic line breaks for text. For example, whether to break lines after each character or after each word. Refer to the default example and modify mainly the text-related components. Example RegexForDfTokenizer regex: `(?<StartTag>\[(?<StartTagName>(color|sprite))\s*(?<AttributeValue>[^\]\s]+)?\])|(?<EndTag>\[\/(?<EndTagName>color|sprite)\])|(?<Newline>\r?\n)|(?<Whitespace>\s+)|(?<Text>[a-zA-Z0-9]+|.)`

    - **StartTag**: The start of a tag.
    - **TagName**: The name of the tag.
    - **AttributeValue**: The value of the tag attribute.
    - **EndTag**: The end of a tag.
    - **Newline**: A newline character.
    - **Whitespace**: Spaces or tabs.
    - **Text**: The smallest text block that cannot break in the middle; line breaks can only occur after it.

6. **DfTextScaleExpandThreshold**  
   Df TextScale below this value will be expanded. Negative indicates non effectiveness.

7. **DfTextScaleExpandToValue**  
   How much is the Df TextScale below the threshold expanded to.

### Requested Character Count

1. **ShowRequestedCharacterCount**  
   Show requested character count.

2. **RequestedCharacterCountAlertThreshold**  
   The alert threshold of the count of characters requested for translation. When this count is about to exceed, translation will be paused.

3. **ToggleRequestedCharacterCountKeyBinding**  
   The key binding of toggling the display of requested character count.

4. **CountLabelAnchor**  
   A two-dimensional vector separated by spaces or commas, which determines where the count label is anchored relative to its parent. The top left corner is 0 0, and the bottom right corner is 1 1.

5. **CountLabelPivot**  
   A two-dimensional vector separated by spaces or commas, which defines the pivot point of the count label for positioning. The top left corner is 0 0, and the bottom right corner is 1 1.


### Compatibility

1. **TranslateTextsOfItemTipsMod**  
   Translate the text within the ItemTipsMod.

2. **RegexForItemTipsModTokenizer**  
   A regular expression used for generating tokens from ItemTipsMod. Token is used to handle the automatic line break position of text. Whether to wrap each word or to wrap after each word. Example RegexForItemTipsModTokenizer regex: `(?:<color=[^>]+?>|</color>|[a-zA-Z0-9]+|\s+|.)`

3. **ItemTipsFontScale**  
   The font scale of ItemTips.
   
4. **ItemTipsBackgroundWidthScale**  
   The width scale of ItemTips background.
   
5. **ItemTipsLineHeightScale**  
   The width scale of ItemTips line height.
   
6. **ItemTipsAnchor**  
   A two-dimensional vector separated by spaces or commas, which determines where ItemTips is anchored relative to its parent. The top left corner is 0 0, and the bottom right corner is 1 1.
   
7. **ItemTipsPivot**  
   A two-dimensional vector separated by spaces or commas, which defines the pivot point of ItemTips for positioning. The top left corner is 0 0, and the bottom right corner is 1 1.
   
8. **ItemTipsSourceBitmapFontBaseLine**  
   If the font used by ItemTips is generated from a bitmap font, you can adjust the baseline of the bitmap font here.


---

## **5. 字体相关**

若默认字体不支持所需语言的字符，可以使用自定义字体。详见GitHub页面。

## **5. Font-related**

If the default font does not support the characters required for a specific language, you can use a custom font. For more details, please refer to the GitHub page.
