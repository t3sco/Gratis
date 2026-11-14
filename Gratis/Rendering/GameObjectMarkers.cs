namespace Gratis.Rendering;

public enum PetOwner { None, Own, Party, Alliance }

// Which object-table battle NPCs get a Gratis marker. Pure so the rules can be tested without the game.
public static class GameObjectMarkers {
    private const uint NoOwner = 0xE0000000;

    // BNpcName rows "striking dummy" and "timeworn striking dummy".
    public static bool IsDummy(uint nameId) => nameId is 541 or 13078;

    public static PetOwner Owner(uint ownerId, uint localId, bool ownerInParty, bool ownerInAlliance) =>
        ownerId is 0 or NoOwner ? PetOwner.None
        : ownerId == localId ? PetOwner.Own
        : ownerInParty ? PetOwner.Party
        : ownerInAlliance ? PetOwner.Alliance
        : PetOwner.None;

    // Your own pet always; party and alliance pets follow the "Other players" setting.
    public static bool ShowsPet(PetOwner owner, bool showPlayers) =>
        owner == PetOwner.Own || (owner is PetOwner.Party or PetOwner.Alliance && showPlayers);

    public static string Relation(PetOwner owner) => owner switch {
        PetOwner.Own => "Your pet",
        PetOwner.Party => "Party pet",
        PetOwner.Alliance => "Alliance pet",
        _ => "",
    };
}
