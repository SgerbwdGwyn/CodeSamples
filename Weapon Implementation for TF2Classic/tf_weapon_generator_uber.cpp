//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. =======//
//
// Purpose: TF Pipebomb Grenade.
//
//=============================================================================//
#include "cbase.h"
#include "tf_weapon_heallauncher.h"
#include "tf_weapon_generator_uber.h"
#include "tf_shareddefs.h"

#ifdef GAME_DLL
#include "te_effect_dispatch.h"
#include "props.h"

#include "tf_team.h"
#include "tf_obj.h"
#include "entity_healthkit.h"
#include "tf_player_shared.h"
#include "tf_ammo_pack.h"
#else
#include "c_tf_player.h"
#include "functionproxy.h"
#include <toolframework_client.h>
#include "debugoverlay_shared.h"
#endif

#define TF_WEAPON_UBERGENERATOR_MODEL		"models/items/nader_mine.mdl"

extern ConVar tf2c_sticky_touch_fix;

#ifdef GAME_DLL
ConVar tf2c_medicgl_generator_delay( "tf2c_medicgl_generator_delay", "0.0", 0, "Time in the world the Uber Generator needs to be before activating." );
ConVar tf2c_medicgl_generator_radius( "tf2c_medicgl_generator_radius", "250", 0, "The radius the Uber Generator will apply its effect in." );
ConVar tf2c_medicgl_generator_opacity( "tf2c_medicgl_generator_opacity", "255", 0, "The opacity of the Uber Generator bubble model (0-255).", true, 0, true, 255 );
extern ConVar tf_obj_gib_velocity_min;
extern ConVar tf_obj_gib_velocity_max;
extern ConVar tf_obj_gib_maxspeed;
#endif
#ifdef CLIENT_DLL
ConVar tf2c_medicgl_generator_drawradius("tf2c_medicgl_generator_drawradius", "0", FCVAR_CHEAT, " ");
#endif

IMPLEMENT_NETWORKCLASS_ALIASED( TFGeneratorUber, DT_TFProjecile_UberGenerator )

#ifdef GAME_DLL
//-----------------------------------------------------------------------------
// Purpose: SendProxy that converts the Healing list UtlVector to entindices
//-----------------------------------------------------------------------------
void SendProxy_UberList( const SendProp *pProp, const void *pStruct, const void *pData, DVariant *pOut, int iElement, int objectID )
{
	CTFGeneratorUber *pGenerator = (CTFGeneratorUber *)pStruct;

	// If this assertion fails, then SendProxyArrayLength_HealingArray must have failed.
	Assert( iElement < pGenerator->m_hUberTargets.Size() );

	EHANDLE hOther = pGenerator->m_hUberTargets[iElement].Get();
	SendProxy_EHandleToInt( pProp, pStruct, &hOther, pOut, iElement, objectID );
}

int SendProxyArrayLength_UberArray( const void *pStruct, int objectID )
{
	return ( (CTFGeneratorUber *)pStruct )->m_hUberTargets.Count();
}
#else
//-----------------------------------------------------------------------------
// Purpose: RecvProxy that converts the Team's player UtlVector to entindexes.
//-----------------------------------------------------------------------------
void RecvProxy_UberList( const CRecvProxyData *pData, void *pStruct, void *pOut )
{
	CTFGeneratorUber *pGenerator = (CTFGeneratorUber *)pStruct;

	CBaseHandle *pHandle = (CBaseHandle *)( &pGenerator->m_hUberTargets[pData->m_iElement] ); 
	RecvProxy_IntToEHandle( pData, pStruct, pHandle );

	// Update the heal beams.
	pGenerator->UpdateUberTargets();
}

void RecvProxyArrayLength_UberArray( void *pStruct, int objectID, int currentArrayLength )
{
	CTFGeneratorUber *pGenerator = (CTFGeneratorUber *)pStruct;
	if ( pGenerator->m_hUberTargets.Size() != currentArrayLength )
	{
		pGenerator->m_hUberTargets.SetSize( currentArrayLength );
	}

	// Update the uber beams.
	pGenerator->UpdateUberTargets();
}

#endif

