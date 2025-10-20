using System.Collections.Generic;

namespace AutoTranslate
{
    public static class Pools
    {
        public static readonly ObjectPool<List<string>> listStringPool = new ObjectPool<List<string>>(() => new List<string>(), 256, list => list.Clear(), 256);
        public static readonly ObjectPool<List<TextObject>> listTextObjectPool = new ObjectPool<List<TextObject>>(() => new List<TextObject>(), 256, list => list.Clear(), 256);
        public static readonly ObjectPool<TextObject> textObjectPool = new ObjectPool<TextObject>(() => new TextObject(), 256, obj => obj.Reset(), 256);
        public static readonly ObjectPool<List<int>> listIntPool = new ObjectPool<List<int>>(() => new List<int>(), 256, list => list.Clear(), 256);
    }
}
