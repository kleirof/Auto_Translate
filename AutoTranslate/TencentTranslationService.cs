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

        private ReusableStringReader pooledReader = new ReusableStringReader();
        private StringBuilder stringBuilder = new StringBuilder(1024);

        public TencentTranslationService(AutoTranslateConfig config)
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
                    bool inResponse = false;
                    bool inTargetTextList = false;
                    bool inArray = false;
                    int targetDepth = -1;

                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                string propertyName = (string)reader.Value;

                                if (propertyName == "Response" && !inResponse)
                                {
                                    inResponse = true;
                                }
                                else if (inResponse && propertyName == "TargetTextList" && !inTargetTextList)
                                {
                                    inTargetTextList = true;
                                }
                                break;

                            case JsonToken.StartArray:
                                if (inTargetTextList && !inArray)
                                {
                                    inArray = true;
                                    targetDepth = reader.Depth;
                                }
                                break;

                            case JsonToken.String:
                                if (inArray && inTargetTextList)
                                {
                                    string translatedText = reader.Value?.ToString();
                                    if (!string.IsNullOrEmpty(translatedText))
                                    {
                                        result.Add(translatedText);
                                    }
                                }
                                break;

                            case JsonToken.EndArray:
                                if (inArray && reader.Depth == targetDepth)
                                {
                                    inArray = false;
                                    inTargetTextList = false;
                                    inResponse = false;
                                }
                                break;
                        }
                    }
                }

                if (result.Count == 0)
                {
                    throw new InvalidOperationException("翻译结果为空！The translation result is empty!");
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
            return inputTexts.Select(text => text.Replace("\r", "")).ToArray();
        }

        private string BuildTencentPayloadJson(string[] preprocessedTexts, string sourceLang, string targetLang)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("{\"SourceTextList\":[");
            for (int i = 0; i < preprocessedTexts.Length; i++)
            {
                if (i > 0) stringBuilder.Append(",");
                stringBuilder.Append('"');
                TextProcessor.AppendEscapeJsonString(preprocessedTexts[i], stringBuilder);
                stringBuilder.Append('"');
            }
            stringBuilder.Append("],\"Source\":\"");
            stringBuilder.Append(sourceLang);
            stringBuilder.Append("\",\"Target\":\"");
            stringBuilder.Append(targetLang);
            stringBuilder.Append("\",\"ProjectId\":0}");
            return stringBuilder.ToString();
        }

        private string BuildCanonicalRequest(string payloadJson, string endpoint)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("POST\n/\n\ncontent-type:application/json\nhost:");
            stringBuilder.Append(endpoint);
            stringBuilder.Append("\n\ncontent-type;host\n");
            stringBuilder.Append(ComputeSHA256(payloadJson));
            return stringBuilder.ToString();
        }

        private string BuildStringToSign(long timestamp, string date, string canonicalRequest)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("TC3-HMAC-SHA256\n");
            stringBuilder.Append(timestamp);
            stringBuilder.Append('\n');
            stringBuilder.Append(date);
            stringBuilder.Append("/tmt/tc3_request\n");
            stringBuilder.Append(ComputeSHA256(canonicalRequest));
            return stringBuilder.ToString();
        }

        private string BuildAuthorization(string secretId, string date, string signature)
        {
            stringBuilder.Length = 0;
            stringBuilder.Append("TC3-HMAC-SHA256 Credential=");
            stringBuilder.Append(secretId);
            stringBuilder.Append('/');
            stringBuilder.Append(date);
            stringBuilder.Append("/tmt/tc3_request, SignedHeaders=content-type;host, Signature=");
            stringBuilder.Append(signature);
            return stringBuilder.ToString();
        }

        public IEnumerator StartTranslation(List<string> texts, Action<List<string>> callback)
        {
            string[] preprocessedTexts = PreprocessText(texts);
            long timestamp = GetUnixTimeSeconds();

            string date = DateTime.UtcNow.ToString("yyyy-MM-dd");

            string payloadJson = BuildTencentPayloadJson(preprocessedTexts, config.TencentSourceLanguage, config.TencentTargetLanguage);

            string canonicalRequest = BuildCanonicalRequest(payloadJson, endpoint);
            string stringToSign = BuildStringToSign(timestamp, date, canonicalRequest);
            byte[] signingKey = GetSignatureKey(config.TencentSecretKey, date, "tmt", "tc3_request");
            string signature = ConvertToHexString(ComputeHMACSHA256(stringToSign, signingKey));
            string authorization = BuildAuthorization(config.TencentSecretId, date, signature);

            int retryCount = 0;
            bool needRetry = false;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

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

                using (UnityWebRequest request = new UnityWebRequest($"https://{endpoint}", "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", authorization);
                    request.SetRequestHeader("X-TC-Action", action);
                    request.SetRequestHeader("X-TC-Timestamp", timestamp.ToString());
                    request.SetRequestHeader("X-TC-Version", version);
                    request.SetRequestHeader("X-TC-Region", config.TencentRegion);

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
                            Debug.LogError($"解析翻译结果失败 Failed to parse translation result：{ex.Message}");
                            Debug.LogError($"响应JSON Response JSON：\n{responseJson}");
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
            StringBuilder sb = new StringBuilder(1024);
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
