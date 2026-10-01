using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchQuaternion007 : PersistenceProbe
    {
        [UdonSynced] public Quaternion V001 = new Quaternion();
        [UdonSynced] public Quaternion V002 = new Quaternion();
        [UdonSynced] public Quaternion V003 = new Quaternion();
        [UdonSynced] public Quaternion V004 = new Quaternion();
        [UdonSynced] public Quaternion V005 = new Quaternion();
        [UdonSynced] public Quaternion V006 = new Quaternion();
        [UdonSynced] public Quaternion V007 = new Quaternion();
    }
}
