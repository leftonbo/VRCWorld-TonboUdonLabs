using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchSByte001 : PersistenceProbe
    {
        [UdonSynced] public sbyte V001 = 0;
    }
}
