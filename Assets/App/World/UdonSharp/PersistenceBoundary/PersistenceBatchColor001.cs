using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchColor001 : PersistenceProbe
    {
        [UdonSynced] public Color V001 = new Color();
    }
}
