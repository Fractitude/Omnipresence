using Sandbox;

namespace Hl2Metrocop;

/// <summary>
/// A player playing as the Metrocop (Warbox): the soldier's play-as (CombineNpc.Pilot.cs) with the
/// cop's own moves on the keys - the baton swung on the attack key, the pistol drawn first, a manhack
/// thrown on the second attack, HL2's shove on use.
/// </summary>
public sealed partial class CombineNpc
{
	partial void OtherPilotStart()
	{
		PreloadManhack();

		// The pistol starts in its holster: out it comes (shown at frame 11, as the AI draws it)
		if ( HasGun && !WeaponDrawn )
		{
			_pilotBusy = PlayAction( "draw_pistol" );
			Invoke( 11f / 30f, () => { if ( IsValid && IsAlive ) WeaponDrawn = true; } );
		}

		// The baton lit, with its sparks (frame 7 of baton_on)
		if ( HasBaton && !BatonOn )
		{
			SetBatonState( true );
			_pilotBusy = PlayAction( "baton_on" );
			MetrocopVoice.Speak( this, "METROPOLICE_ACTIVATE_BATON" );
			Invoke( 7f / 30f, () => { if ( IsValid && IsAlive ) BatonSwitched( true ); } );
		}
	}

	partial void OtherPilotAct( bool fire, bool fire2, bool use, ref bool handled )
	{
		// Use: the shove (frame 11 of push_player)
		if ( use )
		{
			handled = true;
			_pilotBusy = PlayAction( "push_player" );
			Invoke( 11f / 30f, () => { if ( IsValid && IsAlive ) Shove( null ); } );
			return;
		}

		// Second attack: a manhack off the belt (frame 10) and away (frame 33)
		if ( fire2 )
		{
			handled = true;
			if ( !CanDeployManhack ) return;
			MetrocopVoice.Speak( this, "METROPOLICE_DEPLOY_MANHACK" );
			_pilotBusy = PlayAction( "deploy_manhack" );
			Invoke( 10f / 30f, () => { if ( IsValid && IsAlive ) StartDeployManhack(); } );
			Invoke( 33f / 30f, () => { if ( IsValid && IsAlive ) ReleaseManhack(); } );
			return;
		}

		if ( !HasBaton ) return;

		// The baton: a swing on the attack key (it lands at frame 13 of 35), nothing to shoot or reload
		handled = true;
		if ( !fire ) return;
		var duration = PlayAction( "melee" );
		_pilotBusy = duration;
		Invoke( duration * 13f / 35f, () => { if ( IsValid && IsAlive ) BatonHit( null, stepped: true ); } );
	}

	partial void OtherPilotHud( ref string hud )
	{
		hud = HasGun ? $"AMMO\t{Clip}" : "";
		if ( Manhacks > 0 || _manhack.IsValid() ) hud += (hud.Length > 0 ? "\n" : "") + $"MANHACKS\t{Manhacks}";
	}
}
