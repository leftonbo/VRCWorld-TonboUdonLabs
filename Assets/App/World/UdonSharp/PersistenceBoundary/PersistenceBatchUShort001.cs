using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchUShort001 : PersistenceProbe
    {
        [UdonSynced] public ushort V001 = 0;
    }
}
