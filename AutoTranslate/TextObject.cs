using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTranslate
{
    public class TextObject : IEquatable<TextObject>
    {
        private object target;
        private bool isComponent;
        private int cachedHashCode;
        private int cachedInstanceID;
        private int refCount;

        private float tokens;
        private float lastUpdateTime;
        private float lastCoolingResetTime;
        private bool coolingDown;
        private bool pendingRequest;
        private string lastExceededText;
        private bool isProcessingExceeded;
        private Coroutine exceededCoroutine;

        private const float MAX_TOKENS = 4f;
        private const float NORMAL_FILL_RATE = 1f;
        private const float COOLING_FILL_RATE = 1f;
        private const float COOLING_DURATION = 1f;

        private int generationId;
        private static int nextGenerationId = 1;

        private bool isProcessingRequest = false;

        private bool textUpdated = false;

        public bool HasTextUpdated => textUpdated;

        public object Target => target;

        public bool IsAlive
        {
            get
            {
                if (target == null) return false;
                if (isComponent)
                    return (target as Component) != null;
                return true;
            }
        }

        public object Object => IsAlive ? target : null;
        public int InstanceID => isComponent ? cachedInstanceID : 0;

        public bool HasExceededRequest => !string.IsNullOrEmpty(lastExceededText);
        public string LastExceededText => lastExceededText;
        public bool IsProcessingExceeded => isProcessingExceeded;
        public Coroutine ExceededCoroutine => exceededCoroutine;
        public float CurrentTokens => tokens;
        public float LastUpdateTime => lastUpdateTime;
        public bool IsCoolingDown => coolingDown;
        public bool HasPendingRequest => pendingRequest;
        public float CoolingProgress => coolingDown ? Mathf.Clamp01((Time.realtimeSinceStartup - lastCoolingResetTime) / COOLING_DURATION) : 0f;
        public int GenerationId => generationId;

        private static readonly Dictionary<int, TextObject> unityMap = new Dictionary<int, TextObject>(128);
        private static readonly Dictionary<object, TextObject> objectMap = new Dictionary<object, TextObject>(128);

        public void Reset()
        {
            Unregister(this);

            target = null;
            isComponent = false;
            cachedHashCode = 0;
            cachedInstanceID = 0;
            refCount = 0;

            tokens = MAX_TOKENS;
            lastUpdateTime = Time.realtimeSinceStartup;
            lastCoolingResetTime = 0f;
            coolingDown = false;
            pendingRequest = false;
            lastExceededText = null;
            isProcessingExceeded = false;
            exceededCoroutine = null;

            isProcessingRequest = false;
            textUpdated = false;
            
            if (nextGenerationId == int.MaxValue)
                nextGenerationId = 1;
        }


        public void Set(object obj)
        {
            if (ReferenceEquals(target, obj))
                return;

            Reset();
            generationId = nextGenerationId++;

            target = obj;
            isComponent = target is Component;
            cachedHashCode = RuntimeHelpers.GetHashCode(obj);
            cachedInstanceID = 0;

            textUpdated = false;

            if (isComponent)
            {
                var comp = target as Component;
                cachedInstanceID = comp.GetInstanceID();
            }

            InitializeTokenBucket();
        }

        public void Retain(int count = 1) => refCount += count;

        public void Release()
        {
            if (refCount <= 0) return;
            refCount--;
            if (refCount == 0)
                Pools.textObjectPool.Return(this);
        }

        public bool ShouldProcessRequest(string text, bool updateState = true)
        {
            if (isProcessingRequest)
                return false;

            isProcessingRequest = true;

            try
            {
                float currentTime = Time.realtimeSinceStartup;
                UpdateTokens(currentTime);

                if (coolingDown)
                {
                    if (updateState)
                    {
                        lastCoolingResetTime = currentTime;
                        pendingRequest = true;
                        lastExceededText = text;
                    }
                    return false;
                }
                else
                {
                    if (tokens >= 1f)
                    {
                        if (updateState)
                        {
                            tokens -= 1f;
                            lastExceededText = null;
                        }
                        return true;
                    }
                    else
                    {
                        if (updateState)
                        {
                            coolingDown = true;
                            lastCoolingResetTime = currentTime;
                            pendingRequest = true;
                            lastExceededText = text;
                        }
                        return false;
                    }
                }
            }
            finally
            {
                isProcessingRequest = false;
            }
        }

        public bool CanProcessRequest()
        {
            return ShouldProcessRequest(null, false);
        }

        public void UpdateTokenState()
        {
            UpdateTokens(Time.realtimeSinceStartup);
        }

        private void UpdateTokens(float currentTime)
        {
            float elapsed = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;

            if (coolingDown)
            {
                if (pendingRequest && (currentTime - lastCoolingResetTime) >= COOLING_DURATION)
                {
                    ExitCooling();
                }
                else
                {
                    tokens = Mathf.Min(MAX_TOKENS, tokens + elapsed * COOLING_FILL_RATE);
                }
            }
            else
            {
                tokens = Mathf.Min(MAX_TOKENS, tokens + elapsed * NORMAL_FILL_RATE);
            }
        }

        private void ExitCooling()
        {
            coolingDown = false;
            tokens = MAX_TOKENS;

            if (pendingRequest)
            {
                tokens -= 1f;
                pendingRequest = false;
                lastExceededText = null;
            }
        }

        private void InitializeTokenBucket()
        {
            ResetTokenBucket();
        }

        private void ResetTokenBucket()
        {
            tokens = MAX_TOKENS;
            lastUpdateTime = Time.realtimeSinceStartup;
            lastCoolingResetTime = 0f;
            coolingDown = false;
            pendingRequest = false;
            lastExceededText = null;
            isProcessingExceeded = false;
            exceededCoroutine = null;
        }

        public void SetProcessingState(bool processing, Coroutine coroutine = null)
        {
            isProcessingExceeded = processing;
            exceededCoroutine = coroutine;
        }

        public bool Equals(TextObject other)
        {
            if (other is null) return false;
            return ReferenceEquals(this.target, other.target);
        }

        public override bool Equals(object obj) => Equals(obj as TextObject);
        public override int GetHashCode() => cachedHashCode;

        public static bool operator ==(TextObject left, TextObject right) =>
            left is null ? right is null : left.Equals(right);
        public static bool operator !=(TextObject left, TextObject right) => !(left == right);

        public override string ToString() =>
            IsAlive ? $"{target.GetType().Name} (Gen:{generationId})"
                    : $"TextObject(Dead, Gen:{generationId})";

        public static TextObject GetTextObject(object obj)
        {
            if (obj == null || obj.Equals(null)) return null;

            if (obj is Component comp)
                return GetUnityTextObject(comp);
            else
                return GetNonUnityTextObject(obj);
        }

        private static TextObject GetUnityTextObject(Component comp)
        {
            if (comp == null || comp.Equals(null)) return null;

            int id = comp.GetInstanceID();
            if (unityMap.TryGetValue(id, out var existed))
            {
                existed.Retain();
                return existed;
            }

            var result = Pools.textObjectPool.Get();
            result.Set(comp);
            result.Retain();
            unityMap[id] = result;
            return result;
        }

        private static TextObject GetNonUnityTextObject(object obj)
        {
            if (objectMap.TryGetValue(obj, out var existed))
            {
                existed.Retain();
                return existed;
            }

            var result = Pools.textObjectPool.Get();
            result.Set(obj);
            result.Retain();
            objectMap.Add(obj, result);
            return result;
        }

        public static void SafeRelease(TextObject to)
        {
            if (to == null) return;
            to.Release();
        }

        public static void Unregister(TextObject to)
        {
            if (to == null) return;

            if (to.isComponent)
            {
                int id = to.InstanceID;
                unityMap.Remove(id);
            }
            else
            {
                if (to.Target != null)
                    objectMap.Remove(to.Target);
            }
        }

        public void UpdateExceededText(string text)
        {
            lastExceededText = text;
            textUpdated = true;
        }

        public void ResetTextUpdateFlag()
        {
            textUpdated = false;
        }

        public void ResetLastExceededText()
        {
            lastExceededText = null;
        }
    }
}