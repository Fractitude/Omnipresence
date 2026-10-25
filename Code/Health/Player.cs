using Sandbox.Movement;

namespace Sandbox;

/// <summary>
/// The player, ported from Facepunch's sandbox <c>Player</c> and named like it so code written for that game (the sents
/// in <c>Code/Sents/Imported</c>) finds the same members: health, armour, death, the controller and the eyes. The host owns it: damage comes
/// in through <see cref="OnDamage"/> on the host, which drains armour then health, tells everyone, and on death
/// leaves a ragdoll, hands the owner an observer and removes the player until <see cref="GameManager"/> respawns them.
/// </summary>
[Alias( "PlayerHealth" )]
public sealed partial class Player : Component, Component.IDamageable, IKillSource
{
	[RequireComponent] public PlayerController Controller { get; set; }

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )]
	public float Health { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )]
	public float MaxHealth { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )]
	public float Armour { get; set; } = 0;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )]
	public float MaxArmour { get; set; } = 100;

	/// <summary>Seconds of invulnerability after spawning, to stop spawn-killing. 0 disables it.</summary>
	[ConVar( "omni.spawnprotection", ConVarFlags.Replicated | ConVarFlags.Saved,
		Help = "Seconds of invulnerability after spawning. 0 disables spawn protection." )]
	public static float SpawnProtection { get; set; } = 5.0f;

	static Player localPlayer;

	/// <summary>
	/// The player this client controls, if it is alive.
	/// </summary>
	public static Player FindLocalPlayer() => localPlayer;

	/// <summary>
	/// Find a player for this connection
	/// </summary>
	public static Player FindForConnection( Connection c ) => Players.For<Player>( c );

	public bool IsLocalPlayer => !IsProxy;

	/// <summary>
	/// Whether this player is flying through the world. Synced so proxies animate correctly.
	/// </summary>
	[Sync] public bool IsNoclipping { get; internal set; }

	/// <summary>
	/// Whether any player may turn noclip on, not only the host's own. Noclip is the player's own movement, so the host
	/// cannot enforce this on a modified client.
	/// </summary>
	[ConVar( "omni.noclip", ConVarFlags.Replicated | ConVarFlags.Saved,
		Help = "Let every player fly through the world with the Noclip key (the host always may)." )]
	public static bool AllowNoclip { get; set; }

	protected override void OnUpdate()
	{
		if ( Controller.IsValid() && Controller.Renderer.IsValid() )
			Controller.Renderer.Set( "b_noclip", IsNoclipping );

		UpdateFlashlight();

		if ( !IsLocalPlayer )
			return;

		// Getting into a vehicle ends noclip: a seat and a flying body do not go together
		if ( IsNoclipping && IsSeated )
			SetNoclip( false );

		if ( Input.Suppressed || Input.MouseCursorVisible )
			return;

		// not from a seat
		if ( Input.Pressed( "Noclip" ) && !IsSeated )
			ToggleNoclip();
	}

	void ToggleNoclip()
	{
		if ( !AllowNoclip && Connection.Local?.IsHost != true )
			return;

		SetNoclip( !IsNoclipping );
	}

	void SetNoclip( bool on )
	{
		var noclip = GetComponent<NoclipMoveMode>( true ) ?? (on ? AddComponent<NoclipMoveMode>() : null);

		if ( noclip is null )
			return;

		noclip.Enabled = on;
		IsNoclipping = on;
	}

	/// <summary>
	/// The model's object under the player, the one the skinned renderer is on.
	/// </summary>
	public GameObject Body => Controller.IsValid() && Controller.Renderer.IsValid() ? Controller.Renderer.GameObject : null;

	public Transform EyeTransform
	{
		get
		{
			if ( Controller.IsValid() )
				return Controller.EyeTransform;

			Log.Warning( $"Invalid Controller for {GameObject}" );
			return default;
		}
	}

	/// <summary>
	/// The held weapon wants the HUD out of the way (the hand camera does): <see cref="HudSystem"/> fades it out while
	/// this is true and no menu is open.
	/// </summary>
	public bool WantsHideHud => GetComponent<WeaponInventory>()?.ActiveWeapon is { WantsHideHud: true };

	/// <summary>
	/// Find a player by the id of their connection.
	/// </summary>
	public static Player For( Guid playerId ) =>
		Game.ActiveScene.GetAllComponents<Player>().FirstOrDefault( x => x.Network.Owner?.Id == playerId );

	public static T FindLocalWeapon<T>() where T : BaseSandboxWeapon => FindLocalPlayer()?.GetComponentInChildren<T>( true );

	/// <summary>
	/// This player's kills, deaths and god mode, which outlive the body.
	/// </summary>
	public PlayerData PlayerData => PlayerData.For( Network.Owner );

	string IKillSource.DisplayName => Network.Owner?.DisplayName ?? "Unknown";
	long IKillSource.SteamId => (long)(Network.Owner?.SteamId ?? default);

	void IKillSource.OnKill( GameObject victim )
	{
		if ( PlayerData is { } data )
			data.Kills++;
	}

	/// <summary>
	/// Time since this player last spawned. Drives <see cref="IsSpawnProtected"/>.
	/// </summary>
	RealTimeSince timeSinceSpawned;

	/// <summary>
	/// True while the player is briefly invulnerable just after spawning, per the
	/// <c>omni.spawnprotection</c> convar.
	/// </summary>
	public bool IsSpawnProtected => timeSinceSpawned < SpawnProtection;

	SoundHandle damageSound;

	protected override void OnStart()
	{
		timeSinceSpawned = 0;

		if ( IsLocalPlayer )
			localPlayer = this;

		// We don't care about spectating corpses once we spawn
		foreach ( var target in Scene.GetAllComponents<DeathCameraTarget>()
			         .Where( x => x.Connection == Network.Owner )
			         .ToArray() )
		{
			target.GameObject.Destroy();
		}
	}

	protected override void OnDestroy()
	{
		if ( localPlayer == this )
			localPlayer = null;
	}

	public void OnDamage( in DamageInfo dmg )
	{
		if ( !Networking.IsHost ) return;
		if ( Health < 1 ) return;
		if ( IsSpawnProtected ) return;

		var data = GameManager.Current?.GetOrCreatePlayerData( Network.Owner );
		if ( !data.IsValid() ) return;

		// Explosions throw the player even when god mode stops the damage.
		if ( dmg.Tags.Contains( DamageTags.Explosion ) && dmg.Origin != Vector3.Zero )
			ApplyKnockback( GetExplosionKnockback( dmg ) );

		if ( data.IsGodMode ) return;

		//
		// Ignore impact damage from the world, for now
		//
		if ( dmg.Tags.Contains( DamageTags.Impact ) )
		{
			var impactBody = dmg.Attacker?.GetComponentInParent<Rigidbody>();
			if ( !impactBody.IsValid() || !impactBody.MotionEnabled )
				return;

			// Was this fall damage? If so, we can bail out here
			if ( Controller.Velocity.Dot( Vector3.Down ) > 10 )
				return;

			// We were hit by a moving physics object, so lets take that damage.
		}

		// Fire pre-damage event, listeners can modify damage or cancel
		var damageEvent = new PlayerDamageEvent { Player = this, DamageInfo = dmg, Damage = dmg.Damage };
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamaging( damageEvent ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamaging( damageEvent ) );

		if ( damageEvent.Cancelled )
			return;

		var damage = damageEvent.Damage;
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= 2;

		if ( Armour > 0 )
		{
			var remainingDamage = damage - Armour;
			Armour = Math.Max( 0, Armour - damage );
			damage = Math.Max( 0, remainingDamage );
		}

		Health -= damage;

		NotifyOnDamage( new PlayerDamageParams
		{
			Damage = damage,
			Attacker = dmg.Attacker,
			Weapon = dmg.Weapon,
			Tags = dmg.Tags,
			Position = dmg.Position,
			Origin = dmg.Origin,
		} );

		// We didn't die
		if ( Health >= 1 ) return;

		GameManager.Current.OnDeath( this, dmg );

		Health = 0;
		Kill( dmg );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void NotifyOnDamage( PlayerDamageParams args )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamage( args ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamage( this, args ) );

		if ( !IsLocalPlayer )
			return;

		Scene.Camera?.AddShake( (args.Damage * 0.05f).Clamp( 0.5f, 3f ), 40f, 0.4f );

		damageSound?.Stop();

		if ( args.Tags.Contains( DamageTags.Shock ) )
		{
			damageSound = Sound.Play( "damage_taken_shock" );
		}
		else if ( args.Tags.Contains( DamageTags.Fall ) )
		{
			// Only a really hard landing gets the hurt sound, small falls just shake the view
			damageSound = Sound.Play( args.Damage > 40f ? "damage_taken_fall" : "damage_taken_fall_low" );
		}
		else
		{
			damageSound = Sound.Play( "damage_taken_shot" );
		}
	}

	/// <summary>
	/// Launch velocity for a living player caught in a blast: away from the blast origin and upward,
	/// stronger the closer they were.
	/// </summary>
	Vector3 GetExplosionKnockback( in DamageInfo damage )
	{
		var offset = WorldPosition - damage.Origin;
		var strength = MathX.Remap( offset.Length, 0, 512, 900, 300, true );

		return (offset.Normal + Vector3.Up).Normal * strength;
	}

	/// <summary>
	/// Movement is simulated by the owner, so the velocity change has to happen there.
	/// </summary>
	[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
	void ApplyKnockback( Vector3 velocity )
	{
		if ( !Controller.IsValid() || !Controller.Body.IsValid() ) return;

		Controller.PreventGrounding( 0.3f );
		Controller.Body.Velocity += velocity;
	}

	/// <summary>
	/// Broadcasts death to other players
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void NotifyDeath( PlayerDiedParams args )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDied( args ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDied( this, args ) );

		if ( args.Attacker == GameObject )
		{
			Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnSuicide() );
			Global.IPlayerEvents.Post( x => x.OnPlayerSuicide( this ) );
		}
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void Flatline()
	{
		Sound.Play( "sounds/flatline.sound" );
	}

	/// <summary>
	/// Called on the host when a player dies
	/// </summary>
	void Kill( in DamageInfo d )
	{
		// Play the flatline sound on the owner
		if ( IsLocalPlayer )
			Flatline();

		// Let everyone know about the death
		NotifyDeath( new PlayerDiedParams { Attacker = d.Attacker } );

		CreateRagdoll( GetDeathLaunchVelocity( d ), d.Origin );

		// Ghost and say goodbye to the player
		var observer = new GameObject( false, "Observer" );
		observer.Components.Create<PlayerObserver>();
		observer.NetworkSpawn( Network.Owner );

		GameObject.Destroy();
	}

	/// <summary>
	/// Calculates the launch velocity for a ragdoll based on the damage source.
	/// For explosions, uses the direction from the blast origin to the player.
	/// </summary>
	Vector3 GetDeathLaunchVelocity( in DamageInfo damage )
	{
		if ( !damage.Tags.Contains( DamageTags.Explosion ) || damage.Origin == Vector3.Zero )
			return 0;

		var dist = (WorldPosition - damage.Origin).Length;
		var strength = MathX.Remap( dist, 0, 512, 1024, 2048, true );

		var dir = (WorldPosition - damage.Origin).Normal;
		dir += Vector3.Up * 1.0f;
		dir = dir.Normal;

		return dir * strength;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll( Vector3 velocity, Vector3 origin )
	{
		if ( !Controller.Renderer.IsValid() || Controller.Renderer.Model is null )
			return;

		var batch = Scene.BatchGroup();

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( Controller.Renderer );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in Controller.Renderer.GameObject.Children
			         .Where( x => x.Tags.Has( "clothing" ) )
			         .SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( !clothing.IsValid() ) continue;

			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		batch.Dispose();

		physics.CopyBonesFrom( Controller.Renderer, true );

		var corpse = go.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;

		CopyBoneScalesToRagdoll( go );

		ApplyRagdollForce( physics, velocity, origin );
	}

	/// <summary>
	/// Try to inherit transforms from the player onto its new ragdoll
	/// </summary>
	void CopyBoneScalesToRagdoll( GameObject ragdoll )
	{
		// we are only interested in the bones of the player, not anything that may be attached to it.
		var playerRenderer = Controller.Renderer;
		var bones = playerRenderer.Model.Bones;

		var ragdollRenderer = ragdoll.GetComponent<SkinnedModelRenderer>();
		ragdollRenderer.CreateBoneObjects = true;

		var ragdollObjects = ragdoll.GetAllObjects( true ).ToLookup( x => x.Name );

		foreach ( var bone in bones.AllBones )
		{
			var boneName = bone.Name;

			if ( !ragdollObjects.Contains( boneName ) )
				continue;

			var boneObject = playerRenderer.GetBoneObject( boneName );
			if ( !boneObject.IsValid() )
				continue;

			var boneOnRagdoll = ragdollObjects[boneName].FirstOrDefault();

			if ( boneOnRagdoll.IsValid() && boneObject.WorldScale != Vector3.One )
			{
				boneOnRagdoll.Flags = boneOnRagdoll.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				boneOnRagdoll.WorldScale = boneObject.WorldScale;

				var parent = boneOnRagdoll.Parent;
				parent.Flags = parent.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				parent.WorldScale = boneObject.WorldScale;
			}
		}
	}

	static void ApplyRagdollForce( ModelPhysics physics, Vector3 force, Vector3 origin )
	{
		if ( !physics.IsValid() ) return;
		if ( force.Length < 1 ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			rb.ApplyImpulse( Vector3.Direction( origin, rb.WorldPosition ) * force.Length * rb.Mass );
		}
	}

	/// <summary>
	/// Kill yourself
	/// </summary>
	[Rpc.Host]
	internal void KillSelf()
	{
		if ( Rpc.Caller != Network.Owner ) return;

		OnDamage( new DamageInfo( float.MaxValue, GameObject, null ) );
	}
}
