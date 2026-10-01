using UdonSharp;
using UnityEngine;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector4005 : PersistenceProbe
    {
        [UdonSynced] public Vector4 V001;
        [UdonSynced] public Vector4 V002;
        [UdonSynced] public Vector4 V003;
        [UdonSynced] public Vector4 V004;
        [UdonSynced] public Vector4 V005;
    }
}
