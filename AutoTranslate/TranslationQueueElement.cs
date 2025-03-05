namespace AutoTranslate
{
    class TranslationQueueElement
    {
        public string text;
        public object control;

        public void Set(string text, object control)
        {
            this.text = text;
            this.control = control;
        }

        public void Reset()
        {
            this.text = null;
            this.control = null;
        }
    }
}
