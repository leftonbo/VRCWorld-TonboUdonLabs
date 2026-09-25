using System;
using UdonSharp;
using VRC.SDK3.Data;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class SharedDataTarget : UdonSharpBehaviour
    {
        [NonSerialized] public int Input;
        [NonSerialized] public int Output;
        [NonSerialized] public int[] Buffer;
        [NonSerialized] public DataList List;

        public int _Argument(int value) { return value + 1; }
        public void _Fields() { Output = Input + 1; }
        public void _Array() { Buffer[1] = Buffer[0] + 1; }
        public void _List() { List[1] = List[0].Int + 1; }
        public void _ArrayArgument(int[] value) { value[1] = value[0] + 1; }
        public void _ListArgument(DataList value) { value[1] = value[0].Int + 1; }
        public void _ArrayBatch()
        {
            for (int i = 0; i < Buffer.Length; i++) Buffer[i] = Buffer[i] + 1;
        }
    }
}
