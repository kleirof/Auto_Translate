using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoTranslate
{
    public static class Pools
    {
        public static readonly ObjectPool<List<string>> listStringPool = new ObjectPool<List<string>>(() => new List<string>(), 128, list => list.Clear(), 128);
        public static readonly ObjectPool<List<TextObject>> listTextObjectPool = new ObjectPool<List<TextObject>>(() => new List<TextObject>(), 128, list => list.Clear(), 128);
        public static readonly ObjectPool<TextObject> textObjectPool = new ObjectPool<TextObject>(() => new TextObject(), 256, obj => obj.Reset(), 256);
    }
}
