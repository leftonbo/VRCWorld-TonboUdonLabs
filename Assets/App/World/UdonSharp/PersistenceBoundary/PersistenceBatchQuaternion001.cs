using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchQuaternion001 : PersistenceProbe
    {
        [UdonSynced] public Quaternion V001 = new Quaternion();
    }
}