BEGIN_NETWORK_TABLE( CTFGeneratorUber, DT_TFProjecile_UberGenerator )
#ifdef CLIENT_DLL
RecvPropTime( RECVINFO( m_flCreationTime ) ),
RecvPropBool( RECVINFO( m_bGeneratorActive ) ),
RecvPropFloat( RECVINFO( m_flEffectRadius ) ),
RecvPropVector( RECVINFO( m_vecTeamColour ) ),
RecvPropBool( RECVINFO( m_bUberTargetsParity ) ),
RecvPropArray2( 
	RecvProxyArrayLength_UberArray,
	RecvPropInt( "uber_array_element", 0, SIZEOF_IGNORE, 0, RecvProxy_UberList ), 
	MAX_PLAYERS, 
	0, 
	"uber_array"
),
#else
SendPropTime( SENDINFO( m_flCreationTime ) ),
SendPropBool( SENDINFO( m_bGeneratorActive ) ),
SendPropFloat( SENDINFO( m_flEffectRadius ) ),
SendPropVector( SENDINFO( m_vecTeamColour ) ),
SendPropBool( SENDINFO( m_bUberTargetsParity ) ),
SendPropArray2( 
	SendProxyArrayLength_UberArray,
	SendPropInt( "uber_array_element", 0, SIZEOF_IGNORE, NUM_NETWORKED_EHANDLE_BITS, SPROP_UNSIGNED, SendProxy_UberList ), 
	MAX_PLAYERS, 
	0, 
	"uber_array"
	),
#endif
END_NETWORK_TABLE()

LINK_ENTITY_TO_CLASS( tf_projectile_generator_uber, CTFGeneratorUber );
PRECACHE_REGISTER( tf_projectile_generator_uber );

CTFGeneratorUber::CTFGeneratorUber()
{
	m_hShield = NULL;
	m_bUberTargetsParity = false;
#ifdef GAME_DLL
	m_bTouched = false;
	m_flChargeLevel = 1.0f;
	m_pOwnerDied = false;
	RegisterThinkContext("UberGenActivateThink");
	RegisterThinkContext("UberGenSelfDrainThink");
	m_aGibs.Purge();
#else
	m_pAuraParticleEffect = NULL;
	m_bPulsed = false;
	m_bUpdateUberTargets = m_bOldUberTargetsParity = false;
#endif
}

CTFGeneratorUber::~CTFGeneratorUber()
{
#ifdef CLIENT_DLL
	ParticleProp()->StopEmission();
	StopSound(CTFGeneratorUber::GetActivationSound());
#endif
}


void CTFGeneratorUber::UpdateOnRemove( void )
{
#ifdef CLIENT_DLL
	if( ParticleProp() )
		ParticleProp()->StopEmission();

	StopSound(CTFGeneratorUber::GetActivationSound());
#endif

	// Tell our launcher that we were removed.
	CTFHealLauncher *pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
	if ( pLauncher )
	{
#ifdef GAME_DLL
		pLauncher->DeathNotice( this );
		pLauncher->NotifyGenerator( CTFHealLauncher::TF_UBERGENSTATE_DESTROYED );
#endif
	}

#ifdef GAME_DLL
	m_bGeneratorActive = false;

	CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );
	// Cleanup should happen regardless of there being an owner or not
	FOR_EACH_VEC( m_hUberTargets, i )
	{
		CTFPlayer *pTFPlayer = ToTFPlayer( m_hUberTargets[i] );
		if( !pTFPlayer )
			continue;

		if( pOwner )
			pTFPlayer->m_Shared.StopHealing( pOwner, HEALER_TYPE_BEAM );

		pTFPlayer->m_Shared.RecalculateChargeEffects( !pLauncher );
	}
	m_hUberTargets.Purge();
#endif

	BaseClass::UpdateOnRemove();
}


void CTFGeneratorUber::Precache( void )
{
	int iModel = PrecacheModel( TF_WEAPON_UBERGENERATOR_MODEL );
	PrecacheGibsForModel( iModel );
	PrecacheTeamParticles( "ubergenerator_trail_%s" );
	PrecacheTeamParticles( "ubergenerator_aura_%s" );
	PrecacheTeamParticles( "medicgun_beam_%s_invun" );
	PrecacheScriptSound( GetActivationSound() );
	PrecacheScriptSound( "HealGrenade.Success" );
	PrecacheScriptSound("UberNade.PowerOff");
	BaseClass::Precache();
}

#ifdef CLIENT_DLL
//=============================================================================
//
// TF Uber Generator Projectile functions (Client specific).
//


void CTFGeneratorUber::CreateTrails( void )
{
	const char *pszEffect = ConstructTeamParticle( "ubergenerator_trail_%s", GetTeamNumber() );
	ParticleProp()->Create( pszEffect, PATTACH_ABSORIGIN_FOLLOW );
}

//-----------------------------------------------------------------------------
// Purpose: 
// Input  : updateType - 
//-----------------------------------------------------------------------------
void CTFGeneratorUber::OnPreDataChanged( DataUpdateType_t updateType )
{
	BaseClass::OnPreDataChanged(updateType);

	m_bOldUberTargetsParity = m_bUberTargetsParity;
}


