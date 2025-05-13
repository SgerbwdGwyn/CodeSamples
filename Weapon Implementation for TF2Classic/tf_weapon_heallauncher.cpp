//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. =======
//
// Purpose:
//
//=============================================================================

#include "cbase.h"
#include "tf_weapon_heallauncher.h" 
#include "tf_weapon_medigun.h"
#include "tf_gamerules.h"
#ifdef GAME_DLL
#include "tf_player.h"
#include "tf_gamestats.h"
#else
#include "c_tf_player.h"
#endif

#if defined( CLIENT_DLL )
#include <vgui_controls/Panel.h>
#include <vgui/ISurface.h>
#include "particles_simple.h"
#include "c_tf_player.h"
#include "soundenvelope.h"
#include "tf_hud_mediccallers.h"
#include <prediction.h>
#endif


//ConVar tf2c_medicgl_uber_gain_while_generator_out( "tf2c_medicgl_uber_gain_while_generator_out", "0", FCVAR_REPLICATED, "Enables UberCharge gain while an Uber Generator is deployed." );
ConVar tf2c_medicgl_health_to_uber_exchange_rate( "tf2c_medicgl_health_to_uber_exchange_rate", "14",	FCVAR_REPLICATED, "The exchange rate between heal points and Uber percentage (1%) for the Heal GL to use." );
ConVar tf2c_medicgl_uber_ignore_overheal( "tf2c_medicgl_uber_ignore_overheal", "1",						FCVAR_REPLICATED, "Whether or not to ignore the penalty usually applied to the Uber build rate when healing an overhealed patient." );
ConVar tf2c_medicgl_uber_ignore_maxhealth( "tf2c_medicgl_uber_ignore_maxhealth", "0",					FCVAR_REPLICATED, "Whether or not to ignore the penalty usually applied to the Uber build rate when healing a patient that is within 5% of their max overheal value." );
//ConVar tf2c_residualheal_build_rate_multiplier("tf2c_residualheal_build_rate_multiplier", "0.2",		FCVAR_NOTIFY, "The multiplier for the uber build rate of the residual healing effect applied by the MedicGL (as a proportion of the base build rate).");
ConVar tf2c_medicgl_direct_hit_uber_mult( "tf2c_medicgl_direct_hit_uber_mult", "2",						FCVAR_REPLICATED, "Direct hits give N seconds worth of uber building, where N is the fire interval, multiplied by this value." );
ConVar tf2c_medicgl_generator_duration("tf2c_medicgl_generator_duration", "6",							FCVAR_REPLICATED, "The duration of the Uber Generator.");
ConVar tf2c_medicgl_max_patients_for_uber( "tf2c_medicgl_max_patients_for_uber", "3",					FCVAR_REPLICATED, "How many healed patients contribute to uber charge gain for heal launcher", true, 1, true, MAX_PLAYERS );

extern ConVar tf2c_medigun_setup_uber;
extern ConVar tf2c_medigun_multi_uber_drain;
extern ConVar tf2c_medigun_critboostable;
extern ConVar tf2c_medigun_4team_uber_rate;
extern ConVar weapon_medigun_damage_modifier;
extern ConVar weapon_medigun_construction_rate;
extern ConVar weapon_medigun_charge_rate;
extern ConVar weapon_medigun_chargerelease_rate;
extern ConVar tf2c_uberratestacks_removetime;
extern ConVar tf2c_uberratestacks_max;
#if defined( CLIENT_DLL )
extern ConVar tf_medigun_autoheal;
extern ConVar hud_medicautocallers;
extern ConVar hud_medicautocallersthreshold;
extern ConVar hud_medichealtargetmarker;
extern ConVar tf2c_medicgl_show_patient_health;
#endif

//=============================================================================
//
// Heal GL tables.
//

#ifdef CLIENT_DLL
void RecvProxy_MainPatientGL( const CRecvProxyData *pData, void *pStruct, void *pOut )
{
#ifdef NADER_HSM
	CTFHealLauncher *pMedigun = (CTFHealLauncher *)pStruct;
	if ( pMedigun )
	{
		pMedigun-> // Trigger main target update
			m_bMainTargetParity = !pMedigun->m_bMainTargetParity;
	}
#endif
	RecvProxy_IntToEHandle( pData, pStruct, pOut );
}
#endif

IMPLEMENT_NETWORKCLASS_ALIASED(TFHealLauncher, DT_TFHealLauncher);

BEGIN_NETWORK_TABLE(CTFHealLauncher, DT_TFHealLauncher)
#ifdef CLIENT_DLL
RecvPropFloat(RECVINFO(m_flChargeLevel)),
RecvPropBool( RECVINFO( m_bChargeRelease ) ),
RecvPropBool( RECVINFO( m_bHolstered ) ),
RecvPropInt( RECVINFO( m_nUberRateBonusStacks ) ),
RecvPropFloat( RECVINFO( m_flUberRateBonus ) ),
RecvPropEHandle( RECVINFO( m_hMainPatient ), RecvProxy_MainPatientGL ),
RecvPropInt( RECVINFO( m_iMainPatientHealthLast ) ),
#else
SendPropFloat( SENDINFO( m_flChargeLevel ), 0, SPROP_NOSCALE | SPROP_CHANGES_OFTEN ),
SendPropBool( SENDINFO( m_bChargeRelease ) ),
SendPropBool( SENDINFO( m_bHolstered ) ),
SendPropInt( SENDINFO( m_nUberRateBonusStacks ) ),
SendPropFloat( SENDINFO( m_flUberRateBonus ) ),
SendPropEHandle( SENDINFO( m_hMainPatient ) ),
SendPropInt( SENDINFO( m_iMainPatientHealthLast ) ),
#endif
END_NETWORK_TABLE()

