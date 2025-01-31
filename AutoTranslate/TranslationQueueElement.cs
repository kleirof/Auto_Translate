namespace AutoTranslate
{
    class TranslationQueueElement
    {
        public string Text { get; set; }
        public object Control { get; set; }

        public TranslationQueueElement(string translation, object control)
        {
            Text = translation;
            Control = control;
        }
    }
}
