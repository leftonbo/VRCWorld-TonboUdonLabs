using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchUrl001 : PersistenceProbe
    {
        [UdonSynced] public VRCUrl V001 = new VRCUrl("");
    }
}