BEGIN_PREDICTION_DATA(CTFHealLauncher)
END_PREDICTION_DATA()

LINK_ENTITY_TO_CLASS(tf_weapon_heallauncher, CTFHealLauncher);
PRECACHE_WEAPON_REGISTER(tf_weapon_heallauncher);

#define HEAL_GRENADE_SOUNDCUE_INTERVAL 0.1f

//=============================================================================
//
// Weapon HealLauncher functions.
//

CTFHealLauncher::CTFHealLauncher( )
{
#ifdef CLIENT_DLL
	// Not sure which of these two methods is better.
	//gameeventmanager->AddListener( this, "patient_healed_notify", false );
	ListenForGameEvent( "patient_healed_notify" );
	m_flSloshSound = 0.0f;
	PrecacheScriptSound("Weapon_HealLauncher.Slosh");
#else
	m_bMainPatientFlaggedForRemoval = false;
#endif

	m_bGeneratorActive = false;
	m_bGeneratorCanBeActivated = false;
	m_bGeneratorDeployed = false;
	m_flNextSuccessCue = 0.0f;
	m_iNumPlayersGaveChargeThisTick = 0;
	m_pGenerator = NULL;
}

CTFHealLauncher::~CTFHealLauncher()
{
#ifdef CLIENT_DLL
	if (m_pChargeEffect)
	{
		C_BaseEntity* pEffectOwner = m_hChargeEffectHost.Get();
		if (pEffectOwner)
		{
			// Kill charge effect instantly when holstering otherwise it looks bad.
			if (m_bHolstered)
			{
				pEffectOwner->ParticleProp()->StopEmissionAndDestroyImmediately(m_pChargeEffect);
			}
			else
			{
				pEffectOwner->ParticleProp()->StopEmission(m_pChargeEffect);
			}

			m_hChargeEffectHost = NULL;
		}

		m_pChargeEffect = NULL;
	}

	if (m_pChargedSound)
	{
		CSoundEnvelopeController::GetController().SoundDestroy(m_pChargedSound);
		m_pChargedSound = NULL;
	}
#endif
}

#define	UBER_SHIELD_MODEL "models/items/shield_bubble/shield_bubble2.mdl" // "models/generator/forcefield/forcefield.mdl"

void CTFHealLauncher::Precache( void )
{
	PrecacheModel( UBER_SHIELD_MODEL );
	PrecacheScriptSound("HealGrenade.Splash");
	PrecacheScriptSound("HealGrenade.Max");

	BaseClass::Precache();
}

void CTFHealLauncher::WeaponReset(void)
{
	BaseClass::WeaponReset();
	m_flChargeLevel = 0.0f;
	m_bChargeRelease = false;
	m_bGeneratorDeployed = false;
	m_bGeneratorActive = false;
	m_bGeneratorCanBeActivated = false;
	m_flNextSuccessCue = 0.0f;
	m_flUberRateBonus = 0.0f;
	m_nUberRateBonusStacks = 0;
	m_hMainPatient = NULL;
#ifdef CLIENT_DLL
	m_flNextBuzzTime = 0;
	UTIL_GetHSM( this );
#else
	m_bMainPatientFlaggedForRemoval = false;
	m_iNumPlayersGaveChargeThisTick = 0;
#endif
}

bool CTFHealLauncher::Deploy( void )
{
	if ( BaseClass::Deploy() )
	{
		m_bHolstered = false;

#ifdef CLIENT_DLL
		ManageChargeEffect();

		UTIL_GetHSM(this);
#endif

		return true;
	}

	return false;
}


bool CTFHealLauncher::Holster( CBaseCombatWeapon *pSwitchingTo )
{
	//RemoveHealingTarget( true );
	//m_bAttacking = false;
	m_bHolstered = true;

#ifdef CLIENT_DLL
	//UpdateEffects();
	ManageChargeEffect();
#endif

	return BaseClass::Holster( pSwitchingTo );
}

