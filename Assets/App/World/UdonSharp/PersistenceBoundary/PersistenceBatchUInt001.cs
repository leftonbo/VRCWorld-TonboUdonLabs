using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchUInt001 : PersistenceProbe
    {
        [UdonSynced] public uint V001 = 0;
    }
}
