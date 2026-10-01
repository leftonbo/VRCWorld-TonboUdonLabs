using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchQuaternion011 : PersistenceProbe
    {
        [UdonSynced] public Quaternion V001 = new Quaternion();
        [UdonSynced] public Quaternion V002 = new Quaternion();
        [UdonSynced] public Quaternion V003 = new Quaternion();
        [UdonSynced] public Quaternion V004 = new Quaternion();
        [UdonSynced] public Quaternion V005 = new Quaternion();
        [UdonSynced] public Quaternion V006 = new Quaternion();
        [UdonSynced] public Quaternion V007 = new Quaternion();
        [UdonSynced] public Quaternion V008 = new Quaternion();
        [UdonSynced] public Quaternion V009 = new Quaternion();
        [UdonSynced] public Quaternion V010 = new Quaternion();
        [UdonSynced] public Quaternion V011 = new Quaternion();
    }
}