//-----------------------------------------------------------------------------
// Purpose: Deploys Uber
//-----------------------------------------------------------------------------
void CTFHealLauncher::SecondaryAttack( void )
{
	if ( !CanAttack() )
		return;

	/*
	if ( !m_bGeneratorActive && m_bGeneratorCanBeActivated && m_bGeneratorDeployed )
	{
		m_pGenerator->Activate();
		return;
	}
	*/

	if ( m_bGeneratorDeployed )
		return;

	// Ensure they have a full charge 
	if ( m_flChargeLevel < GetMinChargeAmount() )
	{
#ifdef CLIENT_DLL
		// Deny, buzz.
		if ( gpGlobals->curtime >= m_flNextBuzzTime )
		{
			EmitSound( "Player.DenyWeaponSelection" );
			m_flNextBuzzTime = gpGlobals->curtime + 0.5f; // Only buzz every so often.
		}
#endif
		return;
	}

//	if ( UsesClipsForAmmo1() )
//		m_iClip1 = 0;

	AbortReload();

	// The check above will return false if the owner is NULL, so we can safely assume here.
	CTFPlayer *pOwner = assert_cast<CTFPlayer *>(GetOwner());
	if( !pOwner )
		return;

#ifndef CLIENT_DLL
	pOwner->NoteWeaponFired( this );
#endif

	// Stop the player shootin' for a lil while
	m_flNextPrimaryAttack = gpGlobals->curtime + ( GetFireRate() * 1.5 );
	m_flNextSecondaryAttack = gpGlobals->curtime + ( GetFireRate() * 2 );

	m_bChargeRelease = true;

	SendWeaponAnim( ACT_VM_SECONDARYATTACK );

	// Make weaponbase load our secondary projectile type!
	m_iWeaponMode = TF_WEAPON_SECONDARY_MODE;

	// This will do the ammo stuff too, so technically Ubering costs 1 ammo.
	// actually i dont want it to do that lol. copypaste some stuff
	// m_pGenerator = static_cast<CTFGeneratorUber*>( FireProjectile( pOwner ) );
#ifdef GAME_DLL
	Vector vecForward, vecRight, vecUp;
	AngleVectors( pOwner->EyeAngles() + pOwner->GetPunchAngle(), &vecForward, &vecRight, &vecUp );

	// Create grenades here!!
	Vector vecVelocity = vecForward * GetProjectileSpeed() +
		vecUp * 200.0f +
		vecRight * RandomFloat( -10.0f, 10.0f ) +
		vecUp * RandomFloat( -10.0f, 10.0f );

	Vector vecSrc;
	QAngle angForward;
	Vector vecOffset( 16.0f, -8.0f, -6.0f );

	GetProjectileFireSetup( pOwner, vecOffset, &vecSrc, &angForward, false, true );

	m_pGenerator = CTFGeneratorUber::Create(vecSrc, pOwner->EyeAngles(), vecVelocity, AngularImpulse(0, 0, 0), pOwner, this);

	if ( m_pGenerator )
	{
		float flRadius = GetTFWpnData().m_flDamageRadius;
		CALL_ATTRIB_HOOK_FLOAT( flRadius, mult_explosion_radius );
		m_pGenerator->SetDamageRadius( flRadius );
	}
#endif

	pOwner->DoAnimationEvent(PLAYERANIMEVENT_ATTACK_PRIMARY);

	WeaponSound(SPECIAL2);

	m_flLastPrimaryAttackTime = gpGlobals->curtime;
	
	DoFireEffects();
	
	UpdatePunchAngles(pOwner);

#ifdef CLIENT_DLL
	if (m_hExtraWearable)
	{
		int iBodygroup = m_hExtraWearable->FindBodygroupByName("mine");
		if (iBodygroup != -1)
		{
			m_hExtraWearable->SetBodygroup(iBodygroup, m_bChargeRelease);
		}
	}
#else

	CTF_GameStats.Event_PlayerInvulnerable( pOwner );
	// Handled by the generator
	//pOwner->m_Shared.RecalculateChargeEffects();

	pOwner->SpeakConceptIfAllowed( MP_CONCEPT_MEDIC_CHARGEDEPLOYED );

	IGameEvent *event = gameeventmanager->CreateEvent( "player_chargedeployed" );
	if ( event )
	{
		event->SetInt( "userid", pOwner->GetUserID() );

		gameeventmanager->FireEvent( event );
	}
#endif
}


//-----------------------------------------------------------------------------
// Purpose: Tells the medic and this launcher that the uber generator has
// been destroyed. 
//-----------------------------------------------------------------------------
void CTFHealLauncher::NotifyGenerator( GeneratorState stateChange )
{
	switch ( stateChange )
	{
		case TF_UBERGENSTATE_DEPLOYED:
			m_bChargeRelease = false;
			m_bGeneratorDeployed = true;
			m_bGeneratorActive = false;
			m_bGeneratorCanBeActivated = false;
			break;
		case TF_UBERGENSTATE_ACTIVATED:
			m_bChargeRelease = true;
			m_bGeneratorActive = true;
			m_bGeneratorCanBeActivated = false;
			break;
		case TF_UBERGENSTATE_DESTROYED:
			m_bChargeRelease = false;
			m_bGeneratorDeployed = false;
			m_bGeneratorActive = false;
			m_bGeneratorCanBeActivated = false;
			m_pGenerator = NULL;
			break;
		case TF_UBERGENSTATE_STUCK:
			m_bGeneratorCanBeActivated = true;
			break;
		default:
			break;
	}
}

//-----------------------------------------------------------------------------
// Purpose: Copy of Medigun AddCharge logic
//-----------------------------------------------------------------------------
void CTFHealLauncher::AddCharge( float flAmount )
{

	float flChargeRate = 1.0f;
	CALL_ATTRIB_HOOK_FLOAT( flChargeRate, mult_medigun_uberchargerate );
	CALL_ATTRIB_HOOK_FLOAT( flChargeRate, mult_medigun_uberchargerate_wearer );
	if ( !flChargeRate ) // Can't earn uber.
		return;

	float flNewLevel = Min( m_flChargeLevel + flAmount, 1.0f );
	flNewLevel = Max( flNewLevel, 0.0f );

#ifdef GAME_DLL
	bool bSpeak = !m_bChargeRelease && (flNewLevel >= 1.0f && m_flChargeLevel < 1.0f);
#endif
	m_flChargeLevel = flNewLevel;
#ifdef GAME_DLL
	if ( bSpeak )
	{
		CTFPlayer *pPlayer = GetTFPlayerOwner();
		if ( pPlayer )
		{
			pPlayer->SpeakConceptIfAllowed( MP_CONCEPT_MEDIC_CHARGEREADY );
		}
	}
#endif
}

