namespace MystTiq.Core.Models;

// v0.6.15.0: the read/edit shape for a single Pal instance decoded from Level.sav's
// CharacterSaveParameterMap. Scoped to scalar/identity fields only (see the v0.6.15.0
// architecture doc for why skill lists, species, and inventory are explicitly out of scope this
// pass) -- every field on PalEditFieldChanges is optional so only what's supplied is mutated,
// and the shape leaves room to add skill-list fields later without a breaking change.
public sealed record PalInstanceSnapshot(string InstanceId,string CharacterId,bool IsBoss,string NickName,int Level,int Rank,int TalentHp,int TalentShot,int TalentDefense,string Gender,bool IsRarePal,string? OwnerPlayerId,string? OwnerPlayerName);
public sealed record PalEditFieldChanges(string? NickName,int? Level,int? Rank,int? TalentHp,int? TalentShot,int? TalentDefense,string? Gender,bool? IsRarePal);
