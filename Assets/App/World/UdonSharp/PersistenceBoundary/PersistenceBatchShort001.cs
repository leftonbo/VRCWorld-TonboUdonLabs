using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchShort001 : PersistenceProbe
    {
        [UdonSynced] public short V001 = 0;
    }
}
