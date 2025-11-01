using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace AutoTranslate
{
    public class TextObject : IEquatable<TextObject>
    {
        private object target;
        private bool isDead;
        private int cachedHashCode;
        private bool isTargetType;
        private int cachedInstanceID;
        private int refCount;
        private int cachedGoID;
        private List<int> parentGOIDs;

        private static readonly Dictionary<int, TextObject> map = new Dictionary<int, TextObject>(128);

        private static readonly Dictionary<int, List<int>> goToCompMap = new Dictionary<int, List<int>>(128);

        private static readonly Dictionary<object, TextObject> objectMap = new Dictionary<object, TextObject>(128);

        public object Target => target;
        public bool IsAlive => !isDead && target != null;
        public object Object => IsAlive ? target : null;
        public bool IsTargetType => isTargetType;
        public int InstanceID => isTargetType ? cachedInstanceID : 0;
        public int GameObjectID => isTargetType ? cachedGoID : 0;
        public List<int> ParentGOIDs => isTargetType ? parentGOIDs : null;

        public void Reset()
        {
            Unregister(this);

            Pools.listIntPool.Return(parentGOIDs);
            target = null;
            isDead = false;
            cachedHashCode = 0;
            isTargetType = false;
            cachedInstanceID = 0;
            refCount = 0;
            cachedGoID = 0;
            parentGOIDs = null;
        }

        public void Set(object obj)
        {
            if (ReferenceEquals(target, obj) && !isDead)
                return;

            parentGOIDs = Pools.listIntPool.Get();
            target = obj;
            isDead = false;
            cachedHashCode = RuntimeHelpers.GetHashCode(obj);
            isTargetType = ObjectIsTargetType(target);

            cachedGoID = 0;
            cachedInstanceID = 0;

            if (isTargetType && obj is Component comp)
            {
                cachedInstanceID = comp.GetInstanceID();
                cachedGoID = comp.gameObject != null ? comp.gameObject.GetInstanceID() : 0;

                Transform t = comp.transform;
                GameObject g = comp.gameObject;
                if (g != null)
                {
                    parentGOIDs.Add(g.GetInstanceID());
                    t = g.transform.parent;
                }
                while (t != null)
                {
                    parentGOIDs.Add(t.gameObject.GetInstanceID());
                    t = t.parent;
                }

                Register(this);
            }
            else
            {
                if (!objectMap.ContainsKey(obj))
                    objectMap.Add(obj, this);
            }
        }

        public void MarkDead()
        {
            if (!IsAlive)
                return;
            isDead = true;
        }

        public void Retain(int count = 1)
        {
            refCount += count;
        }

        public void Release()
        {
            if (refCount <= 0)
                return;

            refCount--;
            if (refCount == 0)
            {
                Pools.textObjectPool.Return(this);
            }
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

        public static bool ObjectIsTargetType(object obj)
        {
            return obj is Component && (obj is dfLabel || obj is dfButton || obj is tk2dTextMesh);
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
            return result;
        }


        public static TextObject GetUnityTextObject(UnityEngine.Object uo)
        {
            if (uo == null) return null;
            int id = uo.GetInstanceID();
            if (map.TryGetValue(id, out var existed))
            {
                existed.Retain();
                return existed;
            }
            var result = Pools.textObjectPool.Get();
            result.Set(uo);
            result.Retain();
            return result;
        }

        public static TextObject GetTextObject(object obj)
        {
            if (TextObject.ObjectIsTargetType(obj))
                return GetUnityTextObject(obj as UnityEngine.Object);
            else
                return GetNonUnityTextObject(obj);
        }

        public static void Register(TextObject to)
        {
            if (!(to.Target is UnityEngine.Object uo) || uo == null) return;

            int id = uo.GetInstanceID();
            if (!map.ContainsKey(id))
                map[id] = to;

            var parents = to.ParentGOIDs;
            if (parents == null || parents.Count == 0)
                return;
            foreach (var goId in parents)
            {
                if (!goToCompMap.TryGetValue(goId, out var list))
                {
                    list = Pools.listIntPool.Get();
                    goToCompMap[goId] = list;
                }
                if (!list.Contains(id))
                    list.Add(id);
            }
        }

        public static void MarkDead(UnityEngine.Object uo)
        {
            if (uo == null) return;

            if (uo is Component comp)
            {
                int compId = comp.GetInstanceID();
                MarkDeadByID(compId);
            }
            else if (uo is GameObject go)
            {
                int goId = go.GetInstanceID();
                if (goToCompMap.TryGetValue(goId, out var compList))
                {
                    goToCompMap.Remove(goId);

                    var compIds = compList.ToArray();

                    foreach (var compId in compIds)
                    {
                        MarkDeadByID(compId);
                    }

                    compList.Clear();
                    Pools.listIntPool.Return(compList);
                }
            }
        }

        public static void Unregister(TextObject to)
        {
            if (to == null)
                return;

            if (to.IsTargetType)
            {
                int compId = to.InstanceID;
                map.Remove(compId);

                var parents = to.ParentGOIDs;
                if (parents == null || parents.Count == 0)
                    return;

                foreach (var goId in parents)
                {
                    if (goToCompMap.TryGetValue(goId, out var list) && list != null)
                    {
                        list.Remove(compId);

                        if (list.Count == 0)
                        {
                            list.Clear();
                            Pools.listIntPool.Return(list);
                            goToCompMap.Remove(goId);
                        }
                    }
                }
            }
            else
            {
                if (to.Target != null)
                    objectMap.Remove(to.Target);
            }
        }

        public static void SafeRelease(TextObject to)
        {
            if (to == null)
                return;
            to.Release();
        }

        public static void MarkDeadByID(int id)
        {
            if (map.TryGetValue(id, out var to) && to.IsAlive)
            {
                to.MarkDead();
                Unregister(to);
            }
        }

        public static void MarkIfTarget(object obj)
        {
            if (obj == null)
                return;

            if (obj is UnityEngine.Object uo)
                MarkDead(uo);
        }
    }
}