void CTFGeneratorUber::OnDataChanged( DataUpdateType_t updateType )
{
	BaseClass::OnDataChanged( updateType );

	if ( updateType == DATA_UPDATE_CREATED )
	{
		CreateTrails();
	}

	if ( m_bUberTargetsParity != m_bOldUberTargetsParity )
	{
		m_bUpdateUberTargets = true;
	}

	if ( m_bUpdateUberTargets )
	{
		UpdateEffects();
		m_bUpdateUberTargets = false;
	}

	if ( m_bGeneratorActive )
	{
		if ( !m_pAuraParticleEffect )
		{
			const char* pszEffect = ConstructTeamParticle( "ubergenerator_aura_%s", GetTeamNumber() );
			m_pAuraParticleEffect = ParticleProp()->Create(pszEffect, PATTACH_POINT_FOLLOW, "auraattachment");
		}
	}
#if 0
	if ( m_iOldTeamNum && m_iOldTeamNum != m_iTeamNum )
	{
		ParticleProp()->StopEmission();
		CreateTrails();
	}
#endif
}


void CTFGeneratorUber::Simulate( void )
{
	BaseClass::Simulate();

	if( m_bGeneratorActive && tf2c_medicgl_generator_drawradius.GetBool() )
		DrawRadius(m_flEffectRadius);
}

//-----------------------------------------------------------------------------
// Purpose: Don't draw if we haven't yet gone past our original spawn point
// Input  : flags - 
//-----------------------------------------------------------------------------
int CTFGeneratorUber::DrawModel( int flags )
{
	if ( gpGlobals->curtime < ( m_flCreationTime + 0.1 ) )
		return 0;

	return BaseClass::DrawModel( flags );
}
void CTFGeneratorUber::DrawRadius( float flRadius )
{
	NDebugOverlay::Sphere( GetAbsOrigin(), m_flEffectRadius * 0.8f, m_vecTeamColour.Get().x, m_vecTeamColour.Get().y, m_vecTeamColour.Get().z, false, 0.1f );
}


bool CTFGeneratorUber::ShouldShowUberEffectForPlayer( C_TFPlayer *pPlayer )
{
	// Don't give away cloaked spies.
	// FIXME: Is the latter part of this check necessary?
	if ( pPlayer->m_Shared.IsStealthed() || pPlayer->m_Shared.InCond( TF_COND_STEALTHED_BLINK ) )
		return false;
		
	// Don't show the effect for disguised spies unless they're the same color.
	if ( GetLocalPlayerTeam() >= FIRST_GAME_TEAM && pPlayer->m_Shared.InCond(TF_COND_DISGUISED) && ( !pPlayer->m_Shared.DisguiseFoolsTeam( GetTeamNumber() ) ) )
		return false;

	return true;
}


void CTFGeneratorUber::UpdateEffects( void )
{
	// Find all the targets we've stopped healing.
	int i, j, c, x;
	bool bStillUbering[MAX_PLAYERS];
	for ( i = 0, c = m_hUberTargetEffects.Count(); i < c; i++ )
	{
		bStillUbering[i] = false;

		// Are we still healing this target?
		for ( j = 0, x = m_hUberTargets.Count(); j < x; j++ )
		{
			if ( m_hUberTargets[j] && m_hUberTargets[j] == m_hUberTargetEffects[i].pTarget &&
				ShouldShowUberEffectForPlayer( ToTFPlayer( m_hUberTargets[j] ) ) )
			{
				bStillUbering[i] = true;
				break;
			}
		}
	}

	// Now remove all the dead effects.
	for ( i = m_hUberTargetEffects.Count() - 1; i >= 0; i-- )
	{
		if ( !bStillUbering[i] )
		{
			ParticleProp()->StopEmission( m_hUberTargetEffects[i].pEffect );
			m_hUberTargetEffects.Remove( i );
		}
	}

	// Now add any new targets.
	for ( i = 0, c = m_hUberTargets.Count(); i < c; i++ )
	{
		C_TFPlayer *pTarget = ToTFPlayer( m_hUberTargets[i].Get() );
		if ( !pTarget || !ShouldShowUberEffectForPlayer( pTarget ) )
			continue;

		// Loops through the healing targets, and make sure we have an effect for each of them.
		bool bHaveEffect = false;
		for ( j = 0, x = m_hUberTargetEffects.Count(); j < x; j++ )
		{
			if ( m_hUberTargetEffects[j].pTarget == pTarget )
			{
				bHaveEffect = true;
				break;
			}
		}

		if ( bHaveEffect )
			continue;

		const char *pszEffectName = ConstructTeamParticle( "medicgun_beam_%s_invun", GetTeamNumber() );
		CNewParticleEffect *pEffect = ParticleProp()->Create( pszEffectName, PATTACH_ABSORIGIN_FOLLOW );

		ParticleProp()->AddControlPoint( pEffect, 1, pTarget, PATTACH_ABSORIGIN_FOLLOW, NULL, Vector( 0, 0, 50 ) );

		int iIndex = m_hUberTargetEffects.AddToTail();
		m_hUberTargetEffects[iIndex].pTarget = pTarget;
		m_hUberTargetEffects[iIndex].pEffect = pEffect;
	}
}
#else
BEGIN_DATADESC( CTFGeneratorUber )
END_DATADESC()