void CTFHealLauncher::OnDirectHit( CTFPlayer *pPatient )
{
	// Build direct-hit Uber based on our firerate

	CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );

	if ( !m_bChargeRelease && pOwner)
	{
		if ( weapon_medigun_charge_rate.GetFloat() && pPatient->GetHealth() <= pPatient->GetMaxHealth() )
		{
			int iBoostMax = floor( pPatient->m_Shared.GetMaxBuffedHealth() * 0.95f );
			//float flChargeAmount = flAmount / (tf2c_medicgl_health_to_uber_exchange_rate.GetFloat() * 100.0f);//weapon_medigun_charge_rate.GetFloat();
			float flChargeAmount;

			// Medigun healing for T seconds, where T is the fire interval -> 1 direct hit's uber build amount
			flChargeAmount = GetFireRate() / (weapon_medigun_charge_rate.GetFloat());
			flChargeAmount *= tf2c_medicgl_direct_hit_uber_mult.GetFloat();

			bool bInSetup = (TFGameRules() && TFGameRules()->InSetup() &&
#ifdef GAME_DLL
				TFGameRules()->GetActiveRoundTimer() &&
#endif
				tf2c_medigun_setup_uber.GetBool());

			// We can optionally skip this part since we already have a reduced overheal rate that's reflected in flAmount already.
			if ( pPatient->GetHealth() >= pPatient->GetMaxHealth() && !bInSetup && !tf2c_medicgl_uber_ignore_overheal.GetBool() )
			{
				CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( pOwner, flChargeAmount, mult_medigun_overheal_uberchargerate );
			}

			// On the gun we're using
			CALL_ATTRIB_HOOK_FLOAT( flChargeAmount, mult_medigun_uberchargerate );

			// On the Healer themselves
			CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( pOwner, flChargeAmount, mult_medigun_uberchargerate_wearer );

			if ( bInSetup )
			{
				// Build charge at an increased rate during setup.
				flChargeAmount *= 3.0f;
			}
			else if ( pPatient->GetHealth() >= iBoostMax && !tf2c_medicgl_uber_ignore_maxhealth.GetBool() )
			{
				// Reduced charge for healing fully healed guys.
				flChargeAmount *= 0.5f;
			}

			// Speed up charge rate when under minicrit or crit buffs.
			if ( tf2c_medigun_critboostable.GetBool() )
			{
				if ( pOwner->m_Shared.IsCritBoosted() )
				{
					flChargeAmount *= 3.0f;
				}
				else if ( pOwner->m_Shared.InCond( TF_COND_DAMAGE_BOOST ) || IsWeaponDamageBoosted() )
				{
					flChargeAmount *= 1.35f;
				}
			}

			// Don't speed up Heal Launcher charge rate with Haste since it can already shoot and reload faster.
			//if (pOwner->m_Shared.InCond(TF_COND_CIV_SPEEDBUFF))
			//{
			//	flChargeAmount *= TF2C_HASTE_UBER_FACTOR;
			//}

			// In 4team, speed up charge rate to make up for smaller teams and decreased survivability.
			if ( TFGameRules() && TFGameRules()->IsFourTeamGame() )
			{
				flChargeAmount *= tf2c_medigun_4team_uber_rate.GetFloat();
			}

			// Reduce charge rate when healing someone already being healed.
			int iTotalHealers = pPatient->m_Shared.GetNumHumanHealers();
			if ( !bInSetup && iTotalHealers > 1 )
			{
				flChargeAmount /= (float)iTotalHealers;
			}

			// Build rate bonus stacks
#ifdef GAME_DLL
			CheckAndExpireStacks();
#endif
			flChargeAmount *= 1.0f + GetUberRateBonus();

			float flNewLevel = Min( m_flChargeLevel + flChargeAmount, 1.0f );

			bool bSpeak = (flNewLevel >= GetMinChargeAmount() && m_flChargeLevel < GetMinChargeAmount());
			m_flChargeLevel = flNewLevel;

			if ( bSpeak )
			{
#ifdef GAME_DLL
				pOwner->SpeakConceptIfAllowed( MP_CONCEPT_MEDIC_CHARGEREADY );
#endif
			}
		}
	}

	m_hMainPatient = pPatient;
	m_iMainPatientHealthLast = pPatient->GetHealth();
}

