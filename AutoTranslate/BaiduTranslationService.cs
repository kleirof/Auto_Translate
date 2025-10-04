using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections;
using System.Text;
using System.Security.Cryptography;

namespace AutoTranslate
{
    public class BaiduTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        private string apiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";

        private ReusableStringReader pooledReader = new ReusableStringReader();
        private StringBuilder stringBuilder = new StringBuilder();

        public BaiduTranslationService(AutoTranslateConfig config)
        {
            this.config = config;
        }

        private List<string> ParseResponse(string responseJson)
        {
            var result = Pools.listStringPool.Get();

            try
            {
                pooledReader.Reset(responseJson);
                using (var reader = new JsonTextReader(pooledReader))
                {
                    bool inTransResultArray = false;
                    bool inObject = false;
                    bool lookingForDst = false;

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                string propertyName = (string)reader.Value;

                                if (propertyName == "error_code")
                                {
                                    reader.Read();
                                    string errorCode = reader.Value?.ToString();
                                    if (!string.IsNullOrEmpty(errorCode))
                                    {
                                        string errorMsg = "Unknown error";
                                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                                        {
                                            if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "error_msg")
                                            {
                                                reader.Read();
                                                errorMsg = reader.Value?.ToString() ?? "Unknown error";
                                                break;
                                            }
                                        }
                                        throw new InvalidOperationException($"API请求失败 API request failed: {errorCode} - {errorMsg}");
                                    }
                                }
                                else if (propertyName == "trans_result")
                                {
                                    inTransResultArray = true;
                                }
                                else if (inObject && propertyName == "dst")
                                {
                                    lookingForDst = true;
                                }
                                break;

                            case JsonToken.StartArray:
                                break;

                            case JsonToken.StartObject:
                                if (inTransResultArray)
                                {
                                    inObject = true;
                                }
                                break;

                            case JsonToken.String:
                                if (lookingForDst && inObject)
                                {
                                    string translatedText = reader.Value?.ToString();
                                    if (!string.IsNullOrEmpty(translatedText))
                                    {
                                        result.Add(translatedText);
                                    }
                                    lookingForDst = false;
                                }
                                break;

                            case JsonToken.EndObject:
                                if (inObject)
                                {
                                    inObject = false;
                                }
                                break;

                            case JsonToken.EndArray:
                                if (inTransResultArray)
                                {
                                    inTransResultArray = false;
                                }
                                break;
                        }
                    }
                }

                if (result.Count == 0)
                {
                    Pools.listStringPool.Return(result);
                    throw new InvalidOperationException("翻译结果为空或缺少trans_result字段！Translation result is empty or missing 'trans_result' field!");
                }

                return result;
            }
            catch (Exception ex)
            {
                Pools.listStringPool.Return(result);
                throw new InvalidOperationException($"解析响应失败 Failed to parse response: {ex.Message}");
            }
        }

        public static string[] PreprocessText(List<string> inputTexts)
        {
            return inputTexts.Select(text => text.Replace("\n", "\\n").Replace("\r", "")).ToArray();
        }

        private string JoinStringsWithSeparator(string[] strings, char separator)
        {
            if (strings == null || strings.Length == 0) return string.Empty;

            stringBuilder.Length = 0;
            for (int i = 0; i < strings.Length; i++)
            {
                if (i > 0) stringBuilder.Append(separator);
                stringBuilder.Append(strings[i]);
            }
            return stringBuilder.ToString();
        }

        private string BuildBaiduApiUrl(string apiUrl, string queryText, string sourceLang, string targetLang, string appId, string salt, string sign)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append(apiUrl)
                  .Append("?q=").Append(Uri.EscapeDataString(queryText))
                  .Append("&from=").Append(sourceLang)
                  .Append("&to=").Append(targetLang)
                  .Append("&appid=").Append(appId)
                  .Append("&salt=").Append(salt)
                  .Append("&sign=").Append(sign);
            return stringBuilder.ToString();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<List<string>> callback)
        {
            string salt = Guid.NewGuid().ToString();
            string[] preprocessedTexts = PreprocessText(texts);

            string joinedText = JoinStringsWithSeparator(preprocessedTexts, '\n');
            string sign = GenerateSign(config.BaiduAppId, joinedText, salt, config.BaiduSecretKey);
            string url = BuildBaiduApiUrl(apiUrl, joinedText, config.BaiduSourceLanguage,
                                         config.BaiduTargetLanguage, config.BaiduAppId, salt, sign);

            bool needRetry = false;
            int retryCount = 0;

            for (; ; )
            {
                if (needRetry)
                {
                    yield return null;
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

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    yield return null;
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

                        try
                        {
                            translatedTexts = ParseResponse(responseJson);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result: {ex.Message}");
                            Debug.LogError($"响应JSON Response JSON: \n{responseJson}");
                            needRetry = true;
                        }

                        if (translatedTexts != null && translatedTexts.Count > 0)
                        {
                            yield return null;
                            callback?.Invoke(translatedTexts);
                            translatedTexts = null;
                            yield break;
                        }
                        else
                        {
                            Debug.LogError("翻译失败，未获得翻译结果！Translation failed, no translation result obtained!");
                            needRetry = true;
                        }

                        continue;
                    }
                }
            }
        }

        private string GenerateSign(string appId, string query, string salt, string secretKey)
        {
            string rawString = $"{appId}{query}{salt}{secretKey}";
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(rawString));
                StringBuilder sb = new StringBuilder(1024);
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
