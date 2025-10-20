using System.Collections.Generic;
using UnityEngine;

namespace AutoTranslate
{
    public static class TextObjectManager
    {
        private static readonly Dictionary<int, TextObject> map = new Dictionary<int, TextObject>();

        private static readonly Dictionary<int, List<int>> goToCompMap = new Dictionary<int, List<int>>();

        public static TextObject GetNonUnityTextObject(object so)
        {
            if (so == null) return null;
            var result = Pools.textObjectPool.Get();
            result.Set(so);
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
            if (!(to.Target is UnityEngine.Object uo)) return;

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
            if (to == null || !to.IsTargetType)
                return;

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

        public static void SafeRelease(TextObject to)
        {
            if (to == null) return;
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
            if (obj == null || !TextObject.ObjectIsTargetType(obj)) return;

            if (obj is UnityEngine.Object uo)
                MarkDead(uo);
        }
    }
}
