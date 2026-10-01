using UdonSharp;

namespace App
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PersistenceBatchByte001 : PersistenceProbe
    {
        [UdonSynced] public byte V001 = 0;
    }
}
