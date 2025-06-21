using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoTranslate
{
    public class AzureTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        private string endpoint = "https://api.cognitive.microsofttranslator.com";
        private string action = "/translate";
        private string version = "3.0";

        public AzureTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
        }

        private string[] ParseResponse(string responseJson)
        {
            JArray jsonResponse;

            try
            {
                jsonResponse = JArray.Parse(responseJson);
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException("响应的JSON格式无效 The JSON format of the response is invalid: ", ex);
            }

            var translations = new List<string>();

            foreach (var item in jsonResponse)
            {
                if (item["translations"] is JArray translationsArray)
                {
                    foreach (var translation in translationsArray)
                    {
                        translations.Add(translation["text"].ToString());
                    }
                }
            }

            if (translations.Count == 0)
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            return translations.ToArray();
        }

        public static string[] PreprocessText(List<string> inputTexts)
        {
            return inputTexts.Select(text => text.Replace("\r", "")).ToArray();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<string[]> callback)
        {
            string[] preprocessedTexts = PreprocessText(texts);

            var payload = preprocessedTexts.Select(text => new { Text = text }).ToArray();
            string payloadJson = JsonConvert.SerializeObject(payload);

            string subscriptionKey = config.AzureSubscriptionKey;
            string region = config.AzureRegion;
            string requestUrl = $"{endpoint}{action}?api-version={version}&to={config.AzureTargetLanguage}";
            if (config.AzureSourceLanguage != string.Empty)
                requestUrl += $"&from={config.AzureSourceLanguage}";

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
                        Debug.LogError($"多次尝试失败。已重试 {config.MaxRetryCount} 次。翻译中止！Multiple attempts failed. Retried {config.MaxRetryCount} times. translation aborted!");
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
                    request.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);
                    request.SetRequestHeader("Ocp-Apim-Subscription-Region", region);

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
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result：{ex.Message}");
                            Debug.LogError($"响应JSON Response JSON：\n{responseJson}");
                            needRetry = true;
                            continue;
                        }

                        if (translatedTexts != null)
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
