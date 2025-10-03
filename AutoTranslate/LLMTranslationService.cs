using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Globalization;

namespace AutoTranslate
{
    public class LlmTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        private StringBuilder builder = new StringBuilder(256);
        private StringBuilder jsonBuilder = new StringBuilder(256);
        private StringBuilder positionedBuilder = new StringBuilder(256);
        private readonly string[] cachedSplitPattern = new string[1];
        private StringBuilder payloadBuilder = new StringBuilder(256);

        private ReusableStringReader pooledReader = new ReusableStringReader();

        private static string cachedModelName;
        private static string cachedPrompt;
        private static string cachedTemperature;
        private static string cachedTopP;
        private static string cachedFrequencyPenalty;
        private static int cachedMaxTokens;
        private static int cachedTopK;
        private static string cachedExtraParams;

        public LlmTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
            cachedSplitPattern[0] = config.LlmSplitText;

            cachedModelName = TextProcessor.EscapeJsonString(config.LlmName);
            cachedPrompt = TextProcessor.EscapeJsonString(config.LlmPrompt);
            cachedTemperature = config.LlmTemperature.ToString(CultureInfo.InvariantCulture);
            cachedTopP = config.LlmTopP.ToString(CultureInfo.InvariantCulture);
            cachedFrequencyPenalty = config.LlmFrequencyPenalty.ToString(CultureInfo.InvariantCulture);
            cachedMaxTokens = config.LlmMaxTokens;
            cachedTopK = config.LlmTopK;

