using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// The calm cop (HL2's "gordon_precriminal" part of the game): the player isn't wanted, so he's
/// watched, warned and shoved rather than shot. Three warnings and the baton comes out and the
/// shove becomes a swing; a thing thrown at him gets the thrower chased and hit, then he walks back
/// to his spot. Plus the arrest drill (SF_METROPOLICE_ARREST_ENEMY), and odds and ends HL2 keeps
/// next to them: the "lost him" timer, idle chatter turn-taking, pressing E on him.
/// </summary>
public sealed partial class CombineNpc : Component.IPressable
{
	private int _warnings;
	private int _playerHits;
	private TimeSince _sinceHitPlayer = 100f;
	private bool _playerTooClose;
	private bool _keepFacingPlayer;
	private TimeUntil _chasePlayerUntil;
	private Vector3? _preChaseOrigin;
	private float _preChaseYaw;
	private TimeUntil _nextLostSound;
	private readonly HashSet<GameObject> _criminals = new();

	/// <summary>The player close enough to be watched (128 units, HL2's m_bPlayerIsNear).</summary>
	public bool PlayerNear { get; private set; }
	public GameObject NearPlayer { get; private set; }

	public bool CopWaitingToAnswer { get; set; }
	public void CopResetChatterIfWaiting() => CopWaitingToAnswer = false;

	public bool LostSoundReady => _nextLostSound;
	public void LostSoundSaid() => _nextLostSound = Game.Random.Float( 5f, 15f );

	public int Warnings => _warnings;

	/// <summary>
	/// Is this one a criminal to him? Hostile cops: everyone they hate. Calm cops: not the players -
	/// unless one is being chased for what he did, or has shot at the police (the sandbox lets the
	/// player hold a gun at a calm cop, which HL2 never did).
	/// </summary>
	public bool PlayerIsCriminal( GameObject target )
	{
		if ( !Calm ) return true;
		if ( target is null ) return false;
		if ( !target.GetComponent<PlayerController>().IsValid() ) return true;
		return _criminals.Contains( target ) || _chasePlayerUntil > 0f && !_chasePlayerUntil;
	}

	// ---------------------------------------------------------------- each frame

	private void CalmThink()
	{
		PlayerNear = false;
		NearPlayer = null;
		if ( !Calm ) return;

		var player = Scene.GetAllComponents<PlayerController>()
			.Where( p => p.IsValid() && (p.GetComponent<PlayerHealth>()?.Health ?? 0f) > 0f && !_criminals.Contains( p.GameObject ) )
			.OrderBy( p => p.WorldPosition.Distance( WorldPosition ) )
			.FirstOrDefault();

		if ( player is not null && (player.WorldPosition + Vector3.Up * 36f).Distance( WorldPosition + Vector3.Up * 36f ) < 128f )
		{
			PlayerNear = true;
			NearPlayer = player.GameObject;
			Animation?.AddLookTarget( player.GameObject, 5f );

			// Warned three times already: the baton comes on whenever he's close
			if ( _warnings >= MaxWarnings ) SetBatonState( true );
		}
		else if ( _batonDebounce )
		{
			if ( !CopEnemy.IsValid() ) SetBatonState( false );
			_keepFacingPlayer = false;
		}

		if ( player is null ) return;
		var distance = player.WorldPosition.Distance( WorldPosition );

		// Standing on his head: always beaten. Pressed up against him, in sight: shoved -
		// but not a fourth time in a row, unless he's a cop who hasn't moved off his spot
		if ( player.GroundObject?.Root == GameObject )
		{
			_playerHits = 0;
			_playerTooClose = true;
		}
		else if ( distance < 42f && CanSeePlayer( player.GameObject ) )
		{
			if ( _playerHits < 3 || _preChaseOrigin is null ) _playerTooClose = true;
		}
		else
		{
			// Don't forget the hits for a moment: two cops shouldn't pin the player between them
			if ( _sinceHitPlayer > 3f ) _playerHits = 0;
			_playerTooClose = false;
		}

		// Something the player holds with the physgun bumped into him
		if ( HeldThingTouching( player.GameObject ) ) _playerTooClose = true;
	}