CTFGeneratorUber *CTFGeneratorUber::Create( const Vector &position, const QAngle &angles,
const Vector &velocity, const AngularImpulse &angVelocity,
CBaseEntity *pOwner, CBaseEntity *pWeapon )
{
	return static_cast<CTFGeneratorUber *>(CTFBaseGrenade::Create( "tf_projectile_generator_uber",
		position, angles, velocity, angVelocity, pOwner, pWeapon ));
}


void CTFGeneratorUber::Spawn( void )
{
	// Set this to max, so effectively they do not self-implode.
	SetModel( TF_WEAPON_UBERGENERATOR_MODEL );
	SetDetonateTimerLength( FLT_MAX );
	SetTouch( NULL );

	BaseClass::Spawn();

	m_flCreationTime = gpGlobals->curtime;
	m_flMinSleepTime = 0;

	AddSolidFlags( FSOLID_TRIGGER );

	m_bGeneratorActive = false;
	m_bFizzle = false;

	m_flForceActivateTime = tf2c_medicgl_generator_delay.GetFloat();
	m_flEffectRadius = tf2c_medicgl_generator_radius.GetFloat();

	CTFHealLauncher *pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
	if (pLauncher)
	{
		pLauncher->NotifyGenerator(CTFHealLauncher::TF_UBERGENSTATE_DEPLOYED);
	}

	CTFPlayer* pOwner = ToTFPlayer(GetOwnerEntity());
	if (pOwner)
	{
		pOwner->m_pOwnedUberGenerator = this;
	}

	// Setup the think and touch functions (see CBaseEntity).
	SetContextThink( &CTFGeneratorUber::ActivateThink, gpGlobals->curtime + 0.1f, "UberGenActivateThink" );

//	EmitSound( CTFGeneratorUber::GetActivationSound() );

	//m_iEffectCondition = TF_UBERGENERATOR_COND;

	switch ( GetTeamNumber() ) {
	case TF_TEAM_RED:
		m_vecTeamColour = Vector(232, 28, 28);
		break;
	case TF_TEAM_BLUE:
		m_vecTeamColour = Vector( 28, 28, 232 );
		break;
	case TF_TEAM_YELLOW:
		m_vecTeamColour = Vector( 200, 200, 20 );
		break;
	case TF_TEAM_GREEN:
		m_vecTeamColour = Vector( 20, 200, 20 );
		break;
	default:
		m_vecTeamColour = Vector( 255, 255, 255 );
		break;
	}
}

// Should make it undeflectable.
void CTFGeneratorUber::Deflected( CBaseEntity *pDeflectedBy, Vector &vecDir )
{
}

extern ConVar tf2c_medicgl_generator_duration;

//-----------------------------------------------------------------------------
// Purpose: Used in pre-det and during "det" to process updates
//-----------------------------------------------------------------------------
void CTFGeneratorUber::ActivateThink( void )
{
	//BaseClass::DetonateThink();
	//IPhysicsObject *pPhysicsObject = VPhysicsGetObject();

	// mMmMMmmMmmMmmmmmmMmmMmmmmmm... DING
	if ( m_bGeneratorActive )
	{
		ApplyEffectInRadius();

		CTFHealLauncher* pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
		if ( pLauncher )		
		{
			if ( !m_pOwnerDied )
			{
				if ( GetOwnerEntity() && GetOwnerEntity()->IsAlive() )
				{
					// TODO:
					// if i kill generator it will kill shield because its child . 
					// if i setparent to null in shield it bugs out for a frame . 
					// therefore have this dumb hardcoded shit that i will rewrite soon . im too tired rn -azzy
					m_flChargeLevel = pLauncher->GetChargeLevel();

					if (!pLauncher->IsReleasingCharge())
					{
						Fizzle();
						return;
					}
				}
				else
				{
					m_pOwnerDied = true;
					SetContextThink(&CTFGeneratorUber::SelfDrainThink, gpGlobals->curtime + gpGlobals->frametime, "UberGenSelfDrainThink");
				}
			}
		}
		else
		{
			// Fizzle and then Activate again to let it handle the grenade breaking process
			Fizzle();
			return;
		}
	}
	// If we're on the world and we haven't activated yet
	else if ( m_bTouched )
	{
		if ( (gpGlobals->curtime - m_flCreationTime) >= m_flForceActivateTime )
		{
			Activate();
		}
	}

	SetNextThink(gpGlobals->curtime + 0.1f, "UberGenActivateThink");
}

