using System;
using UnityEngine;

namespace AutoTranslate
{
    public class DestroyNotifier : MonoBehaviour
    {
        public Action<DestroyNotifier> OnDestroyed;

        private void OnDestroy()
        {
            OnDestroyed?.Invoke(this);
        }
    }
}
