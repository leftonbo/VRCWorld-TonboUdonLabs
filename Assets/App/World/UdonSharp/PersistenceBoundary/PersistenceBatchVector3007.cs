using UdonSharp;
using UnityEngine;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchVector3007 : PersistenceProbe
    {
        [UdonSynced] public Vector3 V001;
        [UdonSynced] public Vector3 V002;
        [UdonSynced] public Vector3 V003;
        [UdonSynced] public Vector3 V004;
        [UdonSynced] public Vector3 V005;
        [UdonSynced] public Vector3 V006;
        [UdonSynced] public Vector3 V007;
    }
}