	private bool CanSeePlayer( GameObject player )
	{
		var tr = Scene.Trace.Ray( EyePosition, Sandbox.Npcs.Layers.AnimationLayer.GetEyePosition( player ) )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "playercontroller" )
			.Run();
		return !tr.Hit || tr.GameObject?.Root == player.Root;
	}

	private bool HeldThingTouching( GameObject player )
	{
		foreach ( var gun in Scene.GetAllComponents<Physgun>() )
		{
			var state = gun._state;
			if ( !state.Active || !state.GameObject.IsValid() || gun.GameObject.Root != player.Root ) continue;
			var held = state.GameObject.Root;
			var bounds = held.GetBounds();
			if ( bounds.Grow( 6f ).Overlaps( new BBox( WorldPosition + new Vector3( -16, -16, 0 ), WorldPosition + new Vector3( 16, 16, 72 ) ) ) )
				return true;
		}

		return false;
	}

	// ---------------------------------------------------------------- choosing

	/// <summary>HL2's precriminal branches of SelectSchedule, in its order.</summary>
	private ScheduleBase CalmSelect()
	{
		if ( !Calm || !NearPlayerOrRemembered( out var player ) ) return null;
		if ( PlayerIsCriminal( player ) ) return null;

		if ( _playerTooClose )
		{
			// Don't hit the player too many times in a row, unless he's pushing a cop who hasn't moved
			if ( _playerHits < 3 || _preChaseOrigin is null )
			{
				_playerTooClose = false;
				IncrementPlayerCriminalStatus( player );
				_chasePlayerUntil = 0f;
				var shove = GetSchedule<CopShoveSchedule>();
				shove.Target = player;
				shove.Configure();
				return shove;
			}
		}
		else if ( _playerHits > 0 && ReturnToPreChase() is { } back )
			return back;

		// Watch him like a hawk once he's been warned
		if ( _keepFacingPlayer )
		{
			var face = GetSchedule<CopTargetFaceSchedule>();
			face.Target = player;
			face.Configure();
			return face;
		}

		return ReturnToPreChase();
	}

	private bool NearPlayerOrRemembered( out GameObject player )
	{
		player = NearPlayer ?? _lastWarned;
		return player.IsValid();
	}

	private GameObject _lastWarned;

	private ScheduleBase ReturnToPreChase()
	{
		if ( _preChaseOrigin is null || CopEnemy.IsValid() || _chasePlayerUntil > 0f && !_chasePlayerUntil ) return null;
		var back = GetSchedule<CopReturnToPreChaseSchedule>();
		back.Spot = _preChaseOrigin.Value;
		back.Yaw = _preChaseYaw;
		return back;
	}

	public void ClearPreChase()
	{
		_preChaseOrigin = null;
		_preChaseYaw = 0f;
	}

	/// <summary>IncrementPlayerCriminalStatus: another warning ("back up"), the baton out from the second.</summary>
	public void IncrementPlayerCriminalStatus( GameObject player )
	{
		_lastWarned = player;
		Animation?.AddLookTarget( player, 5f );

		if ( _warnings < MaxWarnings ) _warnings++;
		if ( _warnings >= MaxWarnings - 1 ) SetBatonState( true );
		_batonDebounce = Game.Random.Float( 2f, 4f );

		var line = Game.Random.FromArray( new[] { "METROPOLICE_BACK_UP_A", "METROPOLICE_BACK_UP_B", "METROPOLICE_BACK_UP_C" } );
		MetrocopVoice.Speak( this, line, MetrocopVoice.Medium );
		_keepFacingPlayer = true;
	}

	/// <summary>The shove (or, after three warnings, a real swing) landed on a player.</summary>
	public void StunnedTarget( GameObject target )
	{
		_sinceHitPlayer = 0;
		if ( target.IsValid() && target.GetComponent<PlayerController>().IsValid() ) _playerHits++;
	}

	/// <summary>A calm cop hurt by a player: a thrown thing is "pick up that can"; a bullet makes him a criminal for good.</summary>
	private void CalmHurtByPlayer( GameObject player )
	{
		MetrocopVoice.Speak( this, "METROPOLICE_HIT_BY_PHYSOBJECT", MetrocopVoice.Always );
		_warnings = MaxWarnings;
		AdministerJustice( player );
	}

	/// <summary>A prop hit him: whoever threw it - the nearest player who can see him - is to blame.</summary>
	public void HitByThrownThing()
	{
		if ( !Calm ) return;
		var thrower = Scene.GetAllComponents<PlayerController>()
			.Where( p => p.IsValid() && p.WorldPosition.Distance( WorldPosition ) < 1500f && CanSeePlayer( p.GameObject ) )
			.OrderBy( p => p.WorldPosition.Distance( WorldPosition ) )
			.FirstOrDefault();
		if ( thrower is not null ) CalmHurtByPlayer( thrower.GameObject );
	}

	/// <summary>Shot at by a player: HL2 never let it happen; here he's simply a criminal to this cop from then on.</summary>
	public void ShotByPlayer( GameObject player )
	{
		if ( !Calm || _criminals.Contains( player ) ) return;
		_criminals.Add( player );
		SetBatonState( true );
	}

	/// <summary>
	/// Chase after a player who's just pissed us off, and hit him (HL2's AdministerJustice).
	/// Not allowed to: watch him, and send the nearest cop who is.
	/// </summary>
	public void AdministerJustice( GameObject player )
	{
		if ( AllowedToRespond )
		{
			if ( _preChaseOrigin is null )
			{
				_preChaseOrigin = WorldPosition;
				_preChaseYaw = WorldRotation.Yaw();
			}

			_chasePlayerUntil = Game.Random.Float( 3f, 7f );
			Squad?.Sighted( this, player, player.WorldPosition );
			SetBatonState( true );
			EndCurrentSchedule();
			return;
		}

		_keepFacingPlayer = true;
		var helper = Scene.GetAllComponents<CombineNpc>()
			.Where( c => c != this && c.IsAlive && c.AllowedToRespond && c.WorldPosition.Distance( WorldPosition ) < 512f && c.CanSeePlayer( player ) )
			.FirstOrDefault();
		helper?.AdministerJustice( player );
	}

	// ---------------------------------------------------------------- pressing E on him (PrecriminalUse)

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e ) => null;

	bool IPressable.CanPress( IPressable.Event e ) => Calm && IsAlive && !CopEnemy.IsValid();

	bool IPressable.Press( IPressable.Event e )
	{
		var player = e.Source?.GameObject?.Root;
		if ( !player.IsValid() || PlayerIsCriminal( player ) ) return false;
		PressedBy( player );
		return true;
	}

	[Rpc.Host]
	private void PressedBy( GameObject player )
	{
		if ( !player.IsValid() || CopEnemy.IsValid() ) return;

		// Treat it like the player's bothered the cop; the third time, he's had enough
		IncrementPlayerCriminalStatus( player );
		if ( _warnings == MaxWarnings ) AdministerJustice( player );
	}

	// ---------------------------------------------------------------- arrest (SF_METROPOLICE_ARREST_ENEMY)

	/// <summary>He's resisting: nobody in the squad is arresting any more, they all open fire.</summary>
	public void EnemyResistingArrest()
	{
		foreach ( var mate in Squad?.Alive.ToList() ?? new List<CombineNpc> { this } )
		{
			if ( !mate.ArrestEnemy ) continue;
			mate.ArrestEnemy = false;
			if ( mate.ActiveSchedule is CopArrestSchedule or CopWarnAndArrestSchedule ) mate.EndCurrentSchedule();
		}
	}
}
