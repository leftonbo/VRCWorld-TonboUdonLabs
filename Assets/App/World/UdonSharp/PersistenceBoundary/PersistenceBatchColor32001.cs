using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchColor32001 : PersistenceProbe
    {
        [UdonSynced] public Color32 V001 = new Color32();
    }
}
