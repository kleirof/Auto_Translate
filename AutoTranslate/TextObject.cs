using UnityEngine;
using System;

namespace AutoTranslate
{
    public class TextObject : IEquatable<TextObject>
    {
        private object target;
        private bool isUnityObject;
        private DestroyNotifier notifier;
        private bool isDead;

        public object Target => target;
        public bool IsUnityObject => isUnityObject;
        public bool IsAlive => !isDead && target != null;

        public object Object
        {
            get
            {
                if (!IsAlive) return null;
                return target;
            }
        }

        public void Reset()
        {
            ClearNotifier();
            target = null;
            isDead = false;
            isUnityObject = false;
        }

        public void Set(object obj)
        {
            if (ReferenceEquals(target, obj) && !isDead)
                return;

            ClearNotifier();

            target = obj;
            isDead = false;
            isUnityObject = obj is UnityEngine.Object;

            if (isUnityObject && obj is Component comp)
            {
                notifier = comp.gameObject.GetComponent<DestroyNotifier>();
                if (notifier == null)
                    notifier = comp.gameObject.AddComponent<DestroyNotifier>();

                notifier.OnDestroyed += OnTargetDestroyed;
            }
        }

        private void OnTargetDestroyed(DestroyNotifier destroyedNotifier)
        {
            isDead = true;
            ClearNotifier();
        }

        private void ClearNotifier()
        {
            if (notifier != null)
            {
                notifier.OnDestroyed -= OnTargetDestroyed;
                notifier = null;
            }
        }

        public bool Equals(TextObject other)
        {
            if (other is null) return false;
            return ReferenceEquals(this.target, other.target);
        }

        public override bool Equals(object obj) => Equals(obj as TextObject);
        public override int GetHashCode() => target?.GetHashCode() ?? 0;

        public static bool operator ==(TextObject left, TextObject right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(TextObject left, TextObject right) => !(left == right);

        public override string ToString() =>
            IsAlive ? $"{target.GetType().Name}" : "TextObject(Dead)";
    }
}
