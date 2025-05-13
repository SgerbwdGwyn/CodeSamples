//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. =======
//
// Purpose: 
//
//=============================================================================

#ifndef TF_WEAPON_HEALLAUNCHER_H
#define TF_WEAPON_HEALLAUNCHER_H
#ifdef _WIN32
#pragma once
#endif

#include "tf_weapon_grenadelauncher.h"
#include "tf_weapon_medigun.h"
#include "tf_weapon_generator_uber.h"
#ifdef CLIENT_DLL
#include "tf_hud_mediccallers.h"
#endif
#ifdef GAME_DLL
#include "tf_player.h"
#endif

#ifdef CLIENT_DLL
#define CTFHealLauncher C_TFHealLauncher
#endif

//=============================================================================
//
// Heal GL weapon class.
//
class CTFHealLauncher : public CTFGrenadeLauncher, public ITFHealingWeapon
{
public:
	DECLARE_CLASS(CTFHealLauncher, CTFGrenadeLauncher);
	DECLARE_NETWORKCLASS();
	DECLARE_PREDICTABLE();

	CTFHealLauncher();
	~CTFHealLauncher();

	virtual ETFWeaponID	GetWeaponID(void) const { return TF_WEAPON_HEALLAUNCHER; }

	virtual void		Precache( void );
	void				WeaponReset(void);

	enum GeneratorState {
		TF_UBERGENSTATE_DEPLOYED,
		TF_UBERGENSTATE_ACTIVATED,
		TF_UBERGENSTATE_DESTROYED,
		TF_UBERGENSTATE_STUCK
	};

	virtual bool		Deploy( void );
	virtual bool		Holster( CBaseCombatWeapon *pSwitchingTo );

	virtual void		ItemPostFrame( void ) override;
	virtual void		ItemHolsterFrame( void ) override;

	const char			*GetEffectLabelText( void ) { return "#TF_MedicGL"; }
	float				GetProgress( void );

	void				SecondaryAttack( void ) override;
	void				NotifyGenerator( GeneratorState stateChange );
	void				DrainCharge( void ) override;
	void				AddCharge( float flAmount ) override;
	void				OnHealedPlayer( CTFPlayer *pPatient, float flAmount, HealerType tType ) override;
	void				OnDirectHit( CTFPlayer *pPatient );
	float				GetChargeLevel( void ) const override { return m_flChargeLevel; }
	virtual float		GetMinChargeAmount() const override { return 1.00f; } // live TF2: used for Vaccinator uber charge chunks
	bool				IsReleasingCharge( void ) const override { return m_bChargeRelease; }
	medigun_charge_types GetChargeType( void );
	CTFWeaponBase		*GetWeapon( void ) override { return this; }
	int					GetMedigunType( void ) override { return TF_MEDIGUN_HEALLAUNCHER; }
	bool				AutoChargeOwner( void ) { return false; }
	virtual void		AddUberRateBonusStack( float flBonus, int nStack = 1 );
	virtual int			GetUberRateBonusStacks( void )const;
	virtual float		GetUberRateBonus( void ) const;
	virtual void		UpdateBackpackMaxTargets( void ) {};
	virtual void		BuildUberForTarget( CBaseEntity *pTarget, bool bMultiTarget = false ) override;
#ifdef GAME_DLL
	virtual void		CheckAndExpireStacks( void );
	virtual void		AddBackpackPatient( CTFPlayer *pPlayer ) {}
	virtual bool		IsBackpackPatient( CTFPlayer *pPlayer );
	virtual void		RemoveBackpackPatient( int iIndex ) {}
#endif

#ifdef CLIENT_DLL
	void			UpdateRecentPatientHealthbar( C_TFPlayer *pPatient );
	virtual void	FireGameEvent( IGameEvent * event ) override;
#endif

	CNetworkHandle( CBaseEntity, m_hMainPatient );
	CNetworkVar( int, m_iMainPatientHealthLast );
	CNetworkVar( bool, m_bMainTargetParity );

protected:
	CNetworkVar( float, m_flChargeLevel );
	CNetworkVar( bool, m_bChargeRelease ); // Copy from Medigun: Used to track whether our Uber is active in the world or not.
	CNetworkVar( bool, m_bHolstered );
	CNetworkVar( int, m_iPlayerLastHealedIndex );
	CNetworkVar( int, m_nUberRateBonusStacks );			// How many stacks of increased uber build rate are allowed
	CNetworkVar( float, m_flUberRateBonus );			// The total uber build rate bonus
#ifdef CLIENT_DLL
	float					m_flNextBuzzTime;

	//bool					m_bPlayingSound;
	bool					m_bUpdateHealingTargets;
	struct healingtargeteffects_t
	{
		EHANDLE				hOwner;
		C_BaseEntity		*pTarget;
		CNewParticleEffect	*pEffect;
	};
	healingtargeteffects_t m_hHealingTargetEffect;

	float					m_flFlashCharge;
	bool					m_bOldChargeRelease;

	CNewParticleEffect		*m_pChargeEffect;
	EHANDLE					m_hChargeEffectHost;
	CSoundPatch				*m_pChargedSound;

	int						m_bPlayerHurt[MAX_PLAYERS + 1];

	bool					m_bMainTargetParityOld;
#else
	bool					m_bMainPatientFlaggedForRemoval;
#endif

private:

	CTFHealLauncher(const CTFHealLauncher &);
	bool				m_bGeneratorDeployed;
	bool				m_bGeneratorActive;
	bool				m_bGeneratorCanBeActivated;
	CTFGeneratorUber	*m_pGenerator;
	float				m_flNextSuccessCue;

	int					m_iNumPlayersGaveChargeThisTick; // Counts (and limits) how many patients we gain ubercharge from
#if defined( CLIENT_DLL )
	// Stop all sounds being output.
	//void			StopHealSound( bool bStopHealingSound = true, bool bStopNoTargetSound = true );

	virtual void	OnDataChanged( DataUpdateType_t updateType ) override;
	virtual void	OnPreDataChanged( DataUpdateType_t updateType ) override;
	virtual void	ClientThink();
	virtual void	ThirdPersonSwitch( bool bThirdperson );

	//void			UpdateEffects( void );
	//void			ForceHealingTargetUpdate( void ) { m_bUpdateHealingTargets = true; }

	void			ManageChargeEffect( void );

	void			UpdateMedicAutoCallers( void );

	// slosh sound!! thanks pvk!!
	virtual void	CreateMove(float flInputSampleTime, CUserCmd* pCmd, const QAngle& vecOldViewAngles);
	float			m_flSloshSound;
#endif
#ifdef GAME_DLL
public:
	bool IsGeneratorDeployed() { return m_bGeneratorDeployed; }
#endif
};

#endif // TF_WEAPON_HEALLAUNCHER_H
