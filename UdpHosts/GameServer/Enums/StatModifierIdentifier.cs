namespace GameServer.Enums;

public enum StatModifierIdentifier : ushort
{
    // TODO: Map out these
    RunSpeedMult = 1,
    FwdRunSpeedMult = 2, // 1189
    JumpHeightMult = 3, // 1189
    DamageTaken = 4, // Damage Resistance? Boomerang, Heavy Armor, invincibility (ability 31333)
    DamageDealt = 5, // 1189
    Health = 6, // 1189
    MaxHealth = 7, // 1189
    Shields = 8, // 1189
    MaxShields = 9, // 1189
    AirControlMult = 10, // Air Control? Based on Hover Mode, command 859100 is loading Hover Mode Air Control stat right before 859099 which modifies this stat
    Energy = 11,
    MaxEnergy = 12,
    EnergyRechargeDelay = 13,
    EnergyRechargeRate = 14,
    ThrustStrengthMult = 15,
    ThrustAirControl = 16, // Air Control? Based Engineer Overclock
    Friction = 17,
    SinBonus = 18,
    SinVulnerability = 19,
    MaxTurnRate = 20, // Seems confirmed by msg 551119, command 1120999, effect 11686
    FireRateModifier = 21,
    AmmoConsumption = 22, // Based on Overcharge command 1626508, seems to hold up
    AccuracyModifier = 23,
    CooldownModifier = 24,
    Progress = 25, // Unused in 1962?

    // ability 30945 (Repair Station - death),
    // 40536. PvP Healing Ball - Burst ;
    // Melding Ultra Culex Weapon Burst Ability ;
    // 39980. Place Mine On Back destroy self ;
    // 39329. Melding Bomber Ammo Impact Effect ;
    // 39226. Chosen Warzone Event (SoW) - EMP Pickup ; Ability called from powerup proximity
    // 34815. Healing Ball - Airburst ;
    // 34604. Null Healing Grenade Impact Ability ; Nullifies healing done to anyone it hits
    HealingReceivedMult = 26,

    TurnSpeed = 27,
    ExperienceMult = 28,
    WeaponSplashRadius = 29,
    Damage = 30,
    Lifesteal = 31,
    PoisonDamage = 32,
    PiercingDamage = 33,
    IceDamage = 34,
    JetEnergy = 35,
    JetEnergyRechargeDelay = 36,
    JumpHeight = 37,
    CraterDamage = 38,
    CraterSplashRadius = 39,
    CraterRecharge = 40,
    AfterburnerImpulsePotency = 41,
    AfterburnerSpeedBonusDuration = 42,
    EliteRankXpBonus = 43, // VIP Related
    MissileBarrageDamage = 44,
    XpBonus = 45, // VIP Related
    Unknown_46 = 46,
    OverchargeDuration = 47,
    OverchargePotency = 48,
    OverchargeRecharge = 49,
    GravityMult = 50,
    Unknown_51 = 51,
    Unknown_52 = 52,
    Unknown_53 = 53,
    ResourceStatusBonus = 54, // VIP Related
    Unknown_55 = 55, // No modifiers 1962?
    Unknown_56 = 56, // Jet Energy Consumption? Damage Resistance? Heavy Armor // AirResMult?
    Unknown_57 = 57,

    // FIXME: These are on the combat controller and we use fake ids until we've mapped them to the correct indexes above so that we can reference them by name instead.
    WeaponChargeupMod = 9914,
    WeaponDamageDealtMod = 9915,
    AirResistanceMult = 9913,
    TimeDilation = 9910,
}