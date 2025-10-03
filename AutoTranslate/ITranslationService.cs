using System;
using System.Collections;
using System.Collections.Generic;

namespace AutoTranslate
{
    public interface ITranslationService
    {
        IEnumerator StartTranslation(List<string> texts, Action<List<string>> callback);
    }
}
