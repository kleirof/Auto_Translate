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
        }

        public void Set(object obj)
        {
            if (ReferenceEquals(target, obj))
                return;

            Reset();

            target = obj;
            isComponent = target is Component;
            cachedHashCode = RuntimeHelpers.GetHashCode(obj);
            cachedInstanceID = 0;

            if (isComponent)
            {
                var comp = target as Component;
                cachedInstanceID = comp.GetInstanceID();
            }
        }

        public void Retain(int count = 1) => refCount += count;

        public void Release()
        {
            if (refCount <= 0) return;
            refCount--;
            if (refCount == 0)
                Pools.textObjectPool.Return(this);
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
            IsAlive ? $"{target.GetType().Name}" : $"TextObject(Dead) {target == null}";

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
    }
}