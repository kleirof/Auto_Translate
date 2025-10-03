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

        private StringBuilder stringBuilder = new StringBuilder(256);
        private ReusableStringReader pooledReader = new ReusableStringReader();

        public AzureTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
        }

        private List<string> ParseResponse(string responseJson)
        {
            var translations = Pools.listStringPool.Get();

            try
            {
                pooledReader.Reset(responseJson);
                using (var reader = new JsonTextReader(pooledReader))
                {
                    bool inRootArray = false;
                    bool inTranslationsArray = false;
                    bool inTranslationObject = false;
                    bool lookingForText = false;
                    int rootDepth = -1;
                    int translationsDepth = -1;
                    int translationDepth = -1;

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.StartArray:
                                if (!inRootArray)
                                {
                                    inRootArray = true;
                                    rootDepth = reader.Depth;
                                }
                                else if (inRootArray && !inTranslationsArray && reader.Path.EndsWith(".translations"))
                                {
                                    inTranslationsArray = true;
                                    translationsDepth = reader.Depth;
                                }
                                break;

                            case JsonToken.StartObject:
                                if (inTranslationsArray && reader.Depth == translationsDepth + 1)
                                {
                                    inTranslationObject = true;
                                    translationDepth = reader.Depth;
                                }
                                break;

                            case JsonToken.PropertyName:
                                string propertyName = (string)reader.Value;
                                if (inTranslationObject && propertyName == "text" && reader.Depth == translationDepth + 1)
                                {
                                    lookingForText = true;
                                }
                                break;

                            case JsonToken.String:
                                if (lookingForText && inTranslationObject)
                                {
                                    string text = reader.Value?.ToString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        translations.Add(text);
                                    }
                                    lookingForText = false;
                                }
                                break;

                            case JsonToken.EndObject:
                                if (inTranslationObject && reader.Depth == translationDepth)
                                {
                                    inTranslationObject = false;
                                    translationDepth = -1;
                                }
                                break;

                            case JsonToken.EndArray:
                                if (inTranslationsArray && reader.Depth == translationsDepth)
                                {
                                    inTranslationsArray = false;
                                    translationsDepth = -1;
                                }
                                else if (inRootArray && reader.Depth == rootDepth)
                                {
                                    inRootArray = false;
                                    rootDepth = -1;
                                }
                                break;
                        }
                    }
                }

                if (translations.Count == 0)
                {
                    throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
                }

                return translations;
            }
            catch (Exception)
            {
                Pools.listStringPool.Return(translations);
                throw;
            }
        }

        public static string[] PreprocessText(List<string> inputTexts)
        {
            return inputTexts.Select(text => text.Replace("\r", "")).ToArray();
        }

        private string BuildAzurePayloadJson(string[] preprocessedTexts)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("[");

            for (int i = 0; i < preprocessedTexts.Length; i++)
            {
                if (i > 0) stringBuilder.Append(",");
                stringBuilder.Append("{\"Text\":\"");
                TextProcessor.AppendEscapeJsonString(preprocessedTexts[i], stringBuilder);
                stringBuilder.Append("\"}");
            }

            stringBuilder.Append("]");
            return stringBuilder.ToString();
        }

        private string BuildAzureRequestUrl(string endpoint, string action, string version, string targetLang, string sourceLang)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append(endpoint)
                         .Append(action)
                         .Append("?api-version=")
                         .Append(version)
                         .Append("&to=")
                         .Append(targetLang);

            if (!string.IsNullOrEmpty(sourceLang))
            {
                stringBuilder.Append("&from=")
                             .Append(sourceLang);
            }

            return stringBuilder.ToString();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<List<string>> callback)
        {
            string[] preprocessedTexts = PreprocessText(texts);

            string payloadJson = BuildAzurePayloadJson(preprocessedTexts);

            string subscriptionKey = config.AzureSubscriptionKey;
            string region = config.AzureRegion;

            string requestUrl = BuildAzureRequestUrl(endpoint, action, version, config.AzureTargetLanguage, config.AzureSourceLanguage);

            int retryCount = 0;
            bool needRetry = false;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

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
                        Debug.LogError($"[{config.TranslationAPI}] 多次尝试失败。已重试 {config.MaxRetryCount} 次。翻译中止！Multiple attempts failed. Retried {config.MaxRetryCount} times. translation aborted!");
                        callback?.Invoke(null);
                        yield break;
                    }
                }

                using (UnityWebRequest request = new UnityWebRequest(requestUrl, "POST"))
                {
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
                        List<string> translatedTexts = null;
                        bool success = false;

                        try
                        {
                            translatedTexts = ParseResponse(responseJson);

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
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result：{ex.Message}");
                            Debug.LogError($"响应JSON Response JSON：\n{responseJson}");
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
    }
}
