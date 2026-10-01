using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchJson : PersistenceProbe
    {
        [UdonSynced] public string V001 = "";
    }
}
