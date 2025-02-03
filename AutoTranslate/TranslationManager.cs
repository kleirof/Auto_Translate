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
        private TranslationManagerConfig config;

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

        public void Initialize(TranslationManagerConfig config)
        {
            this.config = config;

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
                foreach (var a in subTexts)
                    Debug.Log("      " + a);
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
                                if (translationDictionary.TryGetValue(part, out var translatedPart))
                                {
                                    translatedTextBuilder.Replace(part, translatedPart);
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

            translationQueue.Enqueue(new TranslationQueueElement(text, control));

            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessTranslationQueue());
            }
        }

        private void OnTranslationFinish(object dfControl, string original, string result)
        {
            if (dfControl == null || result == null)
                return;

            if (dfControl is dfLabel dfLabel)
            {
                if (dfLabel == null || dfLabel.text == null)
                    return;

                if (dfLabel.text == original && dfLabel.isActiveAndEnabled)
                {
                    dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                    if (fontBase != null && dfLabel.Font != fontBase)
                    {
                        dfLabel.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfLabel.Atlas = dfFont.Atlas;
                    }
                    dfLabel.text = result;
                    dfLabel.OnTextChanged();
                }
            }
            else if (dfControl is dfButton dfButton)
            {
                if (dfButton == null || dfButton.text == null)
                    return;

                if (dfButton.text == original && dfButton.isActiveAndEnabled)
                {
                    dfFontBase fontBase = AutoTranslateModule.instance.fontManager.dfFontBase;
                    if (fontBase != null && dfButton.Font != fontBase)
                    {
                        dfButton.Font = fontBase;
                        if (fontBase is dfFont dfFont)
                            dfButton.Atlas = dfFont.Atlas;
                    }
                    dfButton.text = result;
                    dfButton.Invalidate();
                }
            }
            else if (dfControl is SGUI.SLabel sLabel)
            {
                if (sLabel == null || sLabel.Text == null)
                    return;

                if (sLabel.Text == original)
                {
                    sLabel.Text = result;
                }
            }
            else if (dfControl is tk2dTextMesh textMesh)
            {
                if (textMesh == null || textMesh.data == null || textMesh.data.text == null)
                    return;

                if (textMesh.data.text == original)
                {
                    tk2dFontData tk2dFont = AutoTranslateModule.instance.fontManager.tk2dFont;
                    if (tk2dFont != null && textMesh.font != tk2dFont)
                        FontManager.SetTextMeshFont(textMesh, tk2dFont);
                    textMesh.data.text = result;
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
