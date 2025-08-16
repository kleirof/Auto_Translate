using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoTranslate
{
    public class LlmTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        private StringBuilder builder = new StringBuilder();
        private StringBuilder jsonBuilder = new StringBuilder();
        private StringBuilder positionedBuilder = new StringBuilder();
        private readonly string[] cachedSplitPattern = new string[1];
        private static Regex positionedResponseRegex;

        public LlmTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
            cachedSplitPattern[0] = config.LlmSplitText;
        }

        private string[] ParseJsonResponse(string content)
        {
            var translations = new List<string>();

            try
            {
                JArray contentArray = JArray.Parse(content);
                foreach (var item in contentArray)
                {
                    translations.Add(item["text"].ToString());
                }
            }
            catch (JsonReaderException ex)
            {
                Debug.LogError($"解析 'content' 字段时失败: {ex.Message}");
            }

            return translations.ToArray();
        }

        private string[] ParseSplittedResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;
            string[] result = null;

            try
            {
                result = response.Split(cachedSplitPattern, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                Debug.LogError($"分割字符串时发生意外错误。An unexpected error occurred while splitting the string.{ex}");
            }

            return result;
        }

        private string[] ParsePositionedResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            if (string.IsNullOrEmpty(config.LlmPositionText) || string.IsNullOrEmpty(config.LlmSegmentText))
                throw new ArgumentException("标记不能为空。The tag cannot be empty.");

            string startTag = config.LlmPositionText;
            string endTag = config.LlmSegmentText;

            List<string> results = new List<string>();

            int len = response.Length;
            int pos = 0;
            int startTagLen = startTag.Length;
            int endTagLen = endTag.Length;

            while (pos < len)
            {
                int matchStart = response.IndexOf(startTag, pos, StringComparison.OrdinalIgnoreCase);
                if (matchStart == -1)
                {
                    string tail = response.Substring(pos);
                    if (!IsNullOrWhiteSpace(tail))
                        results.Add(tail.Trim());
                    break;
                }

                if (matchStart > pos)
                {
                    string chunk = response.Substring(pos, matchStart - pos);
                    if (!IsNullOrWhiteSpace(chunk))
                        results.Add(chunk.Trim());
                }

                int i = matchStart + startTagLen;

                while (i < len && response[i] >= '0' && response[i] <= '9')
                    i++;

                if (i + endTagLen <= len &&
                    string.Compare(response, i, endTag, 0, endTagLen, StringComparison.OrdinalIgnoreCase) == 0)
                    pos = i + endTagLen;
                else
                    pos = i;
            }

            return results.Count > 0 ? results.ToArray() : null;
        }

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        private string[] ParseFormattedResponse(string responseJson, List<string> texts)
        {
            string content = ParseResponse(responseJson);
            string[] result = null;

            if (!string.IsNullOrEmpty(content))
            {
                switch (config.LlmDataFormat)
                {
                    case AutoTranslateModule.LlmDataFormatType.Json:
                        result = ParseJsonResponse(content);
                        break;
                    case AutoTranslateModule.LlmDataFormatType.Split:
                        result = ParseSplittedResponse(content);
                        break;
                    case AutoTranslateModule.LlmDataFormatType.Positioned:
                        result = ParsePositionedResponse(content);
                        break;
                }
            }

            if (result == null || result.Length == 0)
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            if (result.Length != texts.Count)
            {
                Debug.LogError("翻译结果数量与请求数量不匹配！The number of translation results does not match the number of requests!");
                Debug.LogError("请求 Requests：");
                foreach (var subText in texts)
                    Debug.Log("      " + subText);
                Debug.LogError("结果 Results：");
                foreach (var subText in result)
                    Debug.Log("      " + subText);
                throw new InvalidOperationException("翻译结果数量与请求数量不匹配！The number of translation results does not match the number of requests!");
            }
            return result;
        }

        private string ParseResponse(string responseJson)
        {
            JObject jsonResponse;

            try
            {
                jsonResponse = JObject.Parse(responseJson);
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException("响应的JSON格式无效 The JSON format of the response is invalid: ", ex);
            }

            string result = null;

            if (jsonResponse["choices"] is JArray choicesArray)
            {
                var content = choicesArray[0]["message"]?["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                {
                    result = content;
                }
            }

            if (result == null)
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            return result;
        }

        private string ReplaceQuotesWithChinese(string source)
        {
            builder.Length = 0;
            bool isInsideDoubleQuote = false;
            bool isInsideSingleQuote = false;

            foreach (char c in source)
            {
                if (c == '"')
                {
                    builder.Append(isInsideDoubleQuote ? '”' : '“');
                    isInsideDoubleQuote = !isInsideDoubleQuote;
                }
                else if (c == '\'')
                {
                    builder.Append(isInsideSingleQuote ? '’' : '‘');
                    isInsideSingleQuote = !isInsideSingleQuote;
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private string PreprocessQuotes(string source)
        {
            switch (config.LlmQuotePreprocess)
            {
                case AutoTranslateModule.LlmQuotePreprocessType.Chinese:
                    return ReplaceQuotesWithChinese(source);
                case AutoTranslateModule.LlmQuotePreprocessType.None:
                default:
                    return source.Replace("\"", "\\\"");
            }
        }

        private string ConvertListToJson(List<string> texts)
        {
            jsonBuilder.Length = 0;
            jsonBuilder.Append("[");

            for (int i = 0; i < texts.Count; i++)
            {
                if (i > 0)
                    jsonBuilder.Append(", ");

                int id = i + 1;
                jsonBuilder.Append("{\"id\": ")
                   .Append(id)
                   .Append(", \"text\": \"")
                   .Append(PreprocessQuotes(texts[i].Replace("\\\\", "\\").Replace("\r", "")))
                   .Append("\"}");
            }

            jsonBuilder.Append("]");
            return jsonBuilder.ToString();
        }

        private string ConvertListToSplitted(List<string> texts)
        {
            if (texts == null || texts.Count == 0)
                return string.Empty;

            return string.Join(config.LlmSplitText, texts.ToArray()).Replace("\r", "");
        }

        private string ConvertListToPositioned(List<string> texts)
        {
            if (texts == null || texts.Count == 0)
                return string.Empty;

            positionedBuilder.Length = 0;
            for (int i = 0; i < texts.Count; i++)
            {
                positionedBuilder.Append("<!pos!>")
                    .Append(i + 1)
                    .Append("<!seg!>")
                    .Append(texts[i]);
            }
            return positionedBuilder.ToString().Replace("\r", "");
        }

        private string ConvertListToFormatted(List<string> texts)
        {
            switch (config.LlmDataFormat)
            {
                case AutoTranslateModule.LlmDataFormatType.Json:
                    return ConvertListToJson(texts);
                case AutoTranslateModule.LlmDataFormatType.Split:
                    return ConvertListToSplitted(texts);
                case AutoTranslateModule.LlmDataFormatType.Positioned:
                default:
                    return ConvertListToPositioned(texts);
            }
        }

        public IEnumerator StartSingleTranslation(List<string> texts, Action<string[]> callback)
        {
            var jsonObject = new JObject
            {
                { "model", config.LlmName },
                {
                    "messages", new JArray
                    {
                        new JObject
                        {
                            { "role", "system" },
                            { "content", config.LlmPrompt }
                        },
                        new JObject
                        {
                            { "role", "user" },
                            { "content", ConvertListToFormatted(texts) }
                        }
                    }
                },
                { "temperature", config.LlmTemperature },
                { "max_tokens", config.LlmMaxTokens },
                { "top_p", config.LlmTopP },
                { "top_k", config.LlmTopK },
                { "frequency_penalty", config.LlmFrequencyPenalty },
                { "n", 1 }
            };

            if (!string.IsNullOrEmpty(config.LlmExtraParametersJson))
            {
                JObject extraObj = JObject.Parse(config.LlmExtraParametersJson);

                foreach (var property in extraObj.Properties())
                {
                    jsonObject[property.Name] = property.Value;
                }
            }

            string payloadJson = jsonObject.ToString(Formatting.None);

            string requestUrl = $"{config.LlmBaseUrl}";

            int retryCount = 0;
            bool needRetry = false;

            for (; ; )
            {
                if (needRetry)
                {
                    if (retryCount < config.MaxRetryCount)
                    {
                        retryCount++;
                        Debug.Log($"正在重试。。。尝试第 {retryCount} 次。Retrying... Attempt time {retryCount}.");
                        needRetry = false;
                        yield return new WaitForSecondsRealtime(config.RetryInterval);
                    }
                    else
                    {
                        Debug.LogError($"多次尝试失败。已重试 {config.MaxRetryCount} 次。翻译中止！Multiple attempts failed. Retried {config.MaxRetryCount} times. Translation aborted!");
                        callback?.Invoke(null);
                        yield break;
                    }
                }

                using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {config.LlmApiKey}");

                    yield return request.SendWebRequest();

                    if (request.isNetworkError || request.isHttpError)
                    {
                        Debug.LogError($"请求失败 Request failed: {request.error}");
                        needRetry = true;
                        continue;
                    }
                    else
                    {
                        string responseJson = request.downloadHandler.text;
                        string[] translatedTexts = null;
                        try
                        {
                            translatedTexts = ParseFormattedResponse(responseJson, texts);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result: {ex.Message}");
                            Debug.LogError($"响应JSON Response JSON:\n{responseJson}");
                            needRetry = true;

                            continue;
                        }

                        if (translatedTexts != null && translatedTexts.Length > 0)
                        {
                            callback?.Invoke(translatedTexts);
                        }
                        else
                        {
                            Debug.LogError("翻译失败，未获得翻译结果！Translation failed, no translation result obtained!");
                            needRetry = true;
                        }
                        yield break;
                    }
                }
            }
        }

        public IEnumerator StartTranslation(List<string> texts, Action<string[]> callback)
        {
            if (config.LlmDataFormat == AutoTranslateModule.LlmDataFormatType.Parallel)
            {
                yield return TranslationManager.instance.StartCoroutine(StartParallelBatchTranslation(texts, callback));
            }
            else
            {
                yield return TranslationManager.instance.StartCoroutine(StartSingleTranslation(texts, callback));
            }
        }


        public IEnumerator StartParallelBatchTranslation(List<string> texts, Action<string[]> callback)
        {
            int totalBatches = texts.Count;
            int completedBatches = 0;
            bool anyFailed = false;
            string[] batchResults = new string[totalBatches];

            for (int i = 0; i < totalBatches; i++)
            {
                int batchIndex = i;
                string singleText = texts[i];

                TranslationManager.instance.StartCoroutine(SendBatchRequest(singleText, batchIndex, (success, results) =>
                {
                    completedBatches++;

                    if (success && results != null)
                    {
                        batchResults[batchIndex] = results;
                    }
                    else
                    {
                        anyFailed = true;
                    }

                    if (completedBatches == totalBatches)
                    {
                        if (anyFailed)
                        {
                            Debug.LogError("并行批量模式存在失败批次。There are failed batches in parallel batch mode.");
                            callback?.Invoke(null);
                        }
                        else
                        {
                            callback?.Invoke(batchResults);
                        }
                    }
                }));
            }

            while (completedBatches < totalBatches)
            {
                yield return null;
            }
        }

        private IEnumerator SendBatchRequest(string singleText, int batchIndex, Action<bool, string> batchCallback)
        {
            var jsonObject = new JObject
            {
                { "model", config.LlmName },
                {
                    "messages", new JArray
                    {
                        new JObject
                        {
                            { "role", "system" },
                            { "content", config.LlmPrompt }
                        },
                        new JObject
                        {
                            { "role", "user" },
                            { "content", singleText.Replace("\r", "") }
                        }
                    }
                },
                { "temperature", config.LlmTemperature },
                { "max_tokens", config.LlmMaxTokens },
                { "top_p", config.LlmTopP },
                { "top_k", config.LlmTopK },
                { "frequency_penalty", config.LlmFrequencyPenalty },
                { "n", 1 }
            };

            if (!string.IsNullOrEmpty(config.LlmExtraParametersJson))
            {
                JObject extraObj = JObject.Parse(config.LlmExtraParametersJson);
                foreach (var property in extraObj.Properties())
                {
                    jsonObject[property.Name] = property.Value;
                }
            }

            string payloadJson = jsonObject.ToString(Formatting.None);
            string requestUrl = $"{config.LlmBaseUrl}";

            int retryCount = 0;
            bool needRetry = false;

            for (; ; )
            {
                if (needRetry)
                {
                    if (retryCount < config.MaxRetryCount)
                    {
                        retryCount++;
                        Debug.Log($"批次 {batchIndex} 正在重试... 尝试第 {retryCount} 次。Batch {batchIndex} is retrying... Attempt time {retryCount}.");
                        needRetry = false;
                        yield return new WaitForSecondsRealtime(config.RetryInterval);
                    }
                    else
                    {
                        Debug.LogError($"批次 {batchIndex} 多次尝试失败。已重试 {config.MaxRetryCount} 次。Multiple attempts failed in batch {batchIndex}. Retried {config.MaxRetryCount} times. Translation aborted!");
                        batchCallback?.Invoke(false, null);
                        yield break;
                    }
                }

                using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {config.LlmApiKey}");

                    yield return request.SendWebRequest();

                    if (request.isNetworkError || request.isHttpError)
                    {
                        Debug.LogError($"批次 {batchIndex} 请求失败 Request failed in batch {batchIndex}: {request.error}");
                        needRetry = true;
                        continue;
                    }
                    else
                    {
                        string responseJson = request.downloadHandler.text;
                        string translatedText = null;
                        try
                        {
                            translatedText = ParseResponse(responseJson);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"批次 {batchIndex} 解析翻译结果失败 Failed to parse translation result in batch {batchIndex}: {ex.Message}");
                            Debug.LogError($"响应JSON Response JSON:\n{responseJson}");
                            needRetry = true;
                            continue;
                        }

                        if (translatedText != null)
                        {
                            batchCallback?.Invoke(true, translatedText);
                        }
                        else
                        {
                            Debug.LogError($"批次 {batchIndex} 无翻译结果。No translation results in batch {batchIndex}.");
                            needRetry = true;
                        }
                        yield break;
                    }
                }
            }
        }
    }
}
