//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. =======//
//
// Purpose: TF Healnade.
//
//=============================================================================//
#include "cbase.h"
#include "tf_weapon_grenade_healimpact.h"
#include "tf_weapon_heallauncher.h"

#ifdef GAME_DLL
#include "soundent.h"
#include "te_effect_dispatch.h"
#include "tf_fx.h"
#include "tf_gamerules.h"
#include "props.h"
#else
#include <effect_dispatch_data.h>
#endif

#ifdef GAME_DLL
ConVar tf2c_medicgl_self_heal("tf2c_medicgl_self_heal", "0", 0, "Enables self healing via the healing grenades.\n");
//ConVar tf2c_medicgl_radius("tf2c_medicgl_radius", "100", 0, "Healing radius of the healing grenades\n");
#endif
extern ConVar tf_grenade_show_radius;

IMPLEMENT_NETWORKCLASS_ALIASED( TFGrenadeHealImpactProjectile, DT_TFProjectile_HealImpact )

BEGIN_NETWORK_TABLE( CTFGrenadeHealImpactProjectile, DT_TFProjectile_HealImpact )
END_NETWORK_TABLE()

LINK_ENTITY_TO_CLASS(tf_projectile_grenade_healimpact, CTFGrenadeHealImpactProjectile);
PRECACHE_REGISTER(tf_projectile_grenade_healimpact);

#ifdef GAME_DLL
static string_t s_iszTrainName;
#endif

//-----------------------------------------------------------------------------
// Purpose: 
// Input  :  - 
//-----------------------------------------------------------------------------
CTFGrenadeHealImpactProjectile::CTFGrenadeHealImpactProjectile()
{
#ifdef GAME_DLL
	m_iTouched = 0;
	s_iszTrainName = AllocPooledString( "models/props_vehicles/train_enginecar.mdl" );
#endif
}

//-----------------------------------------------------------------------------
// Purpose: 
// Input  :  - 
//-----------------------------------------------------------------------------
CTFGrenadeHealImpactProjectile::~CTFGrenadeHealImpactProjectile()
{
#ifdef CLIENT_DLL
	ParticleProp()->StopEmission();
#endif
}

#define TF_WEAPON_HEALGRENADE_MODEL		"models/weapons/w_models/w_grenade_nader.mdl"
#define TF_WEAPON_HEALGRENADE_BOUNCE_SOUND	"HealGrenade.Bounce"
#define TF_WEAPON_GRENADE_DETONATE_TIME 2.0f

void CTFGrenadeHealImpactProjectile::Precache()
{
	PrecacheModel( TF_WEAPON_HEALGRENADE_MODEL );

	PrecacheTeamParticles( "healgrenade_trail_%s" );
	PrecacheTeamParticles( "healgrenade_trail_%s_crit" );

	PrecacheScriptSound( TF_WEAPON_HEALGRENADE_BOUNCE_SOUND );

	BaseClass::Precache();
}

#ifdef CLIENT_DLL

void CTFGrenadeHealImpactProjectile::CreateTrails(void)
{
	const char* pszEffect = GetProjectileParticleName("healgrenade_trail_%s", m_hLauncher);
	m_hTimerParticle = ParticleProp()->Create(pszEffect, PATTACH_ABSORIGIN_FOLLOW);

	if (m_bCritical)
	{
		const char* pszEffectName = GetProjectileParticleName("healgrenade_trail_%s_crit", m_hLauncher, m_bCritical);
		ParticleProp()->Create(pszEffectName, PATTACH_ABSORIGIN_FOLLOW);
	}
}

void CTFGrenadeHealImpactProjectile::Simulate( void )
{
	BaseClass::BaseClass::Simulate();

	float flTimer = (m_flDetonateTime - SpawnTime()) ? (m_flDetonateTime - SpawnTime()) : 1.0f;

	if( m_hTimerParticle )
	{
		m_hTimerParticle->SetControlPoint(RADIUS_CP1, Vector(1.0f - ((m_flDetonateTime - gpGlobals->curtime) / flTimer), 0, 0));
	}
}