void CTFGeneratorUber::SelfDrainThink(void)
{
	float flUberTime = tf2c_medicgl_generator_duration.GetFloat();
	float flChargeAmount = gpGlobals->frametime / flUberTime;
	m_flChargeLevel = Max(m_flChargeLevel - flChargeAmount, 0.0f);

	if (!m_flChargeLevel)
	{
		Fizzle();
		return;
	}

	SetNextThink(gpGlobals->curtime + gpGlobals->frametime, "UberGenSelfDrainThink");
}

//-----------------------------------------------------------------------------
// Look for targets and Uber em'
//-----------------------------------------------------------------------------
void CTFGeneratorUber::ApplyEffectInRadius()
{
	CUtlVector<EHANDLE> hOldUberTargets;
	hOldUberTargets.CopyArray(m_hUberTargets.Base(), m_hUberTargets.Count());

	bool bUpdateHealParticles = false;

	CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );
	if ( pOwner && (!pOwner->m_Shared.IsLoser() || pOwner->m_Shared.IsLoserStateStunned()) ) // No Ubering in postgame (unless you're VF4 staff)
	{
		float flRadius2 = Square( GetEffectRadius() );
		Vector vecSegment;
		Vector vecTargetPoint;
		float flDist2 = 0;

		CBaseEntity *pEntity = NULL;
		for ( CEntitySphereQuery sphere( GetAbsOrigin(), m_flRadius, FL_CLIENT | FL_FAKECLIENT ); (pEntity = sphere.GetCurrentEntity()) != NULL; sphere.NextEntity() )
		{
			CTFPlayer *pPlayer = ToTFPlayer( pEntity );
			if ( !pPlayer )
				continue;

			if ( !pPlayer->IsAlive() )
				continue;

			if ( pPlayer->GetFlags() & FL_NOTARGET )
				continue;

			if ( pPlayer->m_Shared.IsStealthed() )	// Don't out any invisible spies, that's rude!
				continue;

			// Enemies aren't allowed to be Ubered, but they can be if they're a disguised spy with a disguise that works on us.
			if ( pPlayer->IsEnemy( pOwner ) && !(pPlayer->m_Shared.IsDisguised() &&
				pPlayer->m_Shared.DisguiseFoolsTeam( pOwner->GetTeamNumber() )) )
			{
				continue;
			}

			vecTargetPoint = pPlayer->GetAbsOrigin();
			vecTargetPoint += pPlayer->GetViewOffset();
			VectorSubtract( vecTargetPoint, GetAbsOrigin(), vecSegment );
			flDist2 = vecSegment.LengthSqr();
			if ( flDist2 <= flRadius2 )
			{
				//trace_t	tr;
				//UTIL_TraceLine( GetAbsOrigin(), vecTargetPoint, MASK_SHOT_HULL, this, COLLISION_GROUP_DEBRIS, &tr );
				//if ( tr.fraction >= 1.0f )
				//{
					hOldUberTargets.FindAndRemove(pPlayer);
					if( !m_hUberTargets.HasElement(pPlayer) )
					{ 
						bUpdateHealParticles = true;
						m_hUberTargets.AddToTail(pPlayer);
						ApplyEffectToPlayer(pPlayer);
					}
				//}
			}
		}
	}

	// Cleanup should happen regardless of there being an owner or not
	FOR_EACH_VEC( hOldUberTargets, i )
	{
		CTFPlayer *pTFPlayer = ToTFPlayer( hOldUberTargets[i] );
		if ( !pTFPlayer ) {
			DevWarning("Uber generator cleanup failed! Uber target was unable to be cast to TFPlayer.\n");
			continue;
		}

		bUpdateHealParticles = true;

		m_hUberTargets.FindAndRemove(pTFPlayer);

		if ( pOwner ) {
			pTFPlayer->m_Shared.StopHealing( pOwner, HEALER_TYPE_BEAM );
			//ITFHealingWeapon *pMedigun = pOwner->GetMedigun();
			//if ( pMedigun )
			//	pTFPlayer->m_Shared.RemoveCond( g_MedigunEffects[pMedigun->GetChargeType()].condition_enable );
		}
		else {
			DevWarning( "Uber generator cleanup failed! Generator owner was null.\n" );
		}

		pTFPlayer->m_Shared.RecalculateChargeEffects( false );
	}


	if( bUpdateHealParticles )
		UpdateUberTargets();

	if ( /* m_hUberTargets && */ m_hUberTargets.Size() >= 5) {
		CTFPlayer* pOwner = ToTFPlayer(GetOwnerEntity());
		IGameEvent* event = gameeventmanager->CreateEvent("player_ubermany");
		if (event && pOwner)
		{
			event->SetInt("userid", pOwner->GetUserID());
			gameeventmanager->FireEvent(event);
		}
	}
}

