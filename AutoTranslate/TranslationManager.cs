﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private Dictionary<string, List<object>> textControlMap;
        private Dictionary<object, List<string>> controlTextMap;

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

        private ObjectPool<List<string>> listStringPool;
        private ObjectPool<List<object>> listObjectPool;
        private ObjectPool<TranslationQueueElement> translationQueueElementPool;

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

            fullTextCache = new LRUCache<string, int>(64);
            translationCache = new LRUCache<string, string>(config.TranslationCacheCapacity);
            translationQueue = new Queue<TranslationQueueElement>();

            if (!string.IsNullOrEmpty(config.PresetTranslations))
            {
                var paths = config.PresetTranslations
                    .Split(semicolon, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
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

            if (config.RegexForFullTextNeedToTranslate != string.Empty)
                fullTextRegex = new Regex(config.RegexForFullTextNeedToTranslate, RegexOptions.Singleline | RegexOptions.Compiled);

            if (config.RegexForEachLineNeedToTranslate != string.Empty)
                eachLineRegex = new Regex(config.RegexForEachLineNeedToTranslate, RegexOptions.Singleline | RegexOptions.Compiled);

            if (config.RegexForIgnoredSubstringWithinText != string.Empty)
                ignoredSubstringWithinTextRegex = new Regex(config.RegexForIgnoredSubstringWithinText, RegexOptions.Multiline | RegexOptions.Compiled);

            batchSubTexts = new List<string>();
            batchSplitMap = new Dictionary<string, List<string>>();
            batchTranslationDictionary = new Dictionary<string, string>();
            batchUniqueSubTexts = new HashSet<string>();
            batchTranslatedParts = new List<string>();
            uniqueTexts = new List<string>();
            textControlMap = new Dictionary<string, List<object>>();
            translatedTextBuilder = new StringBuilder();
            controlTextMap = new Dictionary<object, List<string>>();

            finalChunks = new List<string>();
            listStringPool = new ObjectPool<List<string>>(() => new List<string>(), 64, list => list.Clear());
            listObjectPool = new ObjectPool<List<object>>(() => new List<object>(), 64, list => list.Clear());
            translationQueueElementPool = new ObjectPool<TranslationQueueElement>(() => new TranslationQueueElement(), 64, element => element.Reset());

            translateOn = config.isConfigValid;
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
            if (exceededThreshold)
                StatusLabelController.instance.SetText($"AT: {requestedCharacterCount}\nNow {(translateOn ? "ON" : "OFF")} ({config.ToggleTranslationKeyBinding})");
            else
                StatusLabelController.instance.SetText($"AT: {requestedCharacterCount}");
        }

        private bool NeedToTranslate(string text)
        {
            if (IsNullOrWhiteSpace(text))
                return false;

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

            for (; ; )
            {
                while (!translateOn)
                {
                    yield return null;
                }

                DeduplicateTexts();

                if (uniqueTexts.Count > 0)
                {
                    int count = GenerateBatch();
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
                object control = translationRequest.control;

                if (IsNullOrWhiteSpace(text))
                {
                    translationQueueElementPool.Return(translationQueue.Dequeue());
                    continue;
                }

                if (totalCharacterCount + text.Length > config.MaxBatchCharacterCount)
                {
                    break;
                }

                foreach (var uniqueText in uniqueTexts)
                {
                    if (text.StartsWith(uniqueText) && !text.Equals(uniqueText) && textControlMap.TryGetValue(text, out List<object> list) && list.Contains(control))
                        text = text.Substring(uniqueText.Length);
                }

                if (!textControlMap.ContainsKey(text))
                {
                    uniqueTexts.Add(text);
                    totalCharacterCount += text.Length;
                    textControlMap[text] = listObjectPool.Get();
                }
                textControlMap[text].Add(control);

                translationQueueElementPool.Return(translationQueue.Dequeue());

                if (config.MaxBatchTextCount > 0 && uniqueTexts.Count >= config.MaxBatchTextCount)
                {
                    break;
                }
            }
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
                List<string> parts = listStringPool.Get();
                if (config.RegexForIgnoredSubstringWithinText != null)
                    parts.AddRange(ignoredSubstringWithinTextRegex.Split(text)
                                        .Select(part => part.Trim())
                                        .Where(part => !IsNullOrWhiteSpace(part)));
                else
                    parts.Add(text.Trim());

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
                        int startIndex = translatedTextBuilder.ToString().IndexOf(part);
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
                            OnTranslationFinish(control, text, translatedText, true);
                            RemoveTextFromControlStatusMap(control, text);
                        }
                        listObjectPool.Return(controls);
                        textControlMap.Remove(text);
                    }
                    listStringPool.Return(parts);
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
                StatusLabelController.instance.SetHighlight();
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

            yield return StartCoroutine(translationService.StartTranslation(
                batchSubTexts,
                (translatedTexts) =>
                {
                    if (translatedTexts == null || translatedTexts.Length != batchSubTexts.Count)
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

                            listStringPool.Return(parts);
                            batchSplitMap.Remove(originalText);
                            if (textControlMap.TryGetValue(originalText, out var controls))
                            {
                                foreach (var control in controls)
                                {
                                    RemoveTextFromControlStatusMap(control, originalText);
                                }
                                listObjectPool.Return(controls);
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
                            int startIndex = translatedTextBuilder.ToString().IndexOf(part);
                            if (startIndex != -1 && batchTranslationDictionary.TryGetValue(part, out var translatedPart))
                            {
                                translatedTextBuilder.Replace(part, translatedPart, startIndex, part.Length);
                            }
                        }
                        listStringPool.Return(parts);
                        batchSplitMap.Remove(originalText);

                        string translatedText = translatedTextBuilder.ToString();

                        if (textControlMap.TryGetValue(originalText, out var controls))
                        {
                            foreach (var control in controls)
                            {
                                OnTranslationFinish(control, originalText, translatedText, true);
                                RemoveTextFromControlStatusMap(control, originalText);
                            }
                            listObjectPool.Return(controls);
                            textControlMap.Remove(originalText);
                        }
                    }
                })
            );
        }

        public void AddTranslationRequest(string text, object control)
        {
            if (!translateOn)
                return;

            if (!NeedToTranslate(text))
                return;

            if (translationCache.TryGetValue(text, out string translatedText))
            {
                OnTranslationFinish(control, text, translatedText, false);
                RemoveTextFromControlStatusMap(control, text);
                return;
            }

            if (text.Length <= config.MaxBatchCharacterCount)
            {
                text = RemovePrefixFromText(text, control);
                if (IsNullOrWhiteSpace(text))
                    return;
                SubmitRequest(control, text);
                if (!isProcessingQueue)
                    StartCoroutine(ProcessTranslationQueue());
                return;
            }

            finalChunks.Clear();

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

            foreach (var chunk in finalChunks)
            {
                string chunkToQueue = RemovePrefixFromText(chunk, control);
                if (IsNullOrWhiteSpace(chunkToQueue))
                    continue;
                SubmitRequest(control, chunkToQueue);
            }

            if (!isProcessingQueue)
                StartCoroutine(ProcessTranslationQueue());
        }

        private void SubmitRequest(object control, string text)
        {
            AddTextToControlStatusMap(control, text);
            TranslationQueueElement element = translationQueueElementPool.Get();
            element.Set(text, control);
            translationQueue.Enqueue(element);
            if (fullTextCache.TryGetValue(text, out int count))
                fullTextCache.Set(text, count + 1);
            else
                fullTextCache.Set(text, 1);
        }

        private string RemovePrefixFromText(string text, object control)
        {
            if (controlTextMap.TryGetValue(control, out var prevTexts))
            {
                foreach (var prevText in prevTexts)
                {
                    if (text.StartsWith(prevText) && !text.Equals(prevText))
                        text = text.Substring(prevText.Length);
                }
            }
            return text;
        }

        private void AddTextToControlStatusMap(object control, string text)
        {
            if (!controlTextMap.TryGetValue(control, out List<string> translatedTexts))
            {
                translatedTexts = listStringPool.Get();
                controlTextMap[control] = translatedTexts;
            }

            if (!translatedTexts.Contains(text))
                translatedTexts.Add(text);
        }

        private void RemoveTextFromControlStatusMap(object control, string originalText)
        {
            if (controlTextMap.Count == 0)
                return;


            if (controlTextMap.TryGetValue(control, out List<string> translatedTexts))
            {
                translatedTexts.RemoveAll(text => text == originalText);

                if (translatedTexts.Count == 0)
                {
                    listStringPool.Return(translatedTexts);
                    controlTextMap.Remove(control);
                }
            }
        }

        private void AddChunks(List<string> chunks, string text)
        {
            int size = config.MaxBatchCharacterCount;
            for (int i = 0; i < text.Length; i += size)
                chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
        }

        private void OnTranslationFinish(object textObject, string original, string result, bool setFullTextCached)
        {
            if (textObject == null || result == null)
                return;

            if (setFullTextCached && fullTextCache.TryGetValue(original, out int count) && count > 2)
                translationCache.Set(original, result);

            if (textObject is dfLabel dfLabel)
            {
                if (dfLabel == null || dfLabel.text == null || !dfLabel.isActiveAndEnabled)
                    return;

                string originalText = dfLabel.text;
                int startIndex = dfLabel.text.IndexOf(original);
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

                    dfLabel.text = originalText.Substring(0, startIndex) +
                                   result +
                                   originalText.Substring(startIndex + original.Length);

                    dfLabel.Invalidate();
                    if (isDefaultLabel)
                    {
                        Vector3 position = dfLabel.RelativePosition;
                        dfLabel.RelativePosition = new Vector3(position.x, position.y - dfLabel.Height + originalHeight, position.z);
                    }
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

                        if (config.DfTextScaleExpandThreshold >= 0 && dfButton.TextScale < config.DfTextScaleExpandThreshold)
                            dfButton.TextScale = config.DfTextScaleExpandToValue;
                    }

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
                    FontManager.instance.ItemTipsReposition(sLabel);
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

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
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

                var newPairs = new List<KeyValuePair<string, string>>();
                var node = translationCache.GetOrder().Last;
                while (node != null)
                {
                    if (node.Value != null)
                    {
                        newPairs.Add(new KeyValuePair<string, string>(node.Value.Key, node.Value.Value));
                    }
                    node = node.Previous;
                }

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
