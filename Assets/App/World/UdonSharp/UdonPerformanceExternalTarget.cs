namespace App
{
    using UdonSharp;

    public class UdonPerformanceExternalTarget : UdonSharpBehaviour
    {
        [System.NonSerialized] public int PublicField;
        [System.NonSerialized] public int Sink;
        [System.NonSerialized] public int EventSink;

        private int _input;
        private int _eventInput;

        public int PublicProperty { get; set; }

        private void Start()
        {
            PublicField = 7;
            PublicProperty = 7;
            ResetState();
        }

        public void ResetState()
        {
            _input = 0;
            _eventInput = 0;
            Sink = 0;
            EventSink = 0;
        }

        public int PublicNoArgReturn()
        {
            _input++;
            return _input;
        }

        public void PublicNoArgVoid()
        {
            _input++;
            Sink += _input;
        }

        public int PublicArgReturn(int value)
        {
            return value + 1;
        }

        public void PublicArgVoid(int value)
        {
            Sink += value + 1;
        }

        public void PublicOut(int value, out int output)
        {
            output = value + 1;
        }

        public void PublicRef(ref int value)
        {
            value += 1;
        }

        public void PublicEventNoArg()
        {
            _eventInput++;
            EventSink += _eventInput;
        }
    }
}
