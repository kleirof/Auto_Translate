using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Text;

namespace AutoTranslate
{
    public class TencentTranslationService : ITranslationService
    {
        private AutoTranslateConfig config;

        private string endpoint = "tmt.tencentcloudapi.com";
        private string action = "TextTranslateBatch";
        private string version = "2018-03-21";

        public TencentTranslationService(AutoTranslateConfig config)
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

            if (jsonResponse["Response"] == null)
            {
                throw new InvalidOperationException("响应中缺少Response字段！The 'Response' field is missing from the response!");
            }

            var targetTextList = jsonResponse["Response"]["TargetTextList"];
            if (targetTextList == null)
            {
                throw new InvalidOperationException("响应中缺少TargetTextList字段！The 'TargetTextList' field is missing from the response!");
            }

            string[] translatedTexts = targetTextList.ToObject<string[]>();

            if (translatedTexts == null || translatedTexts.Length == 0)
            {
                throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
            }

            return translatedTexts;
        }
        public static string[] PreprocessText(List<string> inputTexts)
        {
            return inputTexts.Select(text => text.Replace("\r", "")).ToArray();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<string[]> callback)
        {
            string[] preprocessedTexts = PreprocessText(texts);
            long timestamp = GetUnixTimeSeconds();
            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var payload = new
            {
                SourceTextList = preprocessedTexts,
                Source = config.TencentSourceLanguage,
                Target = config.TencentTargetLanguage,
                ProjectId = 0
            };
            string payloadJson = JsonConvert.SerializeObject(payload);

            string canonicalRequest = $"POST\n/\n\ncontent-type:application/json\nhost:{endpoint}\n\ncontent-type;host\n{ComputeSHA256(payloadJson)}";
            string stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{date}/tmt/tc3_request\n{ComputeSHA256(canonicalRequest)}";
            byte[] signingKey = GetSignatureKey(config.TencentSecretKey, date, "tmt", "tc3_request");
            string signature = ConvertToHexString(ComputeHMACSHA256(stringToSign, signingKey));

            string authorization = $"TC3-HMAC-SHA256 Credential={config.TencentSecretId}/{date}/tmt/tc3_request, SignedHeaders=content-type;host, Signature={signature}";

            int retryCount = 0;
            bool needRetry = false;

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

                using (UnityWebRequest request = new UnityWebRequest($"https://{endpoint}", "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", authorization);
                    request.SetRequestHeader("X-TC-Action", action);
                    request.SetRequestHeader("X-TC-Timestamp", timestamp.ToString());
                    request.SetRequestHeader("X-TC-Version", version);
                    request.SetRequestHeader("X-TC-Region", config.TencentRegion);

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

        private string ComputeSHA256(string rawData)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                return ConvertToHexString(bytes);
            }
        }

        private byte[] ComputeHMACSHA256(string data, byte[] key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }
        }

        private byte[] GetSignatureKey(string secretKey, string date, string service, string request)
        {
            byte[] kDate = ComputeHMACSHA256(date, Encoding.UTF8.GetBytes("TC3" + secretKey));
            byte[] kService = ComputeHMACSHA256(service, kDate);
            byte[] kSigning = ComputeHMACSHA256(request, kService);
            return kSigning;
        }

        private string ConvertToHexString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private long GetUnixTimeSeconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
