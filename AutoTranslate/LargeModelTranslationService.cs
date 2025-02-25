using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoTranslate
{
    public class LargeModelTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        public LargeModelTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
        }

        private string[] ParseResponse(string responseJson)
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

            var translations = new List<string>();

            if (jsonResponse["choices"] is JArray choicesArray)
            {
                foreach (var choice in choicesArray)
                {
                    var content = choice["message"]?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
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
                    }
                }
            }

            if (translations.Count == 0)
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            return translations.ToArray();
        }

        public static List<object> PreprocessText(string[] inputTexts)
        {
            var list = new List<object>();
            for (int i = 0; i < inputTexts.Length; i++)
            {
                var cleanedText = inputTexts[i].Replace("\r", "");
                list.Add(new { id = i + 1, text = cleanedText });
            }
            return list;
        }

        public IEnumerator StartTranslation(string[] texts, Action<string[]> callback)
        {
            var payload = new
            {
                model = config.LargeModelName,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = config.LargeModelPrompt
                    },
                    new
                    {
                        role = "user",
                        content = JsonConvert.SerializeObject(PreprocessText(texts))
                    }
                },
                temperature = config.LargeModelTemperature,
                max_tokens = config.LargeModelMaxTokens,
                top_p = config.LargeModelTopP,
                frequency_penalty = config.LargeModelFrequencyPenalty,
                n = 1
            };

            string payloadJson = JsonConvert.SerializeObject(payload);

            string requestUrl = $"{config.LargeModelBaseUrl}";

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
                        yield return new WaitForSecondsRealtime(2);
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
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(payloadJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {config.LargeModelApiKey}");

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
                            translatedTexts = ParseResponse(responseJson);
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
    }
}