//=============================================================================
//
// TF Heal Grenade Projectile functions (Client specific).
//

/*

void CTFGrenadeHealImpactProjectile::CreateParticles( void )
{
	const char *pszEffect = ConstructTeamParticle( "medic_explosion_PARENT_%s", GetTeamNumber() );
	
	//pWeaponInfo->m_szExplosionEffects[]
	CTFWeaponBase *pLauncher = dynamic_cast<CTFWeaponBase*>(GetOriginalLauncher());
	if ( pLauncher )
	{
		for ( int i = 0; i < TF_EXPLOSION_COUNT; i++ )
		{
			V_strcpy_safe( GetTFWeaponInfo( pLauncher->GetWeaponID() )->m_szExplosionEffects[i], pszEffect );
		}

	}
}


void CTFGrenadeHealImpactProjectile::OnDataChanged( DataUpdateType_t updateType )
{
	BaseClass::OnDataChanged( updateType );

	if ( updateType == DATA_UPDATE_CREATED )
	{
		m_flCreationTime = gpGlobals->curtime;

		CreateParticles();
	}
	else if ( m_hOldOwner.Get() != GetOwnerEntity() )
	{
		ParticleProp()->StopEmission();
		CreateParticles();
	}
}

//-----------------------------------------------------------------------------
// Purpose: Don't draw if we haven't yet gone past our original spawn point
// Input  : flags - 
//-----------------------------------------------------------------------------
int CTFGrenadeHealImpactProjectile::DrawModel( int flags )
{
	if ( gpGlobals->curtime < ( m_flCreationTime + 0.1 ) )
		return 0;

	return BaseClass::DrawModel( flags );
}
*/
#else

//=============================================================================
//
// TF Healnade Projectile functions (Server specific).
//

BEGIN_DATADESC( CTFGrenadeHealImpactProjectile )
	DEFINE_ENTITYFUNC( HealnadeTouch ),
END_DATADESC()


CTFGrenadeHealImpactProjectile *CTFGrenadeHealImpactProjectile::Create( const Vector &position, const QAngle &angles,
	const Vector &velocity, const AngularImpulse &angVelocity,
	CBaseEntity *pOwner, CBaseEntity *pWeapon, int iType )
{
	return static_cast<CTFGrenadeHealImpactProjectile *>( CTFBaseGrenade::Create( "tf_projectile_grenade_healimpact",
		position, angles, velocity, angVelocity, pOwner, pWeapon, iType ) );
}


void CTFGrenadeHealImpactProjectile::Spawn()
{
	BaseClass::Spawn();

	SetModel( TF_WEAPON_HEALGRENADE_MODEL );
	SetDetonateTimerLength( TF_WEAPON_GRENADE_DETONATE_TIME );
	SetTouch( &CTFGrenadeHealImpactProjectile::HealnadeTouch );
	
	// We want to get touch functions called so we can damage enemy players
	//AddSolidFlags( FSOLID_TRIGGER );
}

void CTFGrenadeHealImpactProjectile::Detonate()
{
	if ( ShouldNotDetonate() )
	{
		RemoveGrenade();
		return;
	}
	// Putting this here just in case we're inside prediction to make sure all effects show up.
	CDisablePredictionFiltering disabler;

	trace_t		tr;
	Vector		vecSpot;// trace starts here!

	SetThink(NULL);

	vecSpot = GetAbsOrigin() + Vector(0, 0, 8);
	UTIL_TraceLine(vecSpot, vecSpot + Vector(0, 0, -32), MASK_SHOT_HULL, this, COLLISION_GROUP_NONE, &tr);

	Explode(&tr, GetDamageType(), true);

	//if (GetShakeAmplitude())
	//{
	//	UTIL_ScreenShake(GetAbsOrigin(), GetShakeAmplitude(), 150.0, 1.0, GetShakeRadius(), SHAKE_START);
	//}
}


