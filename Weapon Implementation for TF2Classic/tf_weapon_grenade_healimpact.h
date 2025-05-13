//====== Copyright © 1996-2005, Valve Corporation, All rights reserved. ========//
//
// Purpose: TF Pipebomb Grenade.
//
//=============================================================================//
#ifndef TF_WEAPON_GRENADE_HEALIMPACT_H
#define TF_WEAPON_GRENADE_HEALIMPACT_H
#ifdef _WIN32
#pragma once
#endif

//#include "tf_weaponbase_grenadeproj.h"
#include "tf_weapon_grenade_pipebomb.h"

// Client specific.
#ifdef CLIENT_DLL
#define CTFGrenadeHealImpactProjectile C_TFGrenadeHealImpactProjectile
#endif

//=============================================================================
//
// TF Pipebomb Grenade
//
class CTFGrenadeHealImpactProjectile : public CTFGrenadePipebombProjectile
{
public:
	DECLARE_CLASS(CTFGrenadeHealImpactProjectile, CTFGrenadePipebombProjectile);
	DECLARE_NETWORKCLASS();

	CTFGrenadeHealImpactProjectile();
	~CTFGrenadeHealImpactProjectile();

	virtual void	Precache() override;

#ifdef CLIENT_DLL
	virtual void	CreateTrails(void);
	virtual void	Simulate(void);

private:
	HPARTICLEFFECT	m_hTimerParticle;
	float			m_flCreationTime;

#else

public:
	DECLARE_DATADESC();
	
	// Creation.
	static CTFGrenadeHealImpactProjectile *Create(const Vector &position, const QAngle &angles, const Vector &velocity,
		const AngularImpulse &angVelocity, CBaseEntity *pOwner, CBaseEntity *pWeapon, int iType );

	// Unique identifier.
	virtual ETFWeaponID	GetWeaponID( void ) const { return TF_WEAPON_HEALLAUNCHER; }

	// Overrides.
	virtual void	Spawn() override;

	//virtual void	BounceSound( void );
	virtual void	Detonate() override;

	virtual void	HealnadeTouch( CBaseEntity *pOther );
	virtual void	VPhysicsCollision( int index, gamevcollisionevent_t *pEvent ) override;
	virtual bool	ShouldExplodeOnEntity( CBaseEntity *pOther ) override;

	virtual void	Explode(trace_t *pTrace, int bitsDamageType, bool bDealDamage = true);

	//virtual Vector	GetBlastForce();

	//virtual int	OnTakeDamage( const CTakeDamageInfo &info );

	//virtual CBaseEntity *GetEnemy( void ) { return m_hEnemy; }

	virtual float	GetHealingAmount() { return m_flHealing; }
	virtual void	SetHealingAmount(float flHealing) { m_flHealing = flHealing; }

private:
	EHANDLE			m_hEnemy;
	bool			m_bDirectHitWasTeammate;
	int				m_iTouched;
	float			m_flHealing;

#endif
};
#endif // TF_WEAPON_GRENADE_HEALIMPACT_H