#define TF2C_HEALLAUNCHER_UBER_HEALING	24.0

//-----------------------------------------------------------------------------
// Purpose: Applies the specified effect (TF_COND) to a given player
//-----------------------------------------------------------------------------
void CTFGeneratorUber::ApplyEffectToPlayer( CTFPlayer *pPlayer )
{
	if ( !pPlayer )
		return;

	CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );
	if( !pOwner )
		return;

	// Just need to set the healing target and Recalculate our Charge Conds
	// if you want 0 healing, set this to 0, don't remove it
	pPlayer->m_Shared.Heal( pOwner, TF2C_HEALLAUNCHER_UBER_HEALING );

	//ITFHealingWeapon *pMedigun = pOwner->GetMedigun();
	//if ( pMedigun && pMedigun->IsReleasingCharge() ) {
	//	pPlayer->m_Shared.AddCond( g_MedigunEffects[pMedigun->GetChargeType()].condition_enable );
	//}
	//else {
	//	pPlayer->m_Shared.RemoveCond( g_MedigunEffects[pMedigun->GetChargeType()].condition_enable );
	//}
}


float CTFGeneratorUber::GetEffectRadius( void )
{
	return m_flEffectRadius;
}


int CTFGeneratorUber::UpdateTransmitState( void )
{
	return SetTransmitState( FL_EDICT_FULLCHECK );
}


int CTFGeneratorUber::ShouldTransmit( CCheckTransmitInfo *pInfo )
{
	// Always transmit to all players

	return FL_EDICT_ALWAYS;
}

void CTFGeneratorUber::SetModel( const char *pModel )
{
	BaseClass::SetModel( pModel );

	// Clear out the gib list and create a new one.
	m_aGibs.Purge();
	BuildGibList( m_aGibs, GetModelIndex(), 1.0f, COLLISION_GROUP_NONE );
}

void CTFGeneratorUber::CreateGeneratorGibs()
{
	if ( m_aGibs.Count() <= 0 )
		return;

	int nMetalPerGib = 15;
	float flPackRatio = 0.05f;

	for ( int i = 0, c = m_aGibs.Count(); i < c; i++ )
	{
		CTFAmmoPack *pAmmoPack = CTFAmmoPack::Create( GetAbsOrigin(), GetAbsAngles(), this, m_aGibs[i].modelName, flPackRatio );
		Assert( pAmmoPack );
		if ( pAmmoPack )
		{
			pAmmoPack->ActivateWhenAtRest();

			// Fill up the ammo pack.
			pAmmoPack->GiveAmmo( nMetalPerGib, TF_AMMO_METAL );

			// Calculate the initial impulse on the weapon.
			Vector vecImpulse( random->RandomFloat( -0.5f, 0.5f ), random->RandomFloat( -0.5f, 0.5f ), random->RandomFloat( 0.75f, 1.25f ) );
			VectorNormalize( vecImpulse );
			vecImpulse *= random->RandomFloat( tf_obj_gib_velocity_min.GetFloat(), tf_obj_gib_velocity_max.GetFloat() );

			QAngle angImpulse( random->RandomFloat( -100, -500 ), 0, 0 );

			// Cap the impulse.
			float flSpeed = vecImpulse.Length();
			if ( flSpeed > tf_obj_gib_maxspeed.GetFloat() )
			{
				VectorScale( vecImpulse, tf_obj_gib_maxspeed.GetFloat() / flSpeed, vecImpulse );
			}

			if ( pAmmoPack->VPhysicsGetObject() )
			{
				// We can probably remove this when the mass on the weapons is correct!
				//pAmmoPack->VPhysicsGetObject()->SetMass( 25.0f );
				AngularImpulse angImpulse( 0, random->RandomFloat( 0, 100 ), 0 );
				pAmmoPack->VPhysicsGetObject()->SetVelocityInstantaneous( &vecImpulse, &angImpulse );
			}

			pAmmoPack->m_nSkin = GetTeamSkin( GetTeamNumber() );
		}
	}
}

