using System.IO;

namespace AutoTranslate
{
    class ReusableStringReader : TextReader
    {
        private string _s;
        private int _pos;

        public void Reset(string s)
        {
            _s = s;
            _pos = 0;
        }

        public override int Read()
        {
            if (_s == null || _pos >= _s.Length) return -1;
            return _s[_pos++];
        }

        public override int Peek()
        {
            if (_s == null || _pos >= _s.Length) return -1;
            return _s[_pos];
        }
    }
}