void CTFGrenadeHealImpactProjectile::HealnadeTouch( CBaseEntity *pOther )
{
	// Verify a correct "other".
	// Hack as shit to check the classname but if mappers are going to mark the entire gullywash ramp as solid there's
	// not much I can do. Grenades fire their touch functions every frame they're inside said ramps.
	if ( !pOther->IsSolid() || pOther->IsSolidFlagSet( FSOLID_VOLUME_CONTENTS ) )
		return;

	// Handle hitting skybox (disappear).
	trace_t pTrace;
	Vector velDir = GetAbsVelocity();
	VectorNormalize( velDir );
	Vector vecSpot = GetAbsOrigin() - velDir * 32;
	UTIL_TraceLine( vecSpot, vecSpot + velDir * 64, MASK_SOLID, this, COLLISION_GROUP_NONE, &pTrace );
	if ( pTrace.fraction < 1.0f && pTrace.surface.flags & SURF_SKY )
	{
		UTIL_Remove( this );
		return;
	}

	// Heal-splode if we can hit this entity.
	if ( ShouldExplodeOnEntity( pOther ) )
	{
		// If we've directly hit an ally, store them and skip them when applying the radial healing.
		// If we've directly hit an enemy, store them for the 100% damage application (direct hit).
		m_hEnemy = pOther;

		// We should only explode with damage if we've hit an enemy or the world. No damage when hitting teammates directly.
		bool bDealDamage = true;
		if ( GetOwnerEntity() )
			bDealDamage = pOther->GetTeamNumber() != GetOwnerEntity()->GetTeamNumber();
		
		m_bDirectHitWasTeammate = !IsEnemy( pOther );

		// Only apply damage if we're timing out or hitting an enemy directly.
		Explode( &pTrace, GetDamageType(), bDealDamage );
	}
}