void CTFGeneratorUber::Activate()
{
	// If we're detonating stickies then we're currently inside prediction
	// so we gotta make sure all effects show up.
	CDisablePredictionFiltering disabler;

	// Should get to this part if the owner player dies.
	if ( m_bFizzle )
	{
		CreateGeneratorGibs();

		m_bGeneratorActive = false;

		CTFHealLauncher *pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
		if( pLauncher )
			pLauncher->NotifyGenerator( CTFHealLauncher::TF_UBERGENSTATE_DESTROYED );

		// Remove the Uber targets so they stop receiving the effects
		// Cleanup should happen regardless of there being an owner or not
		CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );
		FOR_EACH_VEC( m_hUberTargets, i )
		{
			CTFPlayer *pTFPlayer = ToTFPlayer( m_hUberTargets[i] );
			if ( !pTFPlayer )
				continue;

			m_hUberTargets.FindAndRemove( pTFPlayer );

			if ( pOwner )
				pTFPlayer->m_Shared.StopHealing( pOwner, HEALER_TYPE_BEAM );

			pTFPlayer->m_Shared.RecalculateChargeEffects( false );
		}

		if (m_hShield->Get())
		{
			CTFGeneratorUberShield* pShield = dynamic_cast<CTFGeneratorUberShield*> (m_hShield.Get().Get());
			if (pShield)
				pShield->Kill();
		}

		EmitSound("UberNade.PowerOff");

		UpdateUberTargets();

		RemoveGrenade();
		return;
	}
	else
	{
		m_bGeneratorActive = true;
		m_flActivationTime = gpGlobals->curtime;
		CTFHealLauncher *pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
		if (pLauncher)
		{
			pLauncher->NotifyGenerator(CTFHealLauncher::TF_UBERGENSTATE_ACTIVATED);
		}
		EmitSound( CTFGeneratorUber::GetActivationSound() );

		m_hShield = CBaseEntity::Create("tf_weapon_generator_uber_shield", GetAbsOrigin(), vec3_angle, this);
		m_hShield.Get()->SetParent(this);
		SetSequence( LookupSequence("open") );
	}

	//BaseClass::Detonate();
}


void CTFGeneratorUber::Fizzle( void )
{
	if (m_flActivationTime && ((gpGlobals->curtime - m_flActivationTime) >= 12.0f)) {
		CTFPlayer* pOwner = ToTFPlayer(GetOwnerEntity());		
		IGameEvent* event = gameeventmanager->CreateEvent("player_chargeextended");
		if (event && pOwner)
		{
			event->SetInt("userid", pOwner->GetUserID());
			gameeventmanager->FireEvent(event);
		}
	}
	m_bFizzle = true;
	Activate(); // Goto the grenade breaking logic
}


void CTFGeneratorUber::VPhysicsCollision( int index, gamevcollisionevent_t *pEvent )
{
	BaseClass::VPhysicsCollision( index, pEvent );

	int otherIndex = !index;
	CBaseEntity *pHitEntity = pEvent->pEntities[otherIndex];
	if ( !pHitEntity )
		return;

	if ( PropDynamic_CollidesWithGrenades( pHitEntity ) )
		return;

	// Handle hitting skybox (disappear).
	surfacedata_t *pprops = physprops->GetSurfaceData( pEvent->surfaceProps[otherIndex] );
	if ( pprops->game.material == 'X' )
	{
		// uncomment to destroy grenade upon hitting sky brush
		//SetThink( &CTFGrenadePipebombProjectile::SUB_Remove );
		//SetNextThink( gpGlobals->curtime );
		return;
	}

	// Adding these because maps can use them as essentially static geometry, so we'll bounce off if they move.
	// Beats having to manually check for the model name of sawblades...
	bool bIsDynamicProp = (dynamic_cast<CDynamicProp *>(pHitEntity) != NULL);
	bool bIsFuncBrush = false;
	bool bIsBreakable = false;
	bool bIsDoor = false;

	if ( tf2c_sticky_touch_fix.GetInt() > 0 )
	{
		bIsFuncBrush = (dynamic_cast<CFuncBrush *>(pHitEntity) != NULL);
		bIsBreakable = (FClassnameIs( pHitEntity, "func_breakable" ));
		if ( tf2c_sticky_touch_fix.GetInt() > 1 )
		{
			bIsDoor = (dynamic_cast<CBaseDoor *>(pHitEntity) != NULL);
		}
	}

	// func_rotating doesn't let us access its speed and doesn't give angular velocity for children so just bounce off...
	CBaseEntity* pParent = pHitEntity->GetRootMoveParent();
	if ( pParent && FClassnameIs( pParent, "func_rotating" ) )
		return;

	// Generators stick to the world when they touch it.
	if ( !pHitEntity->IsMoving() && (pHitEntity->IsWorld() || bIsDynamicProp || bIsFuncBrush || bIsBreakable || bIsDoor) && gpGlobals->curtime > m_flMinSleepTime )
	{
		// Save impact data for explosions.
		m_bUseImpactNormal = true;
		pEvent->pInternalData->GetSurfaceNormal( m_vecImpactNormal );
		m_vecImpactNormal.Negate();

		// Only stick to floors.
		// 1.0 facing straight up i.e. floor, 0.0 sideways, -1.0 ceilings.
		// 1 - DOT_30DEGREE makes it unable to stick to inclines steeper than 60 degrees.
		float flDot = DotProduct( m_vecImpactNormal, Vector( 0, 0, 1 ) );

		if ( flDot < ( 1.0f - DOT_30DEGREE ) )
		{
			// Lower bounce force akin to Iron Bomber (grenade_no_bounce attribute).
			// Keeps it from being too chaotic.
			Vector vel;
			AngularImpulse angImp;
			VPhysicsGetObject()->GetVelocity( &vel, &angImp );
			vel *= 0.5;
			angImp *= 0.5;
			VPhysicsGetObject()->SetVelocity( &vel, &angImp );

			return;
		}

		m_bTouched = true;
		IPhysicsObject *pPhysicsObject = VPhysicsGetObject();
		if ( pPhysicsObject )
		{
			pPhysicsObject->EnableMotion( false );
		}

		SetGroundEntity( pHitEntity );

		// Align to hit surfaces.
		QAngle vAngles;
		VectorAngles( m_vecImpactNormal, vAngles );
		vAngles.x += 90.0f; // Compensate for mesh angle.

		Vector vecPos;
		pEvent->pInternalData->GetContactPoint( vecPos );

		// Fix cases of bad placement angle on edges or corners.
		trace_t	tr;
		UTIL_TraceLine( GetAbsOrigin(), vecPos, MASK_SHOT_HULL, this, COLLISION_GROUP_DEBRIS, &tr );

		if ( tr.fraction < 1.0f )
		{
			// If we are inside an object, we also point in the opposite direction for some reason.
			// Do this easy fix instead of doing vector math with behind the scenes vphysics.
			vAngles.x += 180.0f;
			vecPos = vecPos + (m_vecImpactNormal * -2); // Pull out in opposite direction.
		}
		else
		{
			vecPos = vecPos + (m_vecImpactNormal * 2); // Pull out as normal.
		}

		if ( pPhysicsObject )
		{
			pPhysicsObject->SetPosition( vecPos, vAngles, true );
		}

		// Generous bbox for bullet hit detection (collision mesh does precise check)
		UTIL_SetSize( this, Vector( -8.0f, -8.0f, -8.0f ), Vector( 8.0f, 8.0f, 8.0f ) );

		CTFHealLauncher *pLauncher = dynamic_cast<CTFHealLauncher*>(m_hLauncher.Get());
		if (pLauncher)
		{
			pLauncher->NotifyGenerator(CTFHealLauncher::TF_UBERGENSTATE_STUCK);
		}
	}
}
#endif


