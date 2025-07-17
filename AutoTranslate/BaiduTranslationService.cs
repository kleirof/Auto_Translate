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

        public BaiduTranslationService(AutoTranslateConfig config)
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

            if (jsonResponse["error_code"] != null)
            {
                string errorCode = jsonResponse["error_code"]?.ToString();
                string errorMsg = jsonResponse["error_msg"]?.ToString();

                throw new InvalidOperationException($"API请求失败 API request failed: {errorCode} - {errorMsg}");
            }

            var transResults = jsonResponse["trans_result"];
            if (transResults == null)
            {
                throw new InvalidOperationException("响应JSON中缺少trans_result字段！The 'trans_desult' field is missing in the response JSON!");
            }
            if (!transResults.Any())
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            string[] translatedTexts = transResults
                .Select(result => result["dst"]?.ToString() ?? string.Empty)
                .ToArray();

            return translatedTexts;
        }

        public static string[] PreprocessText(List<string> inputTexts)
        {
            return inputTexts.Select(text => text.Replace("\n", "\\n").Replace("\r", "")).ToArray();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<string[]> callback)
        {
            string salt = Guid.NewGuid().ToString();
            string[] preprocessedTexts = PreprocessText(texts);
            string sign = GenerateSign(config.BaiduAppId, string.Join("\n", preprocessedTexts), salt, config.BaiduSecretKey);

            string url = $"{apiUrl}?q={Uri.EscapeDataString(string.Join("\n", preprocessedTexts))}&from={config.BaiduSourceLanguage}&to={config.BaiduTargetLanguage}&appid={config.BaiduAppId}&salt={salt}&sign={sign}";

            bool needRetry = false;
            int retryCount = 0;

            for(; ; )
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

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
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


        private string GenerateSign(string appId, string query, string salt, string secretKey)
        {
            string rawString = $"{appId}{query}{salt}{secretKey}";
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(rawString));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
