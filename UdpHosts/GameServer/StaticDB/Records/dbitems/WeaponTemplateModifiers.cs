namespace GameServer.Data.SDB.Records.dbitems;
public record class WeaponTemplateModifiers
{
    // public Vec3 FpVisualOrient { get; set; }
    // public Vec3 FpVisualOffset { get; set; }
    // public Vec2 AiSwayMin { get; set; }
    // public Vec2 AiSwayMax { get; set; }
    public float SlidePerBurstMult { get; set; }
    public float RunMinspreadAdd { get; set; }
    public float RunPerriseMult { get; set; }
    public uint EquipExitMs { get; set; }
    public float MinSpreadFrac { get; set; }
    public float JumpRiseRampMult { get; set; }
    public float MaxSpreadMult { get; set; }
    public float MinDamageMult { get; set; }
    public float HeadshotMult { get; set; }
    public uint MsSpreadReturnDelay { get; set; }
    public float RunSlideRampMult { get; set; }
    public float Agility { get; set; }
    public float SpreadPerBurstMult { get; set; }
    public float OvermaxRisePermanentFracMult { get; set; }
    public uint MinDamage { get; set; }
    public float HitshakeMult { get; set; }
    public uint MsPerBurst { get; set; }
    public uint MsChargeupMax { get; set; }
    public uint JitterRampTime { get; set; }
    public float MaxSlideMult { get; set; }
    public float SpreadRampExponent { get; set; }
    public float RunPerslideMult { get; set; }
    public float MinSpreadMult { get; set; }
    public float OvermaxSlidePermanentFracMult { get; set; }
    public uint DamagePerRound { get; set; }
    public float MinAmmoPerBurstMult { get; set; }
    public float MaxAmmoMult { get; set; }
    public float MinSlideFrac { get; set; }
    public uint? DefaultUnderbarrelId { get; set; }
    public float RisePerBurst { get; set; }
    public float NoSpreadChanceMult { get; set; }
    public uint EquipEnterMs { get; set; }
    public float AmmoPerBurstMult { get; set; }
    public uint? MeleeAbilityId { get; set; }
    public uint? AttackAbilityId { get; set; }
    public uint AiSwayHperiod { get; set; }
    public float ReloadPenaltyMult { get; set; }
    public uint WeaponId { get; set; }
    public float MinSpread { get; set; }
    public float JumpSlideRampMult { get; set; }
    public float SlidePerBurst { get; set; }
    public float JumpMinspreadAdd { get; set; }
    public float MaxRiseMult { get; set; }
    public float MsPerBurstMult { get; set; }
    public uint? OverchargeAbility { get; set; }
    public uint MsOverchargeDelay { get; set; }
    public float MinSlidePerBurst { get; set; }
    public uint MsAgilityReturn { get; set; }
    public uint? BurstAbilityId { get; set; }
    public float RisePerBurstMult { get; set; }
    public float TargetingRangeMult { get; set; }
    public float MaxJitter { get; set; }
    public uint MsChargeup { get; set; }
    public uint ReloadTime { get; set; }
    public float MaxRise { get; set; }
    public float ReloadTimeMult { get; set; }
    public float CamRecoilBase { get; set; }
    public uint? ReloadAbility { get; set; }
    public float MinRisePerBurst { get; set; }
    public float MsChargeupMaxMult { get; set; }
    public uint MsReturn { get; set; }
    public float MinRoundsPerBurstMult { get; set; }
    public float RangeMult { get; set; }
    public float Range { get; set; }
    public float OvermaxRisePermanentFrac { get; set; }
    public float MaleScaleAdd { get; set; }
    public float MinSlidePerBurstMult { get; set; }
    public float MinRiseFrac { get; set; }
    public float RisePermanentFracMult { get; set; }
    public float InitialJitter { get; set; }
    public float JumpPerslideMult { get; set; }
    public float RoundsPerBurstMult { get; set; }
    public uint MsBurstDuration { get; set; }
    public float ClipRegenMsMult { get; set; }
    public float HeadshotMultMult { get; set; }
    public uint RiseRampTime { get; set; }
    public float DamagePerRoundMult { get; set; }
    public uint MsChargeupMin { get; set; }
    public uint ReloadPenalty { get; set; }
    public float TargetingRange { get; set; }
    public float CamRecoilShake { get; set; }
    public uint MsAgilityReturnDelay { get; set; }
    public uint ClipRegenMs { get; set; }
    public uint MsSpreadReturn { get; set; }
    public float MaxSlide { get; set; }
    public uint AiSwayVperiod { get; set; }
    public float OvermaxSlidePermanentFrac { get; set; }
    public uint CamRecoilRecoverMs { get; set; }
    public float SpreadPerBurst { get; set; }
    public float AimassistCosMult { get; set; }
    public float BaseClipSizeMult { get; set; }
    public float SlideRampExponent { get; set; }
    public uint SpreadRampTime { get; set; }
    public float StartingSpreadMult { get; set; }
    public float RunRiseRampMult { get; set; }
    public float SlidePermanentFrac { get; set; }
    public float StartingSpread { get; set; }
    public float RiseRampExponent { get; set; }
    public float FpVisualFov { get; set; }
    public float MsChargeupMinMult { get; set; }
    public uint? ClipEmptyAbility { get; set; }
    public uint MsRiseReturnDelay { get; set; }
    public float MaxSpread { get; set; }
    public float MinRisePerBurstMult { get; set; }
    public uint WeaponFlags { get; set; }
    public float AimassistCos { get; set; }
    public uint? DefaultScopeId { get; set; }
    public uint AiSwayConvergenceMs { get; set; }
    public float NoSpreadChance { get; set; }
    public float JumpPerriseMult { get; set; }
    public float SlidePermanentFracMult { get; set; }
    public float FemaleScaleAdd { get; set; }
    public uint SlideRampTime { get; set; }
    public float MsChargeupMult { get; set; }
    public float RisePermanentFrac { get; set; }
    public short BaseClipSize { get; set; }
    public ushort? DefaultAmmoId { get; set; }
    public short AnimArmedPriority { get; set; }
    public short MaxAmmo { get; set; }
    public sbyte AnimArmedId { get; set; }
    public sbyte AnimFireType { get; set; }
    public sbyte AnimReloadType { get; set; }
    public sbyte MinRoundsPerBurst { get; set; }
    public sbyte RoundsPerBurst { get; set; }
    public sbyte RoundReload { get; set; }
    public sbyte BurstbonusPerTarget { get; set; }
    public sbyte MinAmmoPerBurst { get; set; }
    public sbyte AnimChargeType { get; set; }
    public sbyte? UiReticleName { get; set; }
    public sbyte? SlotIndex { get; set; }
    public sbyte MaxTargets { get; set; }
    public sbyte FireType { get; set; }
    public sbyte AmmoPerBurst { get; set; }
}