            if (!string.IsNullOrEmpty(config.LlmExtraParametersJson))
            {
                string extraParams = config.LlmExtraParametersJson.Trim();
                if (extraParams.StartsWith("{") && extraParams.EndsWith("}"))
                {
                    cachedExtraParams = extraParams.Substring(1, extraParams.Length - 2);
                }
                else
                {
                    cachedExtraParams = extraParams;
                }
            }
            else
            {
                cachedExtraParams = null;
            }
        }

        private List<string> ParseJsonResponse(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("解析JSON失败: 内容为空。Failed to parse JSON: Content is empty.");
                return null;
            }

            var translations = Pools.listStringPool.Get();

            try
            {
                pooledReader.Reset(content);
                using (var reader = new JsonTextReader(pooledReader))
                {
                    int targetDepth = -1;
                    bool inArray = false;
                    bool inObject = false;
                    bool inTextProperty = false;

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.StartArray:
                                inArray = true;
                                break;

                            case JsonToken.StartObject:
                                if (inArray)
                                {
                                    inObject = true;
                                    targetDepth = reader.Depth;
                                }
                                break;

                            case JsonToken.PropertyName:
                                if (inObject && (string)reader.Value == "text" && reader.Depth == targetDepth + 1)
                                {
                                    inTextProperty = true;
                                }
                                break;

                            case JsonToken.String:
                                if (inTextProperty && inObject)
                                {
                                    string result = reader.Value?.ToString();
                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        translations.Add(result);
                                    }
                                    inTextProperty = false;
                                }
                                break;

                            case JsonToken.EndObject:
                                if (inObject && reader.Depth == targetDepth)
                                {
                                    inObject = false;
                                    targetDepth = -1;
                                    inTextProperty = false;
                                }
                                break;

                            case JsonToken.EndArray:
                                if (inArray)
                                {
                                    inArray = false;
                                }
                                break;
                        }
                    }
                }

                if (translations.Count == 0)
                {
                    Debug.LogError("解析JSON失败: 未找到任何 'text' 字段。 Failed to parse JSON: No 'text' field found.");
                    Pools.listStringPool.Return(translations);
                    return null;
                }

                return translations;
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析JSON失败 Failed to parse JSON: {ex.Message}");
                Pools.listStringPool.Return(translations);
                return null;
            }
        }

        private List<string> ParseSplittedResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            List<string> result = Pools.listStringPool.Get();

            try
            {
                string splitText = config.LlmSplitText;
                int splitLength = splitText.Length;
                int startIndex = 0;

                while (startIndex < response.Length)
                {
                    int endIndex = TextProcessor.IndexOfString(response, splitText, startIndex);
                    if (endIndex == -1)
                    {
                        endIndex = response.Length;
                    }

                    if (endIndex > startIndex)
                    {
                        string segment = response.Substring(startIndex, endIndex - startIndex);
                        if (!string.IsNullOrEmpty(segment))
                        {
                            result.Add(segment);
                        }
                    }

                    startIndex = endIndex + splitLength;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"分割字符串时发生意外错误 An unexpected error occurred while splitting the string: {ex}");
                Pools.listStringPool.Return(result);
                return null;
            }
        }

        private List<string> ParsePositionedResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            if (string.IsNullOrEmpty(config.LlmPositionText) || string.IsNullOrEmpty(config.LlmSegmentText))
                throw new ArgumentException("标记不能为空。The tag cannot be empty.");

            List<string> results = Pools.listStringPool.Get();

            try
            {
                string startTag = config.LlmPositionText;
                string endTag = config.LlmSegmentText;
                int len = response.Length;
                int pos = 0;
                int startTagLen = startTag.Length;
                int endTagLen = endTag.Length;

                while (pos < len)
                {
                    int matchStart = TextProcessor.IndexOfString(response, startTag, pos);
                    if (matchStart == -1)
                    {
                        if (TextProcessor.HasNonWhitespace(response, pos, len - pos))
                        {
                            string tail = response.Substring(pos);
                            results.Add(TextProcessor.TrimOnlyIfNeeded(tail));
                        }
                        break;
                    }

                    if (matchStart > pos)
                    {
                        if (TextProcessor.HasNonWhitespace(response, pos, matchStart - pos))
                        {
                            string chunk = TextProcessor.TrimOnlyIfNeeded(response.Substring(pos, matchStart - pos));
                            results.Add(chunk);
                        }
                    }

                    int i = matchStart + startTagLen;

                    while (i < len && char.IsDigit(response[i]))
                        i++;

                    if (i + endTagLen <= len && TextProcessor.StartsWithString(response, i, endTag))
                        pos = i + endTagLen;
                    else
                        pos = i;
                }

                if (results.Count > 0)
                {
                    return results;
                }
                else
                {
                    Pools.listStringPool.Return(results);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"解析定位响应时发生错误 An error occurred while parsing the positioning response: {ex}");
                Pools.listStringPool.Return(results);
                return null;
            }
        }

        private List<string> ParseFormattedResponse(string responseJson, List<string> texts)
        {
            string content = ParseResponse(responseJson);
            List<string> result = null;

            try
            {
                if (string.IsNullOrEmpty(content))
                    throw new InvalidOperationException("翻译结果为空！The translation result is empty!");

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

                if (result == null || result.Count == 0)
                    throw new InvalidOperationException("翻译结果为空！The translation result is empty!");

                if (result.Count != texts.Count)
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
            catch (Exception)
            {
                if (result != null)
                    Pools.listStringPool.Return(result);
                throw;
            }
        }

        public string ParseResponse(string responseJson)
        {
            try
            {
                pooledReader.Reset(responseJson);
                using (var reader = new JsonTextReader(pooledReader))
                {
                    int targetDepth = -1;
                    bool inChoices = false;
                    bool inFirstChoice = false;
                    bool inMessage = false;

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.StartArray:
                                if (reader.Path == "choices")
                                {
                                    inChoices = true;
                                }
                                break;

                            case JsonToken.StartObject:
                                if (inChoices && reader.Path == "choices[0]")
                                {
                                    inFirstChoice = true;
                                }
                                else if (inFirstChoice && reader.Path == "choices[0].message")
                                {
                                    inMessage = true;
                                    targetDepth = reader.Depth + 1;
                                }
                                break;

                            case JsonToken.PropertyName:
                                if (inMessage && (string)reader.Value == "content" && reader.Depth == targetDepth)
                                {
                                    reader.Read();
                                    string result = reader.Value?.ToString();

                                    if (!string.IsNullOrEmpty(result))
                                        return result;

                                    throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
                                }
                                break;

                            case JsonToken.EndObject:
                                if (inMessage)
                                {
                                    inMessage = false;
                                    targetDepth = -1;
                                }
                                else if (inFirstChoice)
                                {
                                    inFirstChoice = false;
                                }
                                break;

                            case JsonToken.EndArray:
                                if (inChoices)
                                {
                                    inChoices = false;
                                }
                                break;
                        }
                    }
                }

                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException("响应的JSON格式无效 The JSON format of the response is invalid: ", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析响应失败 Failed to parse response: {ex.Message}");
            }
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
                    return source;
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
                           .Append(", \"text\": \"");

                string processedText = PreprocessQuotes(texts[i]);
                TextProcessor.AppendEscapeJsonString(processedText, jsonBuilder);

                jsonBuilder.Append("\"}");
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
                positionedBuilder.Replace("\r", "");
            }
            return positionedBuilder.ToString();
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

        private string BuildTranslationRequestJson(string formattedTexts)
        {
            payloadBuilder.Length = 0;

            payloadBuilder
                .Append("{\"model\":\"").Append(cachedModelName)
                .Append("\",\"messages\":[{\"role\":\"system\",\"content\":\"")
                .Append(cachedPrompt)
                .Append("\"},{\"role\":\"user\",\"content\":\"")
                .Append(formattedTexts)
                .Append("\"}],\"temperature\":").Append(cachedTemperature)
                .Append(",\"max_tokens\":").Append(cachedMaxTokens)
                .Append(",\"top_p\":").Append(cachedTopP);

            if (cachedTopK > 0)
            {
                payloadBuilder.Append(",\"top_k\":").Append(cachedTopK);
            }

            payloadBuilder
                .Append(",\"frequency_penalty\":").Append(cachedFrequencyPenalty)
                .Append(",\"n\":1");

            if (!string.IsNullOrEmpty(cachedExtraParams))
            {
                payloadBuilder.Append(',').Append(cachedExtraParams);
            }

            payloadBuilder.Append('}');

            return payloadBuilder.ToString();
        }

        public IEnumerator StartSingleTranslation(List<string> texts, Action<List<string>> callback)
        {
            string payloadJson = BuildTranslationRequestJson(TextProcessor.EscapeJsonString(ConvertListToFormatted(texts)));

            int retryCount = 0;
            bool needRetry = false;

            for (; ; )
            {
                if (needRetry)
                {
                    if (retryCount < config.MaxRetryCount)
                    {
                        retryCount++;
                        Debug.Log($"[{config.TranslationAPI}] 正在重试。。。尝试第 {retryCount} 次。Retrying... Attempt time {retryCount}.");
                        needRetry = false;
                        yield return new WaitForSecondsRealtime(config.RetryInterval);
                    }
                    else
                    {
                        Debug.LogError($"[{config.TranslationAPI}] 多次尝试失败。已重试 {config.MaxRetryCount} 次。翻译中止！Multiple attempts failed. Retried {config.MaxRetryCount} times. Translation aborted!");
                        callback?.Invoke(null);
                        yield break;
                    }
                }

                using (UnityWebRequest request = new UnityWebRequest(config.LlmBaseUrl, "POST"))
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
                        List<string> translatedTexts = null;
                        bool success = false;

                        try
                        {
                            translatedTexts = ParseFormattedResponse(responseJson, texts);

                            if (translatedTexts != null && translatedTexts.Count > 0)
                            {
                                callback?.Invoke(translatedTexts);
                                translatedTexts = null;
                                success = true;
                                yield break;
                            }
                            else
                            {
                                Debug.LogError("翻译失败，未获得翻译结果！Translation failed, no translation result obtained!");
                                needRetry = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result: {ex.Message}");
                            Debug.LogError($"响应JSON Response JSON: \n{responseJson}");
                            needRetry = true;
                        }
                        finally
                        {
                            if (!success && translatedTexts != null)
                            {
                                Pools.listStringPool.Return(translatedTexts);
                            }
                        }

                        continue;
                    }
                }
            }
        }

        public IEnumerator StartTranslation(List<string> texts, Action<List<string>> callback)
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


        public IEnumerator StartParallelBatchTranslation(List<string> texts, Action<List<string>> callback)
        {
            int totalBatches = texts.Count;
            int completedBatches = 0;
            bool anyFailed = false;

            List<string> batchResults = Pools.listStringPool.Get();
            for (int i = 0; i < totalBatches; i++)
            {
                batchResults.Add(null);
            }

            for (int i = 0; i < totalBatches; i++)
            {
                int batchIndex = i;
                string singleText = texts[i];

                TranslationManager.instance.StartCoroutine(SendBatchRequest(singleText, batchIndex, (success, result) =>
                {
                    completedBatches++;

                    if (success && !string.IsNullOrEmpty(result))
                    {
                        batchResults[batchIndex] = result;
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

                            Pools.listStringPool.Return(batchResults);
                            callback?.Invoke(null);
                        }
                        else
                        {
                            bool hasEmptyResult = false;
                            foreach (var item in batchResults)
                            {
                                if (string.IsNullOrEmpty(item))
                                {
                                    hasEmptyResult = true;
                                    break;
                                }
                            }

                            if (hasEmptyResult)
                            {
                                Debug.LogError("并行批量模式存在空结果。There are empty results in parallel batch mode.");
                                Pools.listStringPool.Return(batchResults);
                                callback?.Invoke(null);
                            }
                            else
                            {
                                callback?.Invoke(batchResults);
                            }
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
            string payloadJson = BuildTranslationRequestJson(singleText.Replace("\r", ""));

            int retryCount = 0;
            bool needRetry = false;

            for (; ; )
            {
                if (needRetry)
                {
                    if (retryCount < config.MaxRetryCount)
                    {
                        retryCount++;
                        Debug.Log($"[{config.TranslationAPI}] 批次 {batchIndex} 正在重试... 尝试第 {retryCount} 次。Batch {batchIndex} is retrying... Attempt time {retryCount}.");
                        needRetry = false;
                        yield return new WaitForSecondsRealtime(config.RetryInterval);
                    }
                    else
                    {
                        Debug.LogError($"[{config.TranslationAPI}] 批次 {batchIndex} 多次尝试失败。已重试 {config.MaxRetryCount} 次。Multiple attempts failed in batch {batchIndex}. Retried {config.MaxRetryCount} times. Translation aborted!");
                        batchCallback?.Invoke(false, null);
                        yield break;
                    }
                }

                using (UnityWebRequest request = new UnityWebRequest(config.LlmBaseUrl, "POST"))
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
