//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. ========//
//
// Purpose: TF Uber Generator projectile launched by the Medic's GL.
//
//=============================================================================//
#ifndef TF_WEAPON_GENERATOR_UBER_H
#define TF_WEAPON_GENERATOR_UBER_H
#ifdef _WIN32
#pragma once
#endif

#include "tf_weaponbase_grenadeproj.h"
#ifdef GAME_DLL
#include "tf_player.h"
#include "props_shared.h"
#endif

// Client specific.
#ifdef CLIENT_DLL
#define CTFGeneratorUber C_TFGeneratorUber
#define CTFGeneratorUberShield C_TFGeneratorUberShield
#endif

class CTFGeneratorUberShield : public CBaseAnimating
{
public:
	DECLARE_CLASS(CTFGeneratorUberShield, CBaseAnimating);
#ifdef GAME_DLL
	DECLARE_DATADESC();
#endif
	DECLARE_NETWORKCLASS();
#ifdef GAME_DLL
	CTFGeneratorUberShield() 
	{
		m_bEnabled = false;
	}

	void Fizzle(void);
	void Spawn(void);
	void Precache(void);
	void Kill(void);
	int ShouldTransmit(CCheckTransmitInfo* pInfo);
	int UpdateTransmitState(void);
private:
	bool m_bEnabled;
#endif
};


//=============================================================================
class CTFGeneratorUber : public CTFBaseGrenade
{
public:
	DECLARE_CLASS( CTFGeneratorUber, CTFBaseGrenade );
	DECLARE_NETWORKCLASS();

	CTFGeneratorUber();
	~CTFGeneratorUber();

	virtual void		UpdateOnRemove( void );

	float				GetCreationTime( void ) { return m_flCreationTime; }
	virtual void		Precache( void ) override;
	static const char			*GetActivationSound() { return "UberNade.Explode"; }

	EHANDLE GetShield() { return m_hShield; }
private:
	CNetworkVar(EHANDLE, m_hShield);
	CNetworkVar( float, m_flCreationTime );
	CNetworkVar( bool, m_bGeneratorActive );
	CNetworkVar( float, m_flEffectRadius );
	CNetworkVar( Vector, m_vecTeamColour );
	CNetworkVar( bool, m_bUberTargetsParity );

public:
	// MUST be public so the networking functions can access it
	CUtlVector<EHANDLE> m_hUberTargets;
	bool IsGeneratorActive() { return m_bGeneratorActive; }

#ifdef CLIENT_DLL

public:
	virtual void		OnPreDataChanged( DataUpdateType_t updateType );
	virtual void		OnDataChanged( DataUpdateType_t updateType );
	virtual void		CreateTrails( void );
	virtual int			DrawModel( int flags );
	virtual void		Simulate( void );
	virtual void		DrawRadius( float flRadius );
	void				UpdateUberTargets() { m_bUpdateUberTargets = true; };
	bool				ShouldShowUberEffectForPlayer( C_TFPlayer *pPlayer );
	void				UpdateEffects( void );

private:
	bool		m_bPulsed;
	bool		m_bUpdateUberTargets;
	bool		m_bOldUberTargetsParity;

	struct ubertargeteffects_t
	{
		C_BaseEntity		*pTarget;
		CNewParticleEffect	*pEffect;
	};

	HPARTICLEFFECT		m_pAuraParticleEffect;
	CUtlVector<ubertargeteffects_t> m_hUberTargetEffects;
#else

public:
	DECLARE_DATADESC();

	// Creation.
	static CTFGeneratorUber *Create( const Vector &position, const QAngle &angles, const Vector &velocity,
		const AngularImpulse &angVelocity, CBaseEntity *pOwner, CBaseEntity *pWeapon );
	
	// Overrides.
	virtual void	Spawn();

	void Deflected( CBaseEntity *pDeflectedBy, Vector &vecDir ) override;

	virtual float	GetEffectRadius();

	virtual int		UpdateTransmitState( void );
	virtual int		ShouldTransmit( CCheckTransmitInfo *pInfo );

	virtual void	Activate();			// Activate the radial effect
	virtual void	ActivateThink();
	virtual void	SelfDrainThink();
	virtual void	Fizzle();			// When the Medic dies before activation, fizzle out *poof*

	virtual void	VPhysicsCollision( int index, gamevcollisionevent_t *pEvent );

	bool			HasTouched() const { return m_bTouched; }

	float			GetChargeLevel( void ) const { return m_flChargeLevel; }

	float			GetMinChargeAmount() const { return 1.00f; } // live TF2: used for Vaccinator uber charge chunks

	void			UpdateUberTargets() { m_bUberTargetsParity = !m_bUberTargetsParity; };
	void			CreateGeneratorGibs();
	void			SetModel(const char* pModel);
private:
	void		ApplyEffectInRadius( void );
	void		ApplyEffectToPlayer( CTFPlayer* );

	bool		m_bTouched;
	bool		m_bFizzle;
	float		m_flMinSleepTime;
	float		m_flActivationTime;
	float		m_flForceActivateTime;
	float		m_flChargeLevel;
	bool		m_pOwnerDied;

	// Gibs.
	CUtlVector<breakmodel_t>	m_aGibs;
#endif
};
#endif // TF_WEAPON_GENERATOR_UBER_H