using System;
using System.Collections;

namespace AutoTranslate
{
    public interface ITranslationService
    {
        IEnumerator StartTranslation(string[] texts, Action<string[]> callback);
    }
}