//-----------------------------------------------------------------------------
// Purpose: Hook for external sources to notify the gun that we've healed 
// someone and that we shoud add Uber charge.
//-----------------------------------------------------------------------------
void CTFHealLauncher::OnHealedPlayer( CTFPlayer *pPatient, float flAmount, HealerType tType )
{
#ifdef GAME_DLL
	CTFPlayer *pOwner = ToTFPlayer( GetOwnerEntity() );
	if( !pOwner || !pPatient )
		return;

	if ( gpGlobals->curtime > m_flNextSuccessCue && tType == HEALER_TYPE_BURST)
	{
		//check if this is a direct hit
		float flHealAmountDirect = 0.0f;
		CALL_ATTRIB_HOOK_FLOAT(flHealAmountDirect, apply_heal_explosion);
		flHealAmountDirect *= 0.9;
		std::string sound = "HealGrenade.Success";

		// Only the healer and patient should hear this sound.
		CRecipientFilter filterOwner;
		filterOwner.AddRecipient( pOwner );
		CRecipientFilter filterPatient;
		filterPatient.AddRecipient( pPatient );

		//play the proper sound. 
		if (pPatient->GetHealth() - flAmount >= pPatient->GetMaxHealth())
		{
			sound = "HealGrenade.Max";
		}
		else if (flAmount >= flHealAmountDirect) 
		{
			sound = "HealGrenade.Success";
		}
		else
		{
			sound = "HealGrenade.Splash";
		}

		pOwner->EmitSound(filterOwner, pOwner->entindex(), sound.c_str());
		pPatient->EmitSound(filterPatient, pPatient->entindex(), sound.c_str());

		m_flNextSuccessCue = gpGlobals->curtime + HEAL_GRENADE_SOUNDCUE_INTERVAL;
	}

	IGameEvent* event = gameeventmanager->CreateEvent( "patient_healed_notify" );
	if ( event )
	{
		event->SetInt( "userid", pPatient->GetUserID() );
		event->SetInt( "healerid", pOwner->GetUserID() );
		event->SetInt( "amount", flAmount );
		gameeventmanager->FireEvent( event );
	}
#endif
}

void CTFHealLauncher::BuildUberForTarget( CBaseEntity *pTarget, bool bMultiTarget /*= false*/ )
{
	CTFPlayer *pPatient = ToTFPlayer( pTarget );

	// Charge up our power if we're not releasing it, and our target
	// isn't receiving any benefit from our healing. (what da heck does this part mean? - hogyn)
	if ( !m_bChargeRelease )
	{
		// Limit max patients for uber charge gain.
		m_iNumPlayersGaveChargeThisTick += 1;

		int iMaxPatientsForCharge = tf2c_medicgl_max_patients_for_uber.GetInt();
		if ( m_iNumPlayersGaveChargeThisTick > iMaxPatientsForCharge )
		{
			//DevMsg( "rejected charge gain from patient %s\n", pPatient->GetPlayerName() );
			return;
		}
		else
		{
			//DevMsg( "gained charge stack %i from patient %s\n", m_iNumPlayersGaveChargeThisTick, pPatient->GetPlayerName() );
		}

		CTFPlayer *pOwner = GetTFPlayerOwner();

		if ( weapon_medigun_charge_rate.GetFloat() )
		{
			int iBoostMax = floor( pPatient->m_Shared.GetMaxBuffedHealth() * 0.95f );
			//float flChargeAmount = flAmount / (tf2c_medicgl_health_to_uber_exchange_rate.GetFloat() * 100.0f);//weapon_medigun_charge_rate.GetFloat();
			float flChargeAmount;

			flChargeAmount = gpGlobals->frametime / weapon_medigun_charge_rate.GetFloat();

			// If we're able to gain uber charge from more than 1 patient, scale our gain down if we're only healing a few.
			// However, if max is e.g. 3, we don't want healing 3 patients to be 3x as fast as healing 1.
			// Apply diminishing returns the higher the number of patients is.
			if ( iMaxPatientsForCharge > 1 )
			{
				// Start by basing it on the halfway count...
				flChargeAmount /= ( iMaxPatientsForCharge * 0.5 );

				// Then divide the higher we get
				if( m_iNumPlayersGaveChargeThisTick > 0 )
					flChargeAmount /= m_iNumPlayersGaveChargeThisTick;
			}

			bool bInSetup = (TFGameRules() && TFGameRules()->InSetup() &&
#ifdef GAME_DLL
				TFGameRules()->GetActiveRoundTimer() &&
#endif
				tf2c_medigun_setup_uber.GetBool());

			// We can optionally skip this part since we already have a reduced overheal rate that's reflected in flAmount already.
			if ( pPatient->GetHealth() >= pPatient->GetMaxHealth() && !bInSetup && !tf2c_medicgl_uber_ignore_overheal.GetBool() )
			{
				CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( pOwner, flChargeAmount, mult_medigun_overheal_uberchargerate );
			}

			// On the gun we're using
			CALL_ATTRIB_HOOK_FLOAT( flChargeAmount, mult_medigun_uberchargerate );

			// On the Healer themselves
			CALL_ATTRIB_HOOK_FLOAT_ON_OTHER( pOwner, flChargeAmount, mult_medigun_uberchargerate_wearer );

			if ( bInSetup )
			{
				// Build charge at an increased rate during setup.
				flChargeAmount *= 3.0f;
			}
			else if ( pPatient->GetHealth() >= iBoostMax && !tf2c_medicgl_uber_ignore_maxhealth.GetBool() )
			{
				// Reduced charge for healing fully healed guys.
				flChargeAmount *= 0.5f;
			}

			// Speed up charge rate when under minicrit or crit buffs.
			if ( tf2c_medigun_critboostable.GetBool() )
			{
				if ( pOwner->m_Shared.IsCritBoosted() )
				{
					flChargeAmount *= 3.0f;
				}
				else if ( pOwner->m_Shared.InCond( TF_COND_DAMAGE_BOOST ) || IsWeaponDamageBoosted() )
				{
					flChargeAmount *= 1.35f;
				}
			}

			// Don't speed up Heal Launcher charge rate with Haste since it can already shoot and reload faster.
			//if (pOwner->m_Shared.InCond(TF_COND_CIV_SPEEDBUFF))
			//{
			//	flChargeAmount *= TF2C_HASTE_UBER_FACTOR;
			//}

			// In 4team, speed up charge rate to make up for smaller teams and decreased survivability.
			if ( TFGameRules() && TFGameRules()->IsFourTeamGame() )
			{
				flChargeAmount *= tf2c_medigun_4team_uber_rate.GetFloat();
			}

			// Reduce charge rate when healing someone already being healed.
			int iTotalHealers = pPatient->m_Shared.GetNumHumanHealers();
			if ( !bInSetup && iTotalHealers > 1 )
			{
				flChargeAmount /= (float)iTotalHealers;
			}

			// Build rate bonus stacks
#ifdef GAME_DLL
			CheckAndExpireStacks();
#endif
			flChargeAmount *= 1.0f + GetUberRateBonus();

			float flNewLevel = Min( m_flChargeLevel + flChargeAmount, 1.0f );

			bool bSpeak = (flNewLevel >= GetMinChargeAmount() && m_flChargeLevel < GetMinChargeAmount());
			m_flChargeLevel = flNewLevel;
			//DevMsg( "charge: %2.2f (+%f (+%f per second))\n", m_flChargeLevel.Get() * 100.0f, flChargeAmount * 100.0f, flChargeAmount * 100.0f / gpGlobals->interval_per_tick );

			if ( bSpeak )
			{
#ifdef GAME_DLL
				pOwner->SpeakConceptIfAllowed( MP_CONCEPT_MEDIC_CHARGEREADY );
#endif
			}
		}
	}
}

