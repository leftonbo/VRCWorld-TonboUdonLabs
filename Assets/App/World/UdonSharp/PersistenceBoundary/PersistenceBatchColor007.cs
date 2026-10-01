using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchColor007 : PersistenceProbe
    {
        [UdonSynced] public Color V001 = new Color();
        [UdonSynced] public Color V002 = new Color();
        [UdonSynced] public Color V003 = new Color();
        [UdonSynced] public Color V004 = new Color();
        [UdonSynced] public Color V005 = new Color();
        [UdonSynced] public Color V006 = new Color();
        [UdonSynced] public Color V007 = new Color();
    }
}
