namespace AutoTranslate
{
    struct TranslationQueueElement
    {
        public string text;
        public object control;

        public TranslationQueueElement(string text, object control)
        {
            this.text = text;
            this.control = control;
        }
    }
}
