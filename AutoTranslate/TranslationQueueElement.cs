namespace AutoTranslate
{
    public struct TranslationQueueElement
    {
        public string text;
        public TextObject textObject;

        public TranslationQueueElement(string text, TextObject textObject)
        {
            this.text = text;
            this.textObject = textObject;
        }
    }
}
