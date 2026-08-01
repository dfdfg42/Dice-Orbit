namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 복원에 필요한 협력자 묶음. 서비스가 찾아서 참가자에게 주입한다.
    /// PartyManager가 UI를 직접 뒤지지 않게 하려는 것이다.
    /// </summary>
    public class RunRestoreContext
    {
        public CharacterSpawner Spawner;
        public SaveIdCatalog Catalog;
        public RestoreReport Report;

        /// <summary>못 찾으면 null. 카탈로그가 없으면 항상 null.</summary>
        public CharacterPreset FindPreset(string saveId)
            => Catalog != null ? Catalog.FindPreset(saveId) : null;
    }
}
