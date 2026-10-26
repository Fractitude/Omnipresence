using System;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

/// <summary>
/// The manhack on his belt (HL2: the police.mdl "manhack" body group, npc_manhack thrown by the
/// deploy animation). The thrown one is the real Manhack - the published hl2npcs.manhack package,
/// loaded the way the spawn menu loads it - so it fights with its own code, on the Combine side.
/// </summary>
public sealed partial class CombineNpc
{
	private const string ManhackPackage = "hl2npcs.manhack";
	private const string ManhackLocal = "entities/manhack.sent";

	private static Task<GameObject> _manhackPrefab;

	/// <summary>The manhack he threw, while it lives - only one out at a time.</summary>
	private GameObject _manhack;
	private bool _manhackInHand;
	private GameObject _manhackHeld;

	/// <summary>
	/// Removed rather than killed (Z, cleanup, deleted): the manhack he threw goes with him. The sandbox's
	/// own undo list belongs to the sandbox and can't be added to from another package, so it can't
	/// get an undo step of its own - this ties it to his.
	/// </summary>
	partial void OtherBrainRemoved()
	{
		if ( _manhack.IsValid() ) _manhack.Destroy();
		if ( _manhackHeld.IsValid() ) _manhackHeld.Destroy();
		_manhack = null;
	}

	public bool CanDeployManhack =>
		!NoManhackDeploy && Manhacks > 0 && !_manhackInHand && !(_manhack.IsValid() && !IsDeadTarget( _manhack ))
		&& _manhackPrefab is { IsCompletedSuccessfully: true, Result: not null };

	/// <summary>Fetch the Manhack once (from this project if it has one, otherwise its package).</summary>
	public static void PreloadManhack()
	{
		_manhackPrefab ??= LoadManhack();
	}

	private static async Task<GameObject> LoadManhack()
	{
		try
		{
			var entity = ResourceLibrary.Get<ScriptedEntity>( ManhackLocal );
			entity ??= await Cloud.Load<ScriptedEntity>( ManhackPackage, true );
			if ( entity?.Prefab is null ) return null;
			return GameObject.GetPrefab( entity.Prefab.ResourcePath );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Metrocop: couldn't load the Manhack ({e.Message}) - cops won't throw any" );
			return null;
		}
	}

	/// <summary>HL2's AE_METROPOLICE_START_DEPLOY: off the belt and into his left hand.</summary>
	public void StartDeployManhack()
	{
		if ( Manhacks <= 0 || _manhackInHand ) return;
		Manhacks--;
		_manhackInHand = true;
		HoldManhackRpc( true );
	}

	/// <summary>HL2's AE_METROPOLICE_DEPLOY_MANHACK: let go of it, up and a little forward.</summary>
	public void ReleaseManhack()
	{
		if ( !_manhackInHand ) return;
		_manhackInHand = false;
		HoldManhackRpc( false );

		if ( _manhackPrefab is not { IsCompletedSuccessfully: true, Result: { } prefab } ) return;

		var hand = HandPosition( true );
		var go = prefab.Clone( new CloneConfig { Transform = new Transform( hand, WorldRotation ), StartEnabled = true } );
		if ( !go.IsValid() ) return;
		go.NetworkSpawn();
		_manhack = go;

		// Start it with what we know: whom to go for
		var enemy = CopEnemy;
		if ( go.GetComponent<Npc>() is { } npc && enemy.IsValid() )
			npc.SetDisposition( enemy, Disposition.Hostile, priority: 10 );
		if ( enemy.IsValid() ) CombineNet.ReportContact( Scene, enemy );

		if ( go.GetComponent<Rigidbody>() is { } rb )
		{
			var yawOff = WorldRotation.Right * Game.Random.Float( -1f, 1f );
			rb.Velocity = WorldRotation.Forward + yawOff * 16f + Vector3.Up * 250f;
		}
	}

	private void TickManhack()
	{
		// NotifyDeadFriend: "my manhack died"
		if ( _manhack is not null && (!_manhack.IsValid() || IsDeadTarget( _manhack )) )
		{
			_manhack = null;
			MetrocopVoice.Speak( this, "METROPOLICE_MANHACK_KILLED" );
		}

		if ( Manhacks > 0 && !NoManhackDeploy ) PreloadManhack();
	}

	public Vector3 HandPosition( bool left )
	{
		if ( Renderer.IsValid() && Renderer.GetAttachment( left ? "lhand" : "rhand" ) is { } t ) return t.Position;
		return WorldPosition + Vector3.Up * 50f + WorldRotation.Forward * 16f;
	}

	/// <summary>A look-alike of the manhack in his hand while he gets it out (every client draws its own).</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void HoldManhackRpc( bool hold )
	{
		_manhackInHand = hold;
		if ( _manhackHeld.IsValid() ) _manhackHeld.Destroy();
		_manhackHeld = null;
		if ( !hold || _manhackPrefab is not { IsCompletedSuccessfully: true, Result: { } prefab } ) return;

		var source = prefab.GetComponentInChildren<ModelRenderer>( true );
		if ( source?.Model is null ) return;

		var go = new GameObject( true, "manhack in hand" );
		go.NetworkMode = NetworkMode.Never;
		go.SetParent( GameObject, false );
		go.WorldPosition = HandPosition( true );
		go.AddComponent<ModelRenderer>().Model = source.Model;
		go.AddComponent<MetrocopFollowHand>().Cop = this;
		_manhackHeld = go;
	}
}

/// <summary>Keeps the held manhack in the cop's left hand.</summary>
public sealed class MetrocopFollowHand : Component
{
	public CombineNpc Cop { get; set; }

	protected override void OnUpdate()
	{
		if ( !Cop.IsValid() ) return;
		WorldPosition = Cop.HandPosition( true );
		WorldRotation = Cop.WorldRotation;
	}
}

public static class MetrocopEffects
{
	private const string SparksPrefab = "entities/particles/fx_sparks.prefab";

	/// <summary>A short burst of sparks (the baton coming on, g_pEffects->Sparks).</summary>
	public static void Sparks( Scene scene, Vector3 where )
	{
		var fx = GameObject.Clone( SparksPrefab, new Transform( where, Rotation.LookAt( Vector3.Up ) ) );
		if ( !fx.IsValid() ) return;
		fx.NetworkMode = NetworkMode.Never;
		foreach ( var emitter in fx.Components.GetAll<ParticleEmitter>( FindMode.EverythingInSelfAndDescendants ) )
			emitter.Invoke( 0.1f, () => { if ( emitter.IsValid() ) emitter.Enabled = false; } );
		fx.Components.Get<ParticleEffect>( FindMode.EverythingInSelfAndDescendants )?.Invoke( 2f, fx.Destroy );
	}
}

/// <summary>The red flash a baton hit leaves on the victim's screen (UTIL_ScreenFade red, 0.5 s).</summary>
public sealed class MetrocopScreenFlash : Component
{
	public Color Color { get; set; } = new( 0.5f, 0f, 0f, 0.5f );
	public float Duration { get; set; } = 0.5f;
	private TimeSince _started;

	protected override void OnStart() => _started = 0;

	protected override void OnUpdate()
	{
		var camera = Scene.Camera;
		if ( camera is null || _started > Duration )
		{
			Destroy();
			return;
		}

		var alpha = Color.a * (1f - _started / Duration);
		using var painter = camera.BeginHud();
		painter.Fill = Color.WithAlpha( alpha );
		painter.Stroke = Stroke.None;
		painter.Rect( new Rect( 0, 0, Screen.Width, Screen.Height ), 0f );
	}
}
