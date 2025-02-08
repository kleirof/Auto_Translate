using Newtonsoft.Json;
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
    class TranslationManager : MonoBehaviour
    {
        private AutoTranslateConfig config;

        private LRUCache<string, string> translationCache;
        private bool isProcessingQueue = false;
        private Queue<TranslationQueueElement> translationQueue;

        private ITranslationService translationService;

        private List<string> subTexts;
        private Dictionary<string, List<string>> splitMap;
        private Dictionary<string, string> translationDictionary;
        private HashSet<string> uniqueSubTexts;
        private List<string> untranslatedParts;
        private List<string> uniqueTexts;
        private Dictionary<string, List<object>> textControlMap;

        private const int countToNew = 200;

        private Regex fullTextRegex;
        private Regex eachLineRegex;

        private string[] newLineSymbols = new string[] { "\r\n", "\r", "\n" };

        private StringBuilder translatedTextBuilder;

        public static TranslationManager instance;

        public void Initialize()
        {
            instance = this;
            this.config = AutoTranslateModule.instance.config;

            switch (config.TranslationAPI)
            {
                case AutoTranslateModule.TranslationAPIType.Tencent:
                    translationService = new TencentTranslationService(config);
                    break;
                case AutoTranslateModule.TranslationAPIType.Baidu:
                    translationService = new BaiduTranslationService(config);
                    break;
                case AutoTranslateModule.TranslationAPIType.Azure:
                    translationService = new AzureTranslationService(config);
                    break;
            }

            translationCache = new LRUCache<string, string>(config.TranslationCacheCapacity);
            translationQueue = new Queue<TranslationQueueElement>();

            if (config.PresetTranslations != string.Empty)
            {
                string presetPath = Path.Combine(ETGMod.FolderPath(AutoTranslateModule.instance), config.PresetTranslations);
                ReadAndRestoreTranslationCache(presetPath);
            }

            if (config.RegexForFullTextNeedToTranslate != string.Empty)
                fullTextRegex = new Regex(config.RegexForFullTextNeedToTranslate, RegexOptions.Singleline);

            if (config.RegexForEachLineNeedToTranslate != string.Empty)
                eachLineRegex = new Regex(config.RegexForEachLineNeedToTranslate, RegexOptions.Singleline);

            subTexts = new List<string>();
            splitMap = new Dictionary<string, List<string>>();
            translationDictionary = new Dictionary<string, string>();
            uniqueSubTexts = new HashSet<string>();
            untranslatedParts = new List<string>();
            uniqueTexts = new List<string>();
            textControlMap = new Dictionary<string, List<object>>();
            translatedTextBuilder = new StringBuilder();
        }

        private bool NeedToTranslate(string text)
        {
            if (fullTextRegex != null && !fullTextRegex.IsMatch(text))
                return false;

            string[] lines;
            if (eachLineRegex != null)
            {
                lines = text.Split(newLineSymbols, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    if (eachLineRegex.IsMatch(line))
                        return true;
                }
                return false;
            }
            return true;
        }

        private IEnumerator ProcessTranslationQueue()
        {
            isProcessingQueue = true;

            while (true)
            {
                if (uniqueTexts.Count > countToNew)
                    uniqueTexts = new List<string>();
                else
                    uniqueTexts.Clear();

                if (textControlMap.Count > countToNew)
                    textControlMap = new Dictionary<string, List<object>>();
                else
                    textControlMap.Clear();

                int totalCharacterCount = 0;

                while (translationQueue.Count > 0 && totalCharacterCount < config.RequestBatchSize)
                {
                    var translationRequest = translationQueue.Peek();
                    string text = translationRequest.Text;
                    object control = translationRequest.Control;

                    if (IsNullOrWhiteSpace(text))
                    {
                        translationQueue.Dequeue();
                        continue;
                    }

                    if (totalCharacterCount + text.Length > config.RequestBatchSize)
                    {
                        break;
                    }

                    if (!textControlMap.ContainsKey(text))
                    {
                        uniqueTexts.Add(text);
                        totalCharacterCount += text.Length;
                        textControlMap[text] = new List<object>();
                    }
                    textControlMap[text].Add(control);

                    translationQueue.Dequeue();
                }

                if (uniqueTexts.Count > 0)
                {
                    yield return StartCoroutine(TranslateBatchCoroutine());
                }

                yield return null;
            }
        }

        private IEnumerator TranslateBatchCoroutine()
        {
            if (subTexts.Count > countToNew)
                subTexts = new List<string>();
            else
                subTexts.Clear();

            if (splitMap.Count > countToNew)
                splitMap = new Dictionary<string, List<string>>();
            else
                splitMap.Clear();

            if (translationDictionary.Count > countToNew)
                translationDictionary = new Dictionary<string, string>();
            else
                translationDictionary.Clear();

            if (uniqueSubTexts.Count > countToNew)
                uniqueSubTexts = new HashSet<string>();
            else
                uniqueSubTexts.Clear();

            if (untranslatedParts.Count > countToNew)
                untranslatedParts = new List<string>();

            string splitPattern = config.RegexForIgnoredSubstringWithinText;

            foreach (var text in uniqueTexts)
            {
                string[] parts;
                if (!string.IsNullOrEmpty(splitPattern))
                    parts = Regex.Split(text, splitPattern);
                else
                    parts = new string[] { text };

                parts = parts.Where(part => !IsNullOrWhiteSpace(part)).ToArray();

                bool allTranslated = true;
                untranslatedParts.Clear();

                foreach (var part in parts)
                {
                    if (translationCache.TryGetValue(part, out string cachedTranslation))
                    {
                        translationDictionary[part] = cachedTranslation;
                    }
                    else
                    {
                        allTranslated = false;
                        if (uniqueSubTexts.Add(part))
                        {
                            untranslatedParts.Add(part);
                        }
                    }
                }

                if (allTranslated)
                {
                    var translatedTextBuilder = new StringBuilder(text);
                    foreach (var part in parts)
                    {
                        if (translationDictionary.TryGetValue(part, out string translation))
                        {
                            translatedTextBuilder.Replace(part, translation);
                        }
                    }

                    string translatedText = translatedTextBuilder.ToString();

                    if (textControlMap.TryGetValue(text, out var controls))
                    {
                        foreach (var control in controls)
                        {
                            OnTranslationFinish(control, text, translatedText);
                        }
                    }
                }
                else
                {
                    splitMap[text] = parts.ToList();
                    subTexts.AddRange(untranslatedParts);
                }
            }

            if (subTexts.Count == 0)
            {
                yield break;
            }

            if (config.LogRequestedTexts)
            {
                Debug.Log("请求的文本 RequestedTexts:  {");
                foreach (var subText in subTexts)
                    Debug.Log("      " + subText);
                Debug.Log("}");
            }

            yield return StartCoroutine(translationService.StartTranslation(
                subTexts.ToArray(),
                (translatedTexts) =>
                {
                    if (translatedTexts != null)
                    {
                        if (translatedTexts.Length != subTexts.Count)
                        {
                            Debug.LogError("翻译结果数量与请求数量不匹配！");
                            return;
                        }

                        for (int i = 0; i < subTexts.Count; i++)
                        {
                            translationDictionary[subTexts[i]] = translatedTexts[i].Replace("\\n", "\n");
                            translationCache.Set(subTexts[i], translatedTexts[i]);
                        }

                        foreach (var originalText in uniqueTexts)
                        {
                            if (!splitMap.TryGetValue(originalText, out var parts)) continue;

                            translatedTextBuilder.Length = 0;
                            translatedTextBuilder.Append(originalText);

                            foreach (var part in parts)
                            {
                                int startIndex = translatedTextBuilder.ToString().IndexOf(part);
                                if (startIndex != -1 && translationDictionary.TryGetValue(part, out var translatedPart))
                                {
                                    translatedTextBuilder.Replace(part, translatedPart, startIndex, part.Length);
                                }
                            }

                            string translatedText = translatedTextBuilder.ToString();

                            if (textControlMap.TryGetValue(originalText, out var controls))
                            {
                                foreach (var control in controls)
                                {
                                    OnTranslationFinish(control, originalText, translatedText);
                                }
                            }
                        }
                    }
                })
            );
        }

        public void AddTranslationRequest(string text, object control)
        {
            if (!NeedToTranslate(text))
                return;

            if (translationCache.TryGetValue(text, out string translatedText))
            {
                OnTranslationFinish(control, text, translatedText);
                return;
            }

            if (text.Length <= config.RequestBatchSize)
            {
                translationQueue.Enqueue(new TranslationQueueElement(text, control));
                if (!isProcessingQueue)
                    StartCoroutine(ProcessTranslationQueue());
                return;
            }

            var regex = new Regex(config.RegexForIgnoredSubstringWithinText);
            var matches = regex.Matches(text);

            List<string> finalChunks = new List<string>();
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (lastIndex < match.Index)
                    AddChunks(finalChunks, text.Substring(lastIndex, match.Index - lastIndex));

                finalChunks.Add(match.Value);
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
                AddChunks(finalChunks, text.Substring(lastIndex));

            foreach (var chunk in finalChunks)
                translationQueue.Enqueue(new TranslationQueueElement(chunk, control));

            if (!isProcessingQueue)
                StartCoroutine(ProcessTranslationQueue());
        }

        private void AddChunks(List<string> chunks, string text)
        {
            int size = config.RequestBatchSize;
            for (int i = 0; i < text.Length; i += size)
                chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        }

        private void OnTranslationFinish(object textObject, string original, string result)
        {
            if (textObject == null || result == null)
                return;

            if (textObject is dfLabel dfLabel)
            {
                if (dfLabel == null || dfLabel.text == null || !dfLabel.isActiveAndEnabled)
                    return;

                string originalText = dfLabel.text;
                int startIndex = dfLabel.text.IndexOf(original);
                if (startIndex != -1)
                {
                    dfFontBase fontBase = FontManager.instance.dfFontBase;
                    if (fontBase != null && dfLabel.Font != fontBase)
                    {
                        dfLabel.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfLabel.Atlas = dfFont.Atlas;
                    }

                    if (config.DfTextScaleExpandThreshold >= 0 && dfLabel.TextScale < config.DfTextScaleExpandThreshold)
                        dfLabel.TextScale = config.DfTextScaleExpandToValue;

                    dfLabel.text = originalText.Substring(0, startIndex) +
                                   result +
                                   originalText.Substring(startIndex + original.Length);
                    dfLabel.OnTextChanged();
                }
            }
            else if (textObject is dfButton dfButton)
            {
                if (dfButton == null || dfButton.text == null || !dfButton.isActiveAndEnabled)
                    return;

                string originalText = dfButton.text;
                int startIndex = originalText.IndexOf(original);
                if (startIndex != -1)
                {
                    dfFontBase fontBase = FontManager.instance.dfFontBase;
                    if (fontBase != null && dfButton.Font != fontBase)
                    {
                        dfButton.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfButton.Atlas = dfFont.Atlas;
                    }

                    if (config.DfTextScaleExpandThreshold >= 0 && dfButton.TextScale < config.DfTextScaleExpandThreshold)
                        dfButton.TextScale = config.DfTextScaleExpandToValue;

                    dfButton.text = originalText.Substring(0, startIndex) +
                                    result +
                                    originalText.Substring(startIndex + original.Length);
                    dfButton.Invalidate();
                }
            }
            else if (textObject is SGUI.SLabel sLabel)
            {
                if (sLabel == null || sLabel.Text == null)
                    return;

                string originalText = FontManager.instance.itemTipsModuleText;
                int startIndex = originalText.IndexOf(original);
                if (startIndex != -1)
                {
                    string newText = originalText.Substring(0, startIndex) +
                                    result +
                                    originalText.Substring(startIndex + original.Length);

                    sLabel.Text = FontManager.instance.WrapText(newText, out Vector2 sizeVector);
                    sLabel.Size = sizeVector;
                }
            }
            else if (textObject is tk2dTextMesh textMesh)
            {
                if (textMesh == null || textMesh.data == null || textMesh.data.text == null)
                    return;

                string originalText = textMesh.data.text;
                int startIndex = originalText.IndexOf(original);
                if (startIndex != -1)
                {
                    tk2dFontData tk2dFont = FontManager.instance.tk2dFont;
                    if (tk2dFont != null && textMesh.font != tk2dFont)
                        FontManager.SetTextMeshFont(textMesh, tk2dFont);

                    textMesh.data.text = originalText.Substring(0, startIndex) +
                                         result +
                                         originalText.Substring(startIndex + original.Length);
                    textMesh.SetNeedUpdate(tk2dTextMesh.UpdateFlags.UpdateText);
                }

            }
        }

        private bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        public void ReadAndRestoreTranslationCache(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"加载预设翻译出现错误，文件不存在。Error loading preset translation, file does not exist.");
                    return;
                }
                string json = File.ReadAllText(filePath);

                var simpleDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                foreach (var entry in simpleDict)
                {
                    translationCache.Set(entry.Key, entry.Value);
                }

                Debug.Log($"加载预设翻译完毕。Loading preset translation completed.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载预设翻译出现错误。Error loading preset translation: {ex.Message}");
            }
        }
    }
}
