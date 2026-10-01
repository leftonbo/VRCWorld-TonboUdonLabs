using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchULong001 : PersistenceProbe
    {
        [UdonSynced] public ulong V001 = 0;
    }
}