void CTFGrenadeHealImpactProjectile::Explode( trace_t *pTrace, int bitsDamageType, bool bDealDamage )
{
	SetModelName( NULL_STRING );
	//AddSolidFlags( FSOLID_NOT_SOLID );
	m_takedamage = DAMAGE_NO;

	// Figure out Econ ID.
	int iItemID = -1;
	if ( GetOriginalLauncher() )
	{
		CTFWeaponBase *pWeapon = dynamic_cast<CTFWeaponBase *>( GetOriginalLauncher() );
		if (pWeapon)
		{
			iItemID = pWeapon->GetItemID();
		}
	}

	// Pull out of the wall a bit.
	if (pTrace->fraction != 1.0)
	{
		SetAbsOrigin(pTrace->endpos + (pTrace->plane.normal * 1.0f));
	}

	CSoundEnt::InsertSound(SOUND_COMBAT, GetAbsOrigin(), BASEGRENADE_EXPLOSION_VOLUME, 3.0);

	// Explosion effect on client.
	Vector vecOrigin = GetAbsOrigin();
	CPVSFilter filter(vecOrigin);
	CTFPlayer *pTFAttacker = ToTFPlayer(GetOwnerEntity());
	int iEntIndex = (pTrace->m_pEnt && pTrace->m_pEnt->IsPlayer()) ? pTrace->m_pEnt->entindex() : -1;
	const Vector &vecNormal = UseImpactNormal() ? GetImpactNormal() : pTrace->plane.normal;

	TE_TFExplosion(filter, 0.0f, vecOrigin, vecNormal, GetWeaponID(), iEntIndex, pTFAttacker, GetTeamNumber(), m_bCritical, iItemID);

	// Use the thrower's position as the reported position
	Vector vecReported = GetOwnerEntity() ? GetOwnerEntity()->GetAbsOrigin() : vec3_origin;

	float flRadius = 146.0f;
	CTFWeaponBaseGun *pLauncherTF = dynamic_cast<CTFWeaponBaseGun*>(m_hLauncher.Get());
	if ( pLauncherTF )
		flRadius = pLauncherTF->GetTFWpnData().m_flDamageRadius;

	/*flRadius  = */ CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flRadius, mult_explosion_radius );

	if (tf_grenade_show_radius.GetBool())
	{
		DrawRadius(flRadius);
	}

	//CTFPlayer* pOwner = ToTFPlayer();
	
	float flHealAmountRadial = 0;
	float flHealAmountDirect = 0;
	float flOverhealMult = 1.0f;
	float flResidualHealRate = 0;
	float flResidualHealDuration = 0;
	float flDamageForceOverride = 0;
	int ibIgnoreFalloff = 0;
	int iHealingAdjustStacking = 0;
	int iHealingAdjustResidual = 0;

	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flHealAmountDirect, apply_heal_explosion );						// Set the heal amount
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( GetOwnerEntity(), flHealAmountDirect, mult_medigun_healrate );
	flHealAmountRadial = flHealAmountDirect;
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flHealAmountRadial, apply_heal_explosion_reduced_indirect );		// Multiply the heal amount by the indirect penalty
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( GetOwnerEntity(), flOverhealMult, overheal_fill_rate );			// Multiply the overheal rate
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flResidualHealRate, residual_heal_rate );
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flResidualHealDuration, residual_heal_duration );
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( m_hLauncher.Get(), flDamageForceOverride, set_damage_knockback );
	CALL_ATTRIB_HOOK_INT_ON_OTHER( m_hLauncher.Get(), ibIgnoreFalloff, disable_explosion_falloff );
	CALL_ATTRIB_HOOK_INT_ON_OTHER( m_hLauncher.Get(), iHealingAdjustStacking, apply_heal_explosion_stacking_adjust );
	CALL_ATTRIB_HOOK_INT_ON_OTHER( m_hLauncher.Get(), iHealingAdjustResidual, apply_heal_explosion_residual_adjust );

	CTFRadiusHealingInfo radiusHealInfo;
	radiusHealInfo.m_flHealingAmountRadial = flHealAmountRadial;
	radiusHealInfo.m_flHealingAmountDirect = flHealAmountDirect;
	radiusHealInfo.m_flOverhealMultiplier = flOverhealMult;
	radiusHealInfo.m_hPlayerResponsible = GetOwnerEntity();
	radiusHealInfo.m_vecSrc = vecOrigin;
	radiusHealInfo.m_flRadius = flRadius;
	radiusHealInfo.m_flSelfHealRadius = GetSelfDamageRadius();
	//radiusHealInfo.m_condConditionsToApply = TF_COND_HEALTH_BUFF;//TF_COND_HEALINGGAS;
	//radiusHealInfo.m_flConditionDuration = tf2c_medicgl_heal_duration.GetFloat();
	radiusHealInfo.m_bUseFalloffCalcs = !ibIgnoreFalloff;
	radiusHealInfo.m_bSelfHeal = tf2c_medicgl_self_heal.GetBool();
	radiusHealInfo.m_hEntityDirectlyHit = m_hEnemy;
	
	radiusHealInfo.m_bApplyResidualHeal = !!flResidualHealRate;
	radiusHealInfo.m_flResidualHealRate = flResidualHealRate;
	radiusHealInfo.m_flResidualHealDuration = flResidualHealDuration;
	radiusHealInfo.m_flHealingAmountStackingAdjust = iHealingAdjustStacking;
	radiusHealInfo.m_flHealingAmountStackingAdjustResidual = iHealingAdjustResidual;

	TFGameRules()->RadiusHeal(radiusHealInfo, 3, true);

	if (bDealDamage)
	{
		//BaseClass::Explode(pTrace, bitsDamageType);
		float flRadius = GetDamageRadius();
		if (tf_grenade_show_radius.GetBool())
		{
			DrawRadius(flRadius);
		}

		CTFPlayer* pOwner = ToTFPlayer(GetOwnerEntity());

		CTFRadiusDamageInfo radiusInfo;
		radiusInfo.info.Set(this, pOwner, GetOriginalLauncher(), GetBlastForce(), GetAbsOrigin(), GetDamage(), bitsDamageType, 0, &vecReported);
		radiusInfo.info.SetDamageForForceCalc( flDamageForceOverride );
		radiusInfo.info.SetDamageForForceCalcOverriden( true );
		radiusInfo.m_vecSrc = vecOrigin;
		radiusInfo.m_flRadius = flRadius;
		radiusInfo.m_flSelfDamageRadius = GetSelfDamageRadius();
		radiusInfo.m_bStockSelfDamage = UseStockSelfDamage();

		TFGameRules()->RadiusDamage(radiusInfo);

		// Don't decal players with scorch.
		if (pTrace->m_pEnt && !pTrace->m_pEnt->IsPlayer())
		{
			// TODO: Custom heal grenade impact decal?
			// UTIL_DecalTrace(pTrace, "Scorch");
		}

		RemoveGrenade(false);
	}
	RemoveGrenade(false);
}