void CTFHealLauncher::DrainCharge( void )
{
	if (!m_bChargeRelease)
		return;

	CTFPlayer* pOwner = GetTFPlayerOwner();
	if (!pOwner)
		return;

	// If we're in charge release mode, drain our charge.
	float flUberTime = tf2c_medicgl_generator_duration.GetFloat();
	CALL_ATTRIB_HOOK_FLOAT_ON_OTHER(pOwner, flUberTime, add_uber_time);
	CALL_ATTRIB_HOOK_FLOAT(flUberTime, add_uber_time_active);

	float flChargeAmount = gpGlobals->frametime / flUberTime;
	m_flChargeLevel = Max(m_flChargeLevel - flChargeAmount, 0.0f);

	if ( !m_flChargeLevel )
	{
		m_bChargeRelease = false;
	}
}

void CTFHealLauncher::AddUberRateBonusStack( float flBonus, int nStack /*= 1*/ )
{
#ifdef GAME_DLL
	CheckAndExpireStacks();
#endif

	if ( m_nUberRateBonusStacks < tf2c_uberratestacks_max.GetInt() ) {
		m_flUberRateBonus += flBonus * nStack;
		m_nUberRateBonusStacks += nStack;
		//DevMsg( "Added charge bonus: %2.4f (%2.4f total) \n", flBonus, m_flUberRateBonus );
		//DevMsg( "Stacks: %i (%i total) \n", nStack, m_nUberRateBonusStacks );
	}
#ifdef GAME_DLL
	m_flStacksRemoveTime = gpGlobals->curtime + tf2c_uberratestacks_removetime.GetFloat();
#endif
}

#ifdef GAME_DLL

void CTFHealLauncher::CheckAndExpireStacks()
{
	if ( m_nUberRateBonusStacks )
	{
		if ( gpGlobals->curtime > m_flStacksRemoveTime )
		{
			//DevMsg( "Charge bonus stacks removed! \n" );
			m_nUberRateBonusStacks = 0;
			m_flUberRateBonus = 0.0f;
		}
	}
}

bool CTFHealLauncher::IsBackpackPatient( CTFPlayer *pPlayer )
{
	return m_vBackpackTargets.HasElement( pPlayer );
}
#endif

int	CTFHealLauncher::GetUberRateBonusStacks( void ) const
{
	return m_nUberRateBonusStacks;
}

float CTFHealLauncher::GetUberRateBonus( void ) const
{
	return m_flUberRateBonus / 100.0f;
}


void CTFHealLauncher::ItemPostFrame( void )
{
	BaseClass::ItemPostFrame();
#ifdef GAME_DLL

	m_iNumPlayersGaveChargeThisTick = 0;

#ifdef NADER_HSM
	if( m_bMainPatientFlaggedForRemoval )
	{
		m_hMainPatient = NULL;
		m_bMainPatientFlaggedForRemoval = false;
	}

	if ( m_hMainPatient.Get() && !m_bMainPatientFlaggedForRemoval )
		m_bMainPatientFlaggedForRemoval = true;
#endif

	CheckAndExpireStacks();
#else
#ifdef NADER_HSM
	if ( GetTFPlayerOwner()->IsLocalPlayer() && prediction->IsFirstTimePredicted() )
		UTIL_HSMSetPlayerThink( ToTFPlayer( m_hMainPatient.Get() ) );
#endif
#endif
}

void CTFHealLauncher::ItemHolsterFrame( void )
{
	BaseClass::ItemHolsterFrame();
#ifdef NADER_HSM
#ifdef GAME_DLL
	if ( m_bMainPatientFlaggedForRemoval )
	{
		m_hMainPatient = NULL;
		m_bMainPatientFlaggedForRemoval = false;
	}
#else
	if ( GetTFPlayerOwner()->IsLocalPlayer() )
		UTIL_HSMSetPlayerThink( ToTFPlayer( m_hMainPatient.Get() ) );
#endif
#endif
}


