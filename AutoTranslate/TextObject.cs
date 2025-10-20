using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

        public object Target => target;
        public bool IsAlive => !isDead && target != null;
        public object Object => IsAlive ? target : null;
        public bool IsTargetType => isTargetType;
        public int InstanceID => isTargetType ? cachedInstanceID : 0;
        public int GameObjectID => isTargetType ? cachedGoID : 0;
        public List<int> ParentGOIDs => isTargetType ? parentGOIDs : null;

        public void Reset()
        {
            if (isTargetType)
                TextObjectManager.Unregister(this);

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

                TextObjectManager.Register(this);
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
    }
}