IMPLEMENT_NETWORKCLASS_ALIASED(TFGeneratorUberShield, DT_TFGeneratorUberShield)

BEGIN_NETWORK_TABLE(CTFGeneratorUberShield, DT_TFGeneratorUberShield)
END_NETWORK_TABLE()

LINK_ENTITY_TO_CLASS(tf_weapon_generator_uber_shield, CTFGeneratorUberShield);
#ifdef GAME_DLL

BEGIN_DATADESC(CTFGeneratorUberShield)
END_DATADESC()

#define	UBER_SHIELD_MODEL "models/items/shield_bubble/shield_bubble2.mdl" // "models/generator/forcefield/forcefield.mdl"

void CTFGeneratorUberShield::Precache(void)
{
	PrecacheModel(UBER_SHIELD_MODEL);

	BaseClass::Precache();
}

void CTFGeneratorUberShield::Spawn(void)
{
	m_flModelScale = 0.0f;
	Precache();

	if (GetOwnerEntity())
		m_nSkin = GetTeamSkin(GetOwnerEntity()->GetTeamNumber());
	SetModel(UBER_SHIELD_MODEL);
	SetSolid(SOLID_NONE);
	AddEffects(EF_NOSHADOW);
	SetModelScale(1.0f, 0.5f); // was 1.2
	SetRenderMode( kRenderTransAlpha );
	SetRenderColorA( tf2c_medicgl_generator_opacity.GetInt() );
	m_bEnabled = true;
}

void CTFGeneratorUberShield::Fizzle(void)
{
	// TODO: Grow and fade out?
	SetModelScale(1.2f, 0.35f);
	SetRenderColorA( tf2c_medicgl_generator_opacity.GetInt() * 0.5 );
}

void CTFGeneratorUberShield::Kill(void)
{
	m_bEnabled = false;
	AddEffects(EF_NODRAW);
	UTIL_Remove(this); // m_hShield is EHANDLE so dont worry
}

int CTFGeneratorUberShield::ShouldTransmit( CCheckTransmitInfo *pInfo )
{
	// Always transmit to all players
	return FL_EDICT_ALWAYS;
}

int CTFGeneratorUberShield::UpdateTransmitState( void )
{
	return SetTransmitState( FL_EDICT_FULLCHECK );
}

#endif