medigun_charge_types CTFHealLauncher::GetChargeType( void )
{
	int iChargeType = TF_CHARGE_INVULNERABLE;
	CALL_ATTRIB_HOOK_INT( iChargeType, set_charge_type );
	if ( iChargeType > TF_CHARGE_NONE && iChargeType < TF_CHARGE_COUNT )
		return (medigun_charge_types)iChargeType;

	AssertMsg( 0, "Invalid charge type!\n" );
	return TF_CHARGE_NONE;
}


float CTFHealLauncher::GetProgress( void )
{
	CTFPlayer* pPlayer = GetTFPlayerOwner();
	if (!pPlayer)
		return -1;

	float flMaxClip = GetMaxClip1();
	float flClip = Clip1() + GetEffectBarProgress();
	return flClip / flMaxClip;
}

#if defined( CLIENT_DLL )

void CTFHealLauncher::ManageChargeEffect( void )
{
	CTFPlayer *pOwner = GetTFPlayerOwner();

	bool bOwnerTaunting = (pOwner && pOwner->m_Shared.InCond( TF_COND_TAUNTING ));
	if ( pOwner && !bOwnerTaunting && !m_bHolstered && (m_flChargeLevel >= GetMinChargeAmount() || m_bChargeRelease) )
	{
		if ( !m_pChargeEffect )
		{
			C_BaseEntity *pEffectOwner = GetWeaponForEffect();
			if ( pEffectOwner )
			{
				const char *pszEffectName = ConstructTeamParticle( "medicgun_invulnstatus_fullcharge_%s", pOwner->GetTeamNumber() );
				m_pChargeEffect = pEffectOwner->ParticleProp()->Create( pszEffectName, PATTACH_POINT_FOLLOW, "muzzle" );
				m_hChargeEffectHost = pEffectOwner;
			}
		}

		if ( !m_pChargedSound )
		{
			CSoundEnvelopeController &controller = CSoundEnvelopeController::GetController();

			CLocalPlayerFilter filter;
			m_pChargedSound = controller.SoundCreate( filter, entindex(), "WeaponMedigun.Charged" );
			controller.Play( m_pChargedSound, 1.0, 100 );
		}
	}
	else
	{
		if ( m_pChargeEffect )
		{
			C_BaseEntity *pEffectOwner = m_hChargeEffectHost.Get();
			if ( pEffectOwner )
			{
				// Kill charge effect instantly when holstering otherwise it looks bad.
				if ( m_bHolstered )
				{
					pEffectOwner->ParticleProp()->StopEmissionAndDestroyImmediately( m_pChargeEffect );
				}
				else
				{
					pEffectOwner->ParticleProp()->StopEmission( m_pChargeEffect );
				}

				m_hChargeEffectHost = NULL;
			}

			m_pChargeEffect = NULL;
		}

		if ( m_pChargedSound )
		{
			CSoundEnvelopeController::GetController().SoundDestroy( m_pChargedSound );
			m_pChargedSound = NULL;
		}
	}
}

//-----------------------------------------------------------------------------
// Purpose: 
// Input  : updateType - 
//-----------------------------------------------------------------------------
void CTFHealLauncher::OnDataChanged( DataUpdateType_t updateType )
{
	BaseClass::OnDataChanged( updateType );

	if ( m_bUpdateHealingTargets )
	{
		//UpdateEffects();
		m_bUpdateHealingTargets = false;
	}

#ifdef NADER_HSM
	if ( m_bMainTargetParityOld != m_bMainTargetParity && g_pHealSoundManager && GetTFPlayerOwner()->IsLocalPlayer() )
	{
		// Let the heal sound manager know of the recorded health from before the healing was done.
		if ( m_hMainPatient )
		{
            // should never happen but it does
            Assert(g_pHealSoundManager);
            if (!g_pHealSoundManager)
            {
                return;
            }
			g_pHealSoundManager->SetPatient( ToTFPlayer( m_hMainPatient.Get() ) );
			g_pHealSoundManager->SetLastPatientHealth( m_iMainPatientHealthLast );
		}
		//DevMsg("Set last patient health.\n");
	}
#endif

	// Think?
	ClientThinkList()->SetNextClientThink( GetClientHandle(), CLIENT_THINK_ALWAYS );

	ManageChargeEffect();

	UpdateMedicAutoCallers();

	if( m_hExtraWearable )
	{
		m_hExtraWearable->SetPoseParameter("backpack_charge_level", m_flChargeLevel);
	}
}

void CTFHealLauncher::OnPreDataChanged( DataUpdateType_t updateType )
{
	BaseClass::OnPreDataChanged( updateType );
#ifdef NADER_HSM
	m_bMainTargetParityOld = m_bMainTargetParity;
#endif
}


