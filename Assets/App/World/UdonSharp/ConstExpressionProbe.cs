using System;
using UdonSharp;

namespace App
{
    /// <summary>Const式の参照と外側の演算を独立したVMエントリで比較します。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ConstExpressionProbe : UdonSharpBehaviour
    {
        #region Constants
        private const int K = 2 + 3;
        private const int D = 20 / 3;
        private const int Nested = K * 2 - 1;
        #endregion

        #region Runtime inputs and outputs
        [NonSerialized] public int Input;
        [NonSerialized] public int Result;
        #endregion

        #region Cases
        public void ConstA() { Result = Input * K; }
        public void ConstB() { Result = K * Input; }
        public void Literal() { Result = Input * 5; }
        public void Parenthesized() { Result = Input * (2 + 3); }
        public void DivisionConst() { Result = Input * D; }
        public void DivisionLiteral() { Result = Input * 6; }
        public void NestedConst() { Result = Input * Nested; }
        public void LocalConst()
        {
            const int local = 2 + 3;
            Result = Input * local;
        }
        #endregion
    }
}