// Turns out this one is useless since it just forcibly uses DamageForForceCalc anyway.... why does Blast Force exist then?
/*
Vector CTFGrenadeHealImpactProjectile::GetBlastForce() {
	return Vector(0, 0, tf2c_medicgl_dmgforce.GetFloat());
}
*/

void CTFGrenadeHealImpactProjectile::VPhysicsCollision( int index, gamevcollisionevent_t *pEvent )
{
	//BaseClass::VPhysicsCollision( index, pEvent );

	int otherIndex = !index;
	CBaseEntity *pHitEntity = pEvent->pEntities[otherIndex];
	if ( !pHitEntity )
		return;

	if ( PropDynamic_CollidesWithGrenades( pHitEntity ) )
	{
		HealnadeTouch( pHitEntity );
	}

	if (!IsMarkedForDeletion()) // not yet sure if this works with base entities. In case PipebombTouch called for UTIL_Remove
	{
		CTFWeaponBase* pWeapon = static_cast<CTFWeaponBase*>(GetOriginalLauncher());
		int iDetonateMode = 0;
		CALL_ATTRIB_HOOK_INT_ON_OTHER(pWeapon, iDetonateMode, set_detonate_mode);
		if (iDetonateMode == TF_DETMODE_FIZZLEONWORLD)
		{
			RemoveGrenade();
			return;
		}
		else if ( iDetonateMode == TF_DETMODE_EXPLODEONWORLD )
		{
			// Save impact data for explosions.
			m_bUseImpactNormal = true;
			pEvent->pInternalData->GetSurfaceNormal(m_vecImpactNormal);
			m_vecImpactNormal.Negate();

			//HealnadeTouch( pHitEntity );
			Detonate();
		}
	}

	// Increment the touch counter.
	m_iTouched++;
}


bool CTFGrenadeHealImpactProjectile::ShouldExplodeOnEntity( CBaseEntity *pOther )
{
	// Train hack!
	if ( pOther->GetModelName() == s_iszTrainName && pOther->GetAbsVelocity().LengthSqr() > 1.0f )
		return true;

	if ( PropDynamic_CollidesWithGrenades( pOther ) )
		return true;

	if ( pOther->m_takedamage == DAMAGE_NO )
		return false;

	// Only explode on players. World explosions will be handled elsewhere
	CTFPlayer *pHitPlayer = ToTFPlayer( pOther );
	if ( !pHitPlayer )
	{
		return false;
	}
	else
	{
		if ( pHitPlayer->GetHealth() > pHitPlayer->m_Shared.GetMaxBuffedHealth() * 0.95f )
		{
			return false;
		}
	}

	// Don't hit ourselves.
	return pOther != GetOwnerEntity();
}

#endif // game_dll