void CTFHealLauncher::ClientThink()
{
	// Don't show it while the player is dead. Ideally, we'd respond to m_bHealing in OnDataChanged,
	// but it stops sending the weapon when it's holstered, and it gets holstered when the player dies.
	CTFPlayer *pFiringPlayer = GetTFPlayerOwner();
	if ( !pFiringPlayer || pFiringPlayer->IsPlayerDead() || pFiringPlayer->IsDormant() )
	{
		ClientThinkList()->SetNextClientThink( GetClientHandle(), CLIENT_THINK_NEVER );
		//m_bPlayingSound = false;
		//StopHealSound();
		return;
	}

	// If the local player is the guy getting healed, let him know 
	// who's healing him, and their charge level.
	/*if ( m_hHealingTarget )
	{
		C_TFPlayer *pLocalPlayer = C_TFPlayer::GetLocalTFPlayer();
		if ( pLocalPlayer && pLocalPlayer == m_hHealingTarget )
		{
			pLocalPlayer->SetHealer( pFiringPlayer, m_flChargeLevel );
		}

		if ( !m_bPlayingSound )
		{
			m_bPlayingSound = true;
			CLocalPlayerFilter filter;
			EmitSound( filter, entindex(), GetHealSound() );
		}
	}*/

	if ( m_bOldChargeRelease != m_bChargeRelease )
	{
		m_bOldChargeRelease = m_bChargeRelease;

		if( m_hExtraWearable )
		{
			int iBodygroup = m_hExtraWearable->FindBodygroupByName("mine");
			if (iBodygroup != -1)
			{
				m_hExtraWearable->SetBodygroup(iBodygroup, m_bChargeRelease);
			}
		}
		//ForceHealingTargetUpdate();
	}
}


void CTFHealLauncher::ThirdPersonSwitch( bool bThirdperson )
{
	BaseClass::ThirdPersonSwitch( bThirdperson );

	// Restart any effects.
	if ( m_hHealingTargetEffect.pEffect )
	{
		C_BaseEntity *pEffectOwner = m_hHealingTargetEffect.hOwner.Get();
		if ( m_hHealingTargetEffect.pEffect && pEffectOwner )
		{
			pEffectOwner->ParticleProp()->StopEmissionAndDestroyImmediately( m_hHealingTargetEffect.pEffect );
		}

		//UpdateEffects();
	}

	if ( m_pChargeEffect )
	{
		C_BaseEntity *pEffectOwner = m_hChargeEffectHost.Get();
		if ( pEffectOwner )
		{
			pEffectOwner->ParticleProp()->StopEmissionAndDestroyImmediately( m_pChargeEffect );
			m_pChargeEffect = NULL;
			m_hChargeEffectHost = NULL;
		}

		ManageChargeEffect();
	}
}


void CTFHealLauncher::UpdateMedicAutoCallers( void )
{
	C_TFPlayer *pOwner = GetTFPlayerOwner();
	if ( !pOwner || !pOwner->IsLocalPlayer() || !hud_medicautocallers.GetBool() )
		return;

	// Monitor teammates' health levels.
	for ( int i = 1; i <= gpGlobals->maxClients; i++ )
	{
		// Ignore enemies and dead players.
		C_TFPlayer *pPlayer = ToTFPlayer( UTIL_PlayerByIndex( i ) );
		if ( !pPlayer || pPlayer->IsDormant() || pPlayer->GetTeamNumber() != pOwner->GetTeamNumber() || !pPlayer->IsAlive() )
		{
			m_bPlayerHurt[i] = false;
			continue;
		}

		// Create auto caller if their health drops below threshold.
		// Only create auto callers for nearby players.
		bool bHurt = (pPlayer->HealthFraction() <= hud_medicautocallersthreshold.GetFloat() * 0.01f);
		if ( pOwner->IsAlive() && bHurt && !m_bPlayerHurt[i] && pOwner->GetAbsOrigin().DistTo( pPlayer->GetAbsOrigin() ) < 1000.0f )
		{
			pPlayer->CreateSaveMeEffect( true );
		}

		m_bPlayerHurt[i] = bHurt;
	}
}

void CTFHealLauncher::UpdateRecentPatientHealthbar( C_TFPlayer *pPatient )
{
	pPatient->CreateOverheadHealthbar();
}

void CTFHealLauncher::FireGameEvent( IGameEvent* pEvent )
{
	if ( tf2c_medicgl_show_patient_health.GetBool() )
	{
		if ( !strcmp( "patient_healed_notify", pEvent->GetName() ) )
		{
			int iUserID = engine->GetPlayerForUserID( pEvent->GetInt( "userid" ) );
			int iHealerID = engine->GetPlayerForUserID( pEvent->GetInt( "healerid" ) );
			CTFPlayer *pPatient = ToTFPlayer(ClientEntityList().GetEnt( iUserID ));
			CTFPlayer *pHealer = ToTFPlayer(ClientEntityList().GetEnt( iHealerID ));
			if( !pHealer || !pHealer->IsLocalPlayer())
				return;

			if ( pPatient )
				UpdateRecentPatientHealthbar( pPatient );
			else
				DevWarning( "patient_healed_notify called on client, but could not cast to player! userid : %i\n", iUserID );
			//DevMsg( "patient_healed_notify called on client! Woohoo! Name: %s, userid: %i, player\n" );
		}
	}
}

void CTFHealLauncher::CreateMove(float flInputSampleTime, CUserCmd* pCmd, const QAngle& vecOldViewAngles)
{
	C_BasePlayer* pOwner = GetPlayerOwner();
	if (pOwner)
	{
		const QAngle oldAngles = pOwner->EyeAngles();
		QAngle newAngles = pCmd->viewangles;
		float turn = (newAngles - oldAngles).Length();
		if (turn > 2 && turn < 50 && m_flSloshSound <= gpGlobals->curtime)
		{
			CSingleUserRecipientFilter PlayerFilter(pOwner);
			PlayerFilter.MakeReliable();
			EmitSound(PlayerFilter, entindex(), "Weapon_HealLauncher.Slosh");
			m_flSloshSound = gpGlobals->curtime + .35;
		}
	}
}

#endif
