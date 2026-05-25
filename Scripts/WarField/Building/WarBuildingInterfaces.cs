

namespace WarField
{
    public interface ISpawnSoldier
    {
        bool SpawnSoldier(int race, int minorT, int soldierT, uint skillType = 0);
        bool SpawnSoldierByIndex(int index);
    }
    
}

