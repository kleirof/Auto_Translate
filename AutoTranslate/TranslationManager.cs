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

        private bool translateOn;

        private LRUCache<string, string> translationCache;
        private LRUCache<string, int> fullTextCache;
        private bool isProcessingQueue = false;
        private Queue<TranslationQueueElement> translationQueue;

        private ITranslationService translationService;

        private List<string> batchSubTexts;
        private Dictionary<string, List<string>> batchSplitMap;
        private Dictionary<string, string> batchTranslationDictionary;
        private HashSet<string> batchUniqueSubTexts;
        private List<string> batchTranslatedParts;
        private List<string> uniqueTexts;
        private Dictionary<string, List<TextObject>> textControlMap;
        private Dictionary<TextObject, List<string>> controlTextMap;

        private Regex fullTextRegex;
        private Regex eachLineRegex;
        private Regex ignoredSubstringWithinTextRegex;

        private string[] newLineSymbols = new string[] { "\r\n", "\r", "\n" };
        private char[] semicolon = new char[] { ';' };

        private StringBuilder translatedTextBuilder;

        internal static TranslationManager instance;

        private int requestedCharacterCount = 0;

        internal bool exceededThreshold = false;

        private List<string> finalChunks;

        private StringBuilder resultBuilder;

        private string cachedString;
        private TextObject cachedObject;

        public void Update()
        {
            if (Input.GetKeyDown(config.ToggleTranslationKeyBinding))
            {
                ToggleTranslate();
                SetStatusLabelText();
            }
        }

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
                case AutoTranslateModule.TranslationAPIType.Llm:
                    translationService = new LlmTranslationService(config);
                    break;
            }

            fullTextCache = new LRUCache<string, int>(64, 64);
            translationCache = new LRUCache<string, string>(config.TranslationCacheCapacity, Math.Min(config.TranslationCacheCapacity, 1024));
            translationQueue = new Queue<TranslationQueueElement>(1024);

            if (!string.IsNullOrEmpty(config.PresetTranslations))
            {
                var paths = config.PresetTranslations
                    .Split(semicolon, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => TextProcessor.TrimOnlyIfNeeded(p))
                    .Where(p => !string.IsNullOrEmpty(p));

                foreach (var relativePath in paths)
                {
                    string fullPath = Path.Combine(
                        ETGMod.FolderPath(AutoTranslateModule.instance),
                        relativePath);

                    try
                    {
                        ReadAndRestoreTranslationCache(fullPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"加载预设翻译文件失败 Failed to load the preset translation file [{relativePath}]: {ex.Message}");
                    }
                }
            }

            if (config.FilterForFullTextNeedToTranslate == AutoTranslateModule.FilterForFullTextNeedToTranslateType.CustomRegex)
            {
                if (config.RegexForFullTextNeedToTranslate != string.Empty)
                    fullTextRegex = new Regex(config.RegexForFullTextNeedToTranslate, RegexOptions.Singleline | RegexOptions.Compiled);
            }

            if (config.FilterForEachLineNeedToTranslate == AutoTranslateModule.FilterForEachLineNeedToTranslateType.CustomRegex)
            {
                if (config.RegexForEachLineNeedToTranslate != string.Empty)
                    eachLineRegex = new Regex(config.RegexForEachLineNeedToTranslate, RegexOptions.Singleline | RegexOptions.Compiled);
            }

            if (config.FilterForIgnoredSubstringWithinText == AutoTranslateModule.FilterForIgnoredSubstringWithinTextType.CustomRegex)
            {
                if (config.RegexForIgnoredSubstringWithinText != string.Empty)
                    ignoredSubstringWithinTextRegex = new Regex(config.RegexForIgnoredSubstringWithinText, RegexOptions.Multiline | RegexOptions.Compiled);
            }

            batchSubTexts = new List<string>(128);
            batchSplitMap = new Dictionary<string, List<string>>(128);
            batchTranslationDictionary = new Dictionary<string, string>(128);
            batchUniqueSubTexts = new HashSet<string>();
            batchTranslatedParts = new List<string>(128);
            uniqueTexts = new List<string>(128);
            textControlMap = new Dictionary<string, List<TextObject>>(128);
            translatedTextBuilder = new StringBuilder(1024);
            controlTextMap = new Dictionary<TextObject, List<string>>(128);

            finalChunks = new List<string>(128);

            resultBuilder = new StringBuilder(1024);

            translateOn = config.isConfigValid;

            StartCoroutine(OnStatusLabelInitialized());
        }

        internal void ToggleTranslate()
        {
            translateOn = !translateOn;
            Debug.Log($"AutoTranslate切换为{(translateOn ? "ON" : "OFF")}。AutoTranslate toggled to {(translateOn ? "ON" : "OFF")}.");
        }

        internal void ForceSetTranslte(bool value)
        {
            translateOn = value;
            Debug.Log($"AutoTranslate强制设置为{(translateOn ? "ON" : "OFF")}。AutoTranslate forcefully set to {(translateOn ? "ON" : "OFF")}.");
        }

        private void SetStatusLabelText()
        {
            StatusLabelController.instance?.SetText($"AT: {requestedCharacterCount}  Now {(translateOn ? "ON" : "OFF")} ({config.ToggleTranslationKeyBinding})");
        }

        private IEnumerator OnStatusLabelInitialized()
        {
            while (StatusLabelController.instance == null)
            {
                yield return null;
            }
            SetStatusLabelText(); 
            if (exceededThreshold == true)
                StatusLabelController.instance?.SetHighlight();

            yield break;
        }

        private static bool FullTextFilter(string text)
        {
            return TextProcessor.StartsWithString(text, 0, "Enter the Gungeon");
        }

        private bool HasLineMatchingFilter(string text)
        {
            int lineStart = 0;
            int length = text.Length;

            for (int i = 0; i <= length; i++)
            {
                bool isLineEnd = i == length || text[i] == '\n' || text[i] == '\r';

                if (isLineEnd && i > lineStart)
                {
                    if (CheckLineSegment(text, lineStart, i - lineStart))
                        return true;
                }

                if (isLineEnd && i < length)
                {
                    if (text[i] == '\r' && i + 1 < length && text[i + 1] == '\n')
                        i++;

                    lineStart = i + 1;
                }
            }

            return false;
        }

        private static bool CheckLineSegment(string text, int start, int length)
        {
            if (length > 0 && (text[start] == '@' || text[start] == '#'))
                return false;

            bool foundValidChar = false;

            for (int i = 0; i < length; i++)
            {
                char c = text[start + i];

                if (char.IsWhiteSpace(c))
                    continue;

                if (TextProcessor.IsChineseChar(c))
                    return false;

                if (char.IsDigit(c) || char.IsPunctuation(c))
                    continue;

                foundValidChar = true;
            }

            return foundValidChar;
        }

        private bool NeedToTranslate(string text)
        {
            if (TextProcessor.IsNullOrWhiteSpace(text))
                return false;

            if (config.FilterForFullTextNeedToTranslate == AutoTranslateModule.FilterForFullTextNeedToTranslateType.CustomRegex)
            {
                if (fullTextRegex != null && !fullTextRegex.IsMatch(text))
                    return false;
            }
            else
            {
                if (FullTextFilter(text))
                    return false;
            }


            if (config.FilterForEachLineNeedToTranslate == AutoTranslateModule.FilterForEachLineNeedToTranslateType.CustomRegex)
            {
                if (eachLineRegex != null)
                {
                    string[] lines = text.Split(newLineSymbols, StringSplitOptions.None);
                    foreach (string line in lines)
                    {
                        if (eachLineRegex.IsMatch(line))
                            return true;
                    }
                    return false;
                }
            }
            else
            {
                return HasLineMatchingFilter(text);
            }
            return true;
        }

        private IEnumerator ProcessTranslationQueue()
        {
            isProcessingQueue = true;

            for (; ; )
            {
                while (!translateOn || translationQueue.Count == 0)
                {
                    yield return null;
                }

                DeduplicateTexts();
                yield return null;

                if (uniqueTexts.Count > 0)
                {
                    int count = GenerateBatch();
                    yield return null;
                    yield return StartCoroutine(TranslateBatchCoroutine(count));
                }

                yield return null;
            }
        }

        private void DeduplicateTexts()
        {
            uniqueTexts.Clear();
            textControlMap.Clear();

            int totalCharacterCount = 0;

            while (translationQueue.Count > 0 && totalCharacterCount < config.MaxBatchCharacterCount)
            {
                var translationRequest = translationQueue.Peek();
                string text = translationRequest.text;
                TextObject textObject = translationRequest.textObject;

                if (TextProcessor.IsNullOrWhiteSpace(text))
                {
                    translationQueue.Dequeue();
                    continue;
                }

                if (totalCharacterCount + text.Length > config.MaxBatchCharacterCount)
                {
                    break;
                }

                int totalRemoveLength = 0;
                foreach (var uniqueText in uniqueTexts)
                {
                    if (TextProcessor.StartsWithString(text, totalRemoveLength, uniqueText) &&
                        totalRemoveLength + uniqueText.Length < text.Length &&
                        textControlMap.TryGetValue(text, out var list) &&
                        list.Contains(textObject))
                    {
                        totalRemoveLength += uniqueText.Length;
                    }
                }

                if (totalRemoveLength > 0)
                    text = text.Substring(totalRemoveLength);

                if (!textControlMap.ContainsKey(text))
                {
                    uniqueTexts.Add(text);
                    totalCharacterCount += text.Length;
                    textControlMap[text] = Pools.listTextObjectPool.Get();
                }
                textControlMap[text].Add(textObject);

                translationQueue.Dequeue();

                if (config.MaxBatchTextCount > 0 && uniqueTexts.Count >= config.MaxBatchTextCount)
                {
                    break;
                }
            }
        }

        private static void AddSegIfNotEmpty(List<string> outlist, string text, int start, int end)
        {
            if (end <= start)
                return;

            int left = start;
            int right = end - 1;

            while (left <= right && char.IsWhiteSpace(text[left]))
                left++;

            while (right >= left && char.IsWhiteSpace(text[right]))
                right--;

            if (right < left)
                return;

            int length = right - left + 1;
            string seg = text.Substring(left, length);
            outlist.Add(seg);
        }

        public static void SubstringFilter(List<string> outlist, string text, bool keepMatches)
        {
            if (outlist == null) return;
            if (string.IsNullOrEmpty(text))
                return;

            int len = text.Length, i = 0, segmentStart = 0;

            while (i < len)
            {
                int matchLen = 0;

                if (text[i] == '[' && TextProcessor.StartsWithString(text, i, "[color "))
                {
                    int closeIdx = TextProcessor.IndexOfChar(text, ']', i + 7);
                    if (closeIdx != -1)
                        matchLen = closeIdx - i + 1;
                }
                else if (text[i] == '[' && TextProcessor.StartsWithString(text, i, "[sprite "))
                {
                    int closeIdx = TextProcessor.IndexOfChar(text, ']', i + 8);
                    if (closeIdx != -1)
                        matchLen = closeIdx - i + 1;
                }
                else if (TextProcessor.StartsWithString(text, i, "[/color]"))
                {
                    matchLen = 8;
                }
                else if (text[i] == '{')
                {
                    int closeIdx = TextProcessor.IndexOfChar(text, '}', i + 1);
                    if (closeIdx != -1)
                        matchLen = closeIdx - i + 1;
                }
                else if (text[i] == '^' && i + 9 < len)
                {
                    bool valid = true;
                    for (int k = 1; k <= 9; k++)
                    {
                        char c = text[i + k];
                        if (!(char.IsLetterOrDigit(c) || c == '_'))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid) matchLen = 10;
                }
                else if (TextProcessor.IsChineseChar(text[i]))
                {
                    int j = i;
                    while (j < len && TextProcessor.IsChineseChar(text[j])) j++;
                    matchLen = j - i;
                }
                else if (text[i] == '<' && TextProcessor.StartsWithString(text, i, "<color="))
                {
                    int closeIdx = TextProcessor.IndexOfChar(text, '>', i + 7);
                    if (closeIdx != -1)
                        matchLen = closeIdx - i + 1;
                }
                else if (TextProcessor.StartsWithString(text, i, "</color>"))
                {
                    matchLen = 8;
                }
                else if ((i == 0 || text[i - 1] == '\n' || text[i - 1] == '\r'))
                {
                    int lineStart = i;
                    int lineEnd = i;
                    while (lineEnd < len && text[lineEnd] != '\r' && text[lineEnd] != '\n')
                        lineEnd++;

                    int left = lineStart;
                    int right = lineEnd - 1;

                    while (left <= right && char.IsWhiteSpace(text[left]))
                        left++;

                    while (right >= left && char.IsWhiteSpace(text[right]))
                        right--;

                    if (right >= left)
                    {
                        bool allDigitsOrPunct = true;
                        for (int idx = left; idx <= right; idx++)
                        {
                            char c = text[idx];
                            if (!char.IsDigit(c) && !char.IsPunctuation(c))
                            {
                                allDigitsOrPunct = false;
                                break;
                            }
                        }

                        if (allDigitsOrPunct)
                            matchLen = lineEnd - i;
                    }
                }

                if (matchLen == 0 && (text[i] == '<' || text[i] == '>' || text[i] == '[' || text[i] == ']'))
                    matchLen = 1;

                if (matchLen == 0 && text[i] == '@' && i + 6 < len)
                {
                    bool valid = true;
                    for (int k = 1; k <= 6; k++)
                    {
                        if (!Uri.IsHexDigit(text[i + k]))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid) matchLen = 7;
                }

                if (matchLen > 0)
                {
                    if (keepMatches)
                    {
                        AddSegIfNotEmpty(outlist, text, segmentStart, i);
                        outlist.Add(text.Substring(i, matchLen));
                        segmentStart = i + matchLen;
                    }
                    else
                    {
                        AddSegIfNotEmpty(outlist, text, segmentStart, i);
                        segmentStart = i + matchLen;
                    }
                    i += matchLen;
                }
                else
                {
                    i++;
                }
            }

            AddSegIfNotEmpty(outlist, text, segmentStart, len);
        }

        private int GenerateBatch()
        {
            batchSubTexts.Clear();
            batchSplitMap.Clear();
            batchTranslationDictionary.Clear();
            batchUniqueSubTexts.Clear();

            int batchCharacterCount = 0;

            foreach (var text in uniqueTexts)
            {
                List<string> parts = Pools.listStringPool.Get();
                if (config.FilterForIgnoredSubstringWithinText == AutoTranslateModule.FilterForIgnoredSubstringWithinTextType.CustomRegex)
                {
                    if (config.RegexForIgnoredSubstringWithinText != null)
                        parts.AddRange(ignoredSubstringWithinTextRegex.Split(text)
                                            .Select(part => TextProcessor.TrimOnlyIfNeeded(part))
                                            .Where(part => !TextProcessor.IsNullOrWhiteSpace(part)));
                    else
                        parts.Add(TextProcessor.TrimOnlyIfNeeded(text));
                }
                else
                {
                    SubstringFilter(parts, text, keepMatches: false);
                }

                bool allTranslated = true;
                batchTranslatedParts.Clear();

                foreach (var part in parts)
                {
                    if (translationCache.TryGetValue(part, out string cachedTranslation))
                    {
                        batchTranslationDictionary[part] = cachedTranslation;
                    }
                    else
                    {
                        allTranslated = false;
                        if (batchUniqueSubTexts.Add(part))
                        {
                            batchTranslatedParts.Add(part);
                            batchCharacterCount += part.Length;
                        }
                    }
                }

                if (allTranslated)
                {
                    translatedTextBuilder.Length = 0;
                    translatedTextBuilder.Append(text);
                    foreach (var part in parts)
                    {
                        int startIndex = TextProcessor.IndexOfString(translatedTextBuilder, part);
                        if (startIndex != -1 && batchTranslationDictionary.TryGetValue(part, out var translatedPart))
                        {
                            translatedTextBuilder.Replace(part, translatedPart, startIndex, part.Length);
                        }
                    }

                    string translatedText = translatedTextBuilder.ToString();

                    if (textControlMap.TryGetValue(text, out var controls))
                    {
                        foreach (var control in controls)
                        {
                            OnTranslationFinishTextObject(control, text, translatedText, true);
                            RemoveTextFromControlTextMap(control, text);
                            TextObjectManager.SafeRelease(control);
                        }
                        Pools.listTextObjectPool.Return(controls);
                        textControlMap.Remove(text);
                    }
                    Pools.listStringPool.Return(parts);
                }
                else
                {
                    batchSplitMap[text] = parts;
                    batchSubTexts.AddRange(batchTranslatedParts);
                }
            }
            return batchCharacterCount;
        }

        private IEnumerator TranslateBatchCoroutine(int batchCharacterCount)
        {
            if (batchSubTexts.Count == 0)
            {
                yield break;
            }

            if (exceededThreshold == false && config.RequestedCharacterCountAlertThreshold > 0 && requestedCharacterCount + batchCharacterCount > config.RequestedCharacterCountAlertThreshold)
            {
                exceededThreshold = true;
                StatusLabelController.instance?.SetHighlight();
                ForceSetTranslte(false);
                SetStatusLabelText();
            }

            while (!translateOn)
            {
                yield return null;
            }

            if (config.LogRequestedTexts)
            {
                Debug.Log("请求的文本 RequestedTexts:  {");
                foreach (var subText in batchSubTexts)
                    Debug.Log("      " + subText);
                Debug.Log("}");
            }
            yield return null;

            yield return StartCoroutine(translationService.StartTranslation(
                batchSubTexts,
                (translatedTexts) =>
                {
                    try
                    {
                        if (translatedTexts == null || translatedTexts.Count != batchSubTexts.Count)
                        {
                            if (translatedTexts != null)
                            {
                                Debug.LogError("翻译结果数量与请求数量不匹配！The number of translation results does not match the number of requests!");
                                Debug.LogError("请求 Requests：");
                                foreach (var subText in batchSubTexts)
                                    Debug.Log("      " + subText);
                                Debug.LogError("结果 Results：");
                                foreach (var subText in translatedTexts)
                                    Debug.Log("      " + subText);
                            }

                            foreach (var originalText in uniqueTexts)
                            {
                                if (!batchSplitMap.TryGetValue(originalText, out var parts))
                                {
                                    continue;
                                }

                                Pools.listStringPool.Return(parts);
                                batchSplitMap.Remove(originalText);
                                if (textControlMap.TryGetValue(originalText, out var controls))
                                {
                                    foreach (var control in controls)
                                    {
                                        RemoveTextFromControlTextMap(control, originalText);
                                        TextObjectManager.SafeRelease(control);
                                    }
                                    Pools.listTextObjectPool.Return(controls);
                                    textControlMap.Remove(originalText);
                                }
                            }
                            return;
                        }

                        for (int i = 0; i < batchSubTexts.Count; i++)
                        {
                            string processedText = translatedTexts[i].Replace("\\n", "\n");
                            batchTranslationDictionary[batchSubTexts[i]] = processedText;
                            translationCache.Set(batchSubTexts[i], processedText);
                        }
                        requestedCharacterCount += batchCharacterCount;
                        SetStatusLabelText();

                        foreach (var originalText in uniqueTexts)
                        {
                            if (!batchSplitMap.TryGetValue(originalText, out var parts))
                                continue;

                            translatedTextBuilder.Length = 0;
                            translatedTextBuilder.Append(originalText);

                            foreach (var part in parts)
                            {
                                int startIndex = TextProcessor.IndexOfString(translatedTextBuilder, part);
                                if (startIndex != -1 && batchTranslationDictionary.TryGetValue(part, out var translatedPart))
                                {
                                    translatedTextBuilder.Replace(part, translatedPart, startIndex, part.Length);
                                }
                            }
                            Pools.listStringPool.Return(parts);
                            batchSplitMap.Remove(originalText);

                            string translatedText = translatedTextBuilder.ToString();

                            if (textControlMap.TryGetValue(originalText, out var controls))
                            {
                                foreach (var control in controls)
                                {
                                    OnTranslationFinishTextObject(control, originalText, translatedText, true);
                                    RemoveTextFromControlTextMap(control, originalText);
                                    TextObjectManager.SafeRelease(control);
                                }
                                Pools.listTextObjectPool.Return(controls);
                                textControlMap.Remove(originalText);
                            }
                        }
                    }
                    finally
                    {
                        if (translatedTexts != null)
                        {
                            Pools.listStringPool.Return(translatedTexts);
                        }
                    }
                })
            );
        }

        public void AddTranslationRequest(string text, object control)
        {
            if (text == null || control == null)
                return;
            if (!translateOn)
                return;
            if (cachedObject != null && cachedObject.IsAlive && control == cachedObject.Object && text.Equals(cachedString))
                return;

            if (!NeedToTranslate(text))
                return;

            if (translationCache.TryGetValue(text, out string translatedText))
            {
                OnTranslationFinish(control, text, translatedText, false);
                return;
            }

            TextObject textObject = TextObjectManager.GetTextObject(control);

            cachedString = text;
            cachedObject = textObject;

            if (text.Length <= config.MaxBatchCharacterCount)
            {
                text = RemovePrefixFromText(text, textObject);
                if (TextProcessor.IsNullOrWhiteSpace(text))
                    return;
                SubmitRequest(textObject, text);
                if (!isProcessingQueue)
                    StartCoroutine(ProcessTranslationQueue());
                return;
            }

            finalChunks.Clear();

            if (config.FilterForIgnoredSubstringWithinText == AutoTranslateModule.FilterForIgnoredSubstringWithinTextType.CustomRegex)
            {
                if (ignoredSubstringWithinTextRegex == null)
                    AddChunks(finalChunks, text);
                else
                {
                    MatchCollection matches = ignoredSubstringWithinTextRegex.Matches(text);
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
                }
            }
            else
            {
                List<string> parts = Pools.listStringPool.Get();
                SubstringFilter(parts, text, keepMatches: true);
                foreach (var part in parts)
                    AddChunks(finalChunks, part);
                Pools.listStringPool.Return(parts);
            }

            foreach (var chunk in finalChunks)
            {
                string chunkToQueue = RemovePrefixFromText(chunk, textObject);
                if (TextProcessor.IsNullOrWhiteSpace(chunkToQueue))
                    continue;
                SubmitRequest(textObject, chunkToQueue);
            }

            if (finalChunks.Count > 1)
                textObject.Retain(finalChunks.Count - 1);

            if (!isProcessingQueue)
                StartCoroutine(ProcessTranslationQueue());
        }

        private void SubmitRequest(TextObject textObject, string text)
        {
            AddTextToControlTextMap(textObject, text);
            translationQueue.Enqueue(new TranslationQueueElement(text, textObject));
            if (fullTextCache.TryGetValue(text, out int count))
                fullTextCache.Set(text, count + 1);
            else
                fullTextCache.Set(text, 1);
        }

        private string RemovePrefixFromText(string text, TextObject textObject)
        {
            if (controlTextMap.TryGetValue(textObject, out var prevTexts))
            {
                int totalRemoveLength = 0;

                foreach (var prevText in prevTexts)
                {
                    if (TextProcessor.StartsWithString(text, totalRemoveLength, prevText) &&
                        totalRemoveLength + prevText.Length < text.Length)
                    {
                        totalRemoveLength += prevText.Length;
                    }
                }

                if (totalRemoveLength > 0)
                    return text.Substring(totalRemoveLength);
            }

            return text;
        }

        private void AddTextToControlTextMap(TextObject textObject, string text)
        {
            if (!controlTextMap.TryGetValue(textObject, out List<string> translatedTexts))
            {
                translatedTexts = Pools.listStringPool.Get();
                controlTextMap[textObject] = translatedTexts;
            }

            if (!translatedTexts.Contains(text))
                translatedTexts.Add(text);
        }

        private void RemoveTextFromControlTextMap(TextObject textObject, string originalText)
        {
            if (controlTextMap.Count == 0)
                return;


            if (controlTextMap.TryGetValue(textObject, out List<string> translatedTexts))
            {
                for (int i = translatedTexts.Count - 1; i >= 0; i--)
                {
                    if (translatedTexts[i].Equals(originalText))
                        translatedTexts.RemoveAt(i);
                }

                if (translatedTexts.Count == 0)
                {
                    Pools.listStringPool.Return(translatedTexts);
                    controlTextMap.Remove(textObject);
                }
            }
        }

        private void AddChunks(List<string> chunks, string text)
        {
            int size = config.MaxBatchCharacterCount;
            for (int i = 0; i < text.Length; i += size)
                chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        }

        private void OnTranslationFinishTextObject(TextObject textObject, string original, string result, bool setFullTextCached)
        {
            if (textObject == null || result == null)
                return;

            object target = textObject.Object;
            if (target == null)
                return;

            OnTranslationFinish(target, original, result, setFullTextCached);
        }

        private void OnTranslationFinish(object control, string original, string result, bool setFullTextCached)
        {
            if (control == null || result == null)
                return;

            if (setFullTextCached && fullTextCache.TryGetValue(original, out int count) && count > 2)
                translationCache.Set(original, result);

            resultBuilder.Length = 0;
            if (control is dfLabel dfLabel)
            {
                if (dfLabel == null || dfLabel.text == null || !dfLabel.isActiveAndEnabled)
                    return;

                string originalText = dfLabel.text;
                int startIndex = TextProcessor.IndexOfString(dfLabel.text, original);
                if (startIndex != -1)
                {
                    bool isDefaultLabel = dfLabel.gameObject?.name == "DefaultLabel";
                    float originalHeight = dfLabel.Height;

                    dfFontBase fontBase = FontManager.instance.dfFontBase;
                    if (fontBase != null && dfLabel.Font != fontBase)
                    {
                        dfLabel.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfLabel.Atlas = dfFont.Atlas;

                        if (config.DfTextScaleExpandThreshold >= 0 && dfLabel.TextScale < config.DfTextScaleExpandThreshold)
                            dfLabel.TextScale = config.DfTextScaleExpandToValue;
                    }

                    resultBuilder.Append(originalText, 0, startIndex)
                      .Append(result)
                      .Append(originalText, startIndex + original.Length, originalText.Length - (startIndex + original.Length));
                    dfLabel.text = resultBuilder.ToString();

                    dfLabel.Invalidate();
                    if (isDefaultLabel)
                    {
                        Vector3 position = dfLabel.RelativePosition;
                        dfLabel.RelativePosition = new Vector3(position.x, position.y - dfLabel.Height + originalHeight, position.z);
                    }
                }
            }
            else if (control is dfButton dfButton)
            {
                if (dfButton == null || dfButton.text == null || !dfButton.isActiveAndEnabled)
                    return;

                string originalText = dfButton.text;
                int startIndex = TextProcessor.IndexOfString(originalText, original);
                if (startIndex != -1)
                {
                    dfFontBase fontBase = FontManager.instance.dfFontBase;
                    if (fontBase != null && dfButton.Font != fontBase)
                    {
                        dfButton.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfButton.Atlas = dfFont.Atlas;

                        if (config.DfTextScaleExpandThreshold >= 0 && dfButton.TextScale < config.DfTextScaleExpandThreshold)
                            dfButton.TextScale = config.DfTextScaleExpandToValue;
                    }

                    resultBuilder.Append(originalText, 0, startIndex)
                      .Append(result)
                      .Append(originalText, startIndex + original.Length, originalText.Length - (startIndex + original.Length));
                    dfButton.text = resultBuilder.ToString();

                    dfButton.Invalidate();
                }
            }
            else if (control is SGUI.SLabel sLabel)
            {
                if (sLabel == null || sLabel.Text == null)
                    return;

                string originalText = FontManager.instance.itemTipsModuleText;
                int startIndex = TextProcessor.IndexOfString(originalText, original);
                if (startIndex != -1)
                {
                    resultBuilder.Append(originalText, 0, startIndex)
                      .Append(result)
                      .Append(originalText, startIndex + original.Length, originalText.Length - (startIndex + original.Length));

                    sLabel.Text = FontManager.instance.WrapText(resultBuilder.ToString(), out Vector2 sizeVector);
                    sLabel.Size = sizeVector;
                    FontManager.instance.ItemTipsReposition(sLabel);
                }
            }
            else if (control is tk2dTextMesh textMesh)
            {
                if (textMesh == null || textMesh.data == null || textMesh.data.text == null)
                    return;

                string originalText = textMesh.data.text;
                int startIndex = TextProcessor.IndexOfString(originalText, original);
                if (startIndex != -1)
                {
                    tk2dFontData tk2dFont = FontManager.instance.tk2dFont;
                    if (tk2dFont != null && textMesh.font != tk2dFont)
                        FontManager.SetTextMeshFont(textMesh, tk2dFont);

                    resultBuilder.Append(originalText, 0, startIndex)
                      .Append(result)
                      .Append(originalText, startIndex + original.Length, originalText.Length - (startIndex + original.Length));
                    textMesh.data.text = resultBuilder.ToString();

                    textMesh.SetNeedUpdate(tk2dTextMesh.UpdateFlags.UpdateText);
                }
            }
        }

        public void ReadAndRestoreTranslationCache(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"文件 {filePath} 不存在。The file {filePath} does not exist.");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var pairs = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(json);

                if (pairs != null)
                {
                    foreach (var pair in pairs)
                    {
                        translationCache.Set(pair.Key, pair.Value);
                    }
                }

                Debug.Log($"从 {filePath} 成功加载 {pairs?.Count ?? 0} 条翻译。Successfully loaded {pairs?.Count ?? 0} translations from {filePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载失败 Loading failed: {ex.Message}");
            }
        }

        public void SaveTranslationCache(string filePath, int maxCacheItems)
        {
            try
            {
                List<KeyValuePair<string, string>> existingPairs = new List<KeyValuePair<string, string>>();
                if (File.Exists(filePath))
                {
                    string existingJson = File.ReadAllText(filePath);
                    existingPairs = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(existingJson)
                                  ?? new List<KeyValuePair<string, string>>();
                }

                List<KeyValuePair<string, string>> newPairs = translationCache. GetOrderedKeyValuePairsReverse();

                var mergedPairs = existingPairs
                    .Concat(newPairs)
                    .GroupBy(p => p.Key)
                    .Select(g => g.Last())
                    .ToList();

                if (mergedPairs.Count > maxCacheItems)
                {
                    mergedPairs = mergedPairs.Skip(mergedPairs.Count - maxCacheItems).ToList();
                }

                string json = JsonConvert.SerializeObject(mergedPairs, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, json);

                Debug.Log($"成功保存翻译缓存共 {mergedPairs.Count} 项到 {filePath}。 Translation cache of {mergedPairs.Count} items saved successfully to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存失败 Saving failed: {ex.Message}");
            }
        }
    }
}
