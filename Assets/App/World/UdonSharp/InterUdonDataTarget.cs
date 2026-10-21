using System;
using UdonSharp;
using VRC.SDK3.Data;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InterUdonDataTarget : UdonSharpBehaviour
    {
        [NonSerialized] public int Hp, Money, Result;
        [NonSerialized] public int[] StateArray, CallArray, BatchArray;
        [NonSerialized] public DataList StateList, CallList;
        public int HpProperty { get; set; }
        public int MoneyProperty { get; set; }

        public void _SetHp(int value) { Hp = value; }
        public void _SetMoney(int value) { Money = value; }
        public int _GetHp() { return Hp; }
        public int _GetMoney() { return Money; }
        public int _Calculate(int hp, int money) { return hp * 3 + money; }
        public void _Fields() { Result = Hp * 3 + Money; }
        public void _Array() { CallArray[2] = CallArray[0] * 3 + CallArray[1]; }
        public void _List() { CallList[2] = CallList[0].Int * 3 + CallList[1].Int; }
        public void _ArrayArgument(int[] buffer) { buffer[2] = buffer[0] * 3 + buffer[1]; }
        public void _ListArgument(DataList buffer) { buffer[2] = buffer[0].Int * 3 + buffer[1].Int; }
        public void _Batch()
        {
            int count = BatchArray.Length / 3;
            for (int i = 0; i < count; i++)
                BatchArray[count * 2 + i] = BatchArray[i] * 3 + BatchArray[count + i];
        }
        public int _ReadArray() { return StateArray[0] + StateArray[1]; }
        public int _ReadList() { return StateList[0].Int + StateList[1].Int; }
        public void _MutateState()
        {
            StateArray[0] += 7; StateArray[1] += 11;
            StateList[0] = StateList[0].Int + 7; StateList[1] = StateList[1].Int + 11;
        }
    }
}
