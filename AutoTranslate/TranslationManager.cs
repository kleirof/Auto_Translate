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
        private bool isProcessingQueue = false;
        private Queue<TranslationQueueElement> translationQueue;

        private ITranslationService translationService;

        private List<string> batchSubTexts;
        private Dictionary<string, List<string>> batchSplitMap;
        private Dictionary<string, string> batchTranslationDictionary;
        private HashSet<string> batchUniqueSubTexts;
        private List<string> batchBntranslatedParts;
        private List<string> uniqueTexts;
        private Dictionary<string, List<object>> textControlMap;
        private Dictionary<object, List<string>> controlTextStatusMap;

        private Regex fullTextRegex;
        private Regex eachLineRegex;
        private Regex ignoredSubstringWithinTextRegex;

        private string[] newLineSymbols = new string[] { "\r\n", "\r", "\n" };

        private StringBuilder translatedTextBuilder;

        internal static TranslationManager instance;

        private int requestedCharacterCount = 0;

        internal bool exceededThreshold = false;

        private List<string> finalChunks;

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
                case AutoTranslateModule.TranslationAPIType.LargeModel:
                    translationService = new LargeModelTranslationService(config);
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

            if (config.RegexForIgnoredSubstringWithinText != string.Empty)
                ignoredSubstringWithinTextRegex = new Regex(config.RegexForIgnoredSubstringWithinText);

            batchSubTexts = new List<string>();
            batchSplitMap = new Dictionary<string, List<string>>();
            batchTranslationDictionary = new Dictionary<string, string>();
            batchUniqueSubTexts = new HashSet<string>();
            batchBntranslatedParts = new List<string>();
            uniqueTexts = new List<string>();
            textControlMap = new Dictionary<string, List<object>>();
            translatedTextBuilder = new StringBuilder();
            controlTextStatusMap = new Dictionary<object, List<string>>();

            translateOn = true;

            finalChunks = new List<string>();
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

            while (translationQueue.Count > 0 && totalCharacterCount < config.RequestBatchSize)
            {
                var translationRequest = translationQueue.Peek();
                string text = translationRequest.text;
                object control = translationRequest.control;

                if (IsNullOrWhiteSpace(text))
                {
                    translationQueue.Dequeue();
                    continue;
                }

                if (totalCharacterCount + text.Length > config.RequestBatchSize)
                {
                    break;
                }

                foreach (var uniqueText in uniqueTexts)
                {
                    if (text.StartsWith(uniqueText) && textControlMap.TryGetValue(text, out List<object> list) && list.Contains(control))
                        text = text.Substring(uniqueText.Length);
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
        }

        private int GenerateBatch()
        {
            batchSubTexts.Clear();
            batchSplitMap.Clear();
            batchTranslationDictionary.Clear();
            batchUniqueSubTexts.Clear();

            string splitPattern = config.RegexForIgnoredSubstringWithinText;

            int batchCharacterCount = 0;

            foreach (var text in uniqueTexts)
            {
                string[] parts;
                if (!string.IsNullOrEmpty(splitPattern))
                    parts = Regex.Split(text, splitPattern);
                else
                    parts = new string[] { text };

                parts = parts.Where(part => !IsNullOrWhiteSpace(part)).ToArray();

                bool allTranslated = true;
                batchBntranslatedParts.Clear();

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
                            batchBntranslatedParts.Add(part);
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
                            translatedTextBuilder.Replace(part, translatedPart);
                        }
                    }

                    string translatedText = translatedTextBuilder.ToString();

                    if (textControlMap.TryGetValue(text, out var controls))
                    {
                        foreach (var control in controls)
                        {
                            OnTranslationFinish(control, text, translatedText);
                            RemoveTextFromControlStatusMap(control, text);
                        }
                    }
                }
                else
                {
                    batchSplitMap[text] = parts.ToList();
                    batchSubTexts.AddRange(batchBntranslatedParts);
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
                batchSubTexts.ToArray(),
                (translatedTexts) =>
                {
                    if (translatedTexts == null)
                        return;

                    if (translatedTexts.Length != batchSubTexts.Count)
                    {
                        Debug.LogError("翻译结果数量与请求数量不匹配！The number of translation results does not match the number of requests!");
                        Debug.LogError("请求 Requests：");
                        foreach (var subText in batchSubTexts)
                            Debug.Log("      " + subText);
                        Debug.LogError("结果 Results：");
                        foreach (var subText in translatedTexts)
                            Debug.Log("      " + subText);
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

                        string translatedText = translatedTextBuilder.ToString();

                        if (textControlMap.TryGetValue(originalText, out var controls))
                        {
                            foreach (var control in controls)
                            {
                                OnTranslationFinish(control, originalText, translatedText);
                                RemoveTextFromControlStatusMap(control, originalText);
                            }
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
                OnTranslationFinish(control, text, translatedText);
                RemoveTextFromControlStatusMap(control, text);
                return;
            }

            if (text.Length <= config.RequestBatchSize)
            {
                text = RemovePrefixFromText(text, control);
                AddTextToControlStatusMap(control, text);
                translationQueue.Enqueue(new TranslationQueueElement(text, control));
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
                AddTextToControlStatusMap(control, chunkToQueue);

                translationQueue.Enqueue(new TranslationQueueElement(chunkToQueue, control));
            }

            if (!isProcessingQueue)
                StartCoroutine(ProcessTranslationQueue());
        }

        private string RemovePrefixFromText(string text, object control)
        {
            if (controlTextStatusMap.TryGetValue(control, out var prevTexts))
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
            if (!controlTextStatusMap.TryGetValue(control, out List<string> translatedTexts))
            {
                translatedTexts = new List<string>();
                controlTextStatusMap[control] = translatedTexts;
            }

            if (!translatedTexts.Contains(text))
                translatedTexts.Add(text);
        }

        private void RemoveTextFromControlStatusMap(object control, string originalText)
        {
            if (controlTextStatusMap.Count == 0)
                return;

            if (controlTextStatusMap.TryGetValue(control, out List<string> translatedTexts))
            {
                translatedTexts.RemoveAll(text => text == originalText);

                if (translatedTexts.Count == 0)
                    controlTextStatusMap.Remove(control);
            }
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

                        if (config.DfTextScaleExpandThreshold >= 0 && dfLabel.TextScale < config.DfTextScaleExpandThreshold)
                            dfLabel.TextScale = config.DfTextScaleExpandToValue;
                    }

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
