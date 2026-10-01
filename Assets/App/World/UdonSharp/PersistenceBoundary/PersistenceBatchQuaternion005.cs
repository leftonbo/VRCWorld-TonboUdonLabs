using UdonSharp;
using UnityEngine;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchQuaternion005 : PersistenceProbe
    {
        [UdonSynced] public Quaternion V001;
        [UdonSynced] public Quaternion V002;
        [UdonSynced] public Quaternion V003;
        [UdonSynced] public Quaternion V004;
        [UdonSynced] public Quaternion V005;
    }
}
