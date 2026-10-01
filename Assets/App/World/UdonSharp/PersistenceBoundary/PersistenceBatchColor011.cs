using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchColor011 : PersistenceProbe
    {
        [UdonSynced] public Color V001 = new Color();
        [UdonSynced] public Color V002 = new Color();
        [UdonSynced] public Color V003 = new Color();
        [UdonSynced] public Color V004 = new Color();
        [UdonSynced] public Color V005 = new Color();
        [UdonSynced] public Color V006 = new Color();
        [UdonSynced] public Color V007 = new Color();
        [UdonSynced] public Color V008 = new Color();
        [UdonSynced] public Color V009 = new Color();
        [UdonSynced] public Color V010 = new Color();
        [UdonSynced] public Color V011 = new Color();
    }
}
