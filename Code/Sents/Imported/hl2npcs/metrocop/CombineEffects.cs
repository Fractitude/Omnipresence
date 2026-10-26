using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Hl2Metrocop;

/// <summary>A short streak of light flying from the muzzle to the impact, like HL2's AR2 tracers.</summary>
public sealed class CombineTracer : Component
{
	private const float Speed = 7000f;
	private const float Length = 160f;

	public Vector3 From { get; set; }
	public Vector3 To { get; set; }

	private LineRenderer _line;
	private float _travelled;

	public Color Color { get; set; } = Color.White;

	private static readonly Dictionary<string, Sprite> Sprites = new();

	public static Sprite LoadSprite( string name )
	{
		if ( !Sprites.TryGetValue( name, out var sprite ) )
		{
			var texture = EffectTexture( $"hl2_metrocop/fx/{name}.vmat" );
			sprite = texture is null ? null : Sprite.FromTexture( texture );
			Sprites[name] = sprite;
		}

		return sprite;
	}

	/// <summary>
	/// An effect texture. Each one sits in its own material: a bare png isn't a compiled asset,
	/// so it would never be published with the soldier.
	/// </summary>
	public static Texture EffectTexture( string material )
	{
		var m = Material.Load( material );
		return m?.GetTexture( "g_tColor" ) ?? m?.FirstTexture;
	}

	public static void Create( Vector3 from, Vector3 to, Color color )
	{
		var go = new GameObject( true, "tracer" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.WorldPosition = from;

		var tracer = go.AddComponent<CombineTracer>();
		tracer.From = from;
		tracer.To = to;
		tracer.Color = color;
	}

	/// <summary>HL2-style flash: two random flash sprites at the muzzle and a brief light.</summary>
	public static void MuzzleFlash( Vector3 position, Vector3 direction, CombineWeaponStats gun )
	{
		var go = new GameObject( true, "muzzle flash" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.WorldPosition = position;

		for ( var i = 0; i < 2; i++ )
		{
			var flash = LoadSprite( Game.Random.FromArray( gun.Flashes ) );
			if ( flash is null ) continue;

			var child = new GameObject( go, true, "sprite" );
			child.WorldPosition = position + direction * (i * gun.FlashSize * 0.4f);

			var sprite = child.AddComponent<SpriteRenderer>();
			sprite.Sprite = flash;
			sprite.Billboard = SpriteRenderer.BillboardMode.Always;
			sprite.Additive = true;
			sprite.Lighting = false;
			sprite.Shadows = false;
			var size = gun.FlashSize * Game.Random.Float( 0.8f, 1.2f ) * (i == 0 ? 1f : 0.7f);
			sprite.Size = new Vector2( size, size );
		}

		var light = go.AddComponent<PointLight>();
		light.LightColor = gun.LightColor * 0.9f;
		light.Radius = 90f;
		light.Shadows = false;

		go.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 0.05f;
	}

	protected override void OnStart()
	{
		_line = AddComponent<LineRenderer>();
		_line.UseVectorPoints = true;
		_line.Face = SceneLineObject.FaceMode.Camera;
		_line.Lighting = false;
		_line.Opaque = false;
		_line.Additive = true;
		_line.CastShadows = false;
		_line.Color = new Gradient( new Gradient.ColorFrame( 0, Color.WithAlpha( 0f ) ), new Gradient.ColorFrame( 1, Color.WithAlpha( 0.55f ) ) );
		_line.Width = 0.9f;
		_line.VectorPoints = new List<Vector3> { From, From };
	}

	protected override void OnUpdate()
	{
		var total = From.Distance( To );
		_travelled += Speed * Time.Delta;

		var head = MathF.Min( _travelled, total );
		var tail = MathF.Max( 0f, _travelled - Length );
		if ( tail >= total )
		{
			GameObject.Destroy();
			return;
		}

		var direction = (To - From).Normal;
		_line.VectorPoints = new List<Vector3> { From + direction * tail, From + direction * head };
	}
}

/// <summary>The blinking red light and ticking of a live Combine grenade.</summary>
public sealed class CombineGrenadeBeacon : Component
{
	[Property] public SoundEvent TickSound { get; set; }

	private PointLight _light;
	private SpriteRenderer _glow;
	private LineRenderer _trail;
	private readonly List<(Vector3 Position, float Time)> _trailPoints = new();
	private TimeSince _alive;
	private TimeSince _sinceTick = 10f;

	protected override void OnStart()
	{
		_alive = 0;

		var go = new GameObject( GameObject, true, "beacon" );
		go.Flags |= GameObjectFlags.NotNetworked;
		go.LocalPosition = Vector3.Up * 2f;

		_light = go.AddComponent<PointLight>();
		_light.LightColor = new Color( 1f, 0.15f, 0.1f ) * 1.2f;
		_light.Radius = 36f;
		_light.Shadows = false;

		_glow = go.AddComponent<SpriteRenderer>();
		_glow.Sprite = CombineTracer.LoadSprite( "redglow1" );
		_glow.Billboard = SpriteRenderer.BillboardMode.Always;
		_glow.Additive = true;
		_glow.Lighting = false;
		_glow.Shadows = false;
		_glow.Size = new Vector2( 10f, 10f );
		_glow.Color = Color.White.WithAlpha( 0.8f );

		// Short red trail, like the frag grenade's sprite trail in HL2
		var trail = new GameObject( true, "trail" );
		trail.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		_trail = trail.AddComponent<LineRenderer>();
		_trail.UseVectorPoints = true;
		_trail.Lighting = false;
		_trail.Opaque = false;
		_trail.Additive = true;
		_trail.CastShadows = false;
		var trailColor = new Color( 1f, 0.35f, 0.28f );
		_trail.Color = new Gradient( new Gradient.ColorFrame( 0, trailColor.WithAlpha( 0f ) ), new Gradient.ColorFrame( 1, trailColor.WithAlpha( 0.4f ) ) );
		_trail.Width = new Curve( new Curve.Frame( 0, 0.4f ), new Curve.Frame( 1, 1.6f ) );
	}

	protected override void OnDestroy()
	{
		if ( _trail.IsValid() ) _trail.GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		var interval = _alive > 2f ? 0.2f : 0.45f;
		if ( _sinceTick > interval )
		{
			_sinceTick = 0;
			if ( TickSound is not null ) Sound.Play( TickSound, WorldPosition );
		}

		if ( _trail.IsValid() )
		{
			var now = Time.Now;
			_trailPoints.Add( (WorldPosition, now) );
			_trailPoints.RemoveAll( p => now - p.Time > 0.35f );
			_trail.VectorPoints = _trailPoints.ConvertAll( p => p.Position );
			_trail.Enabled = _trailPoints.Count > 1 && _trailPoints[0].Position.Distance( WorldPosition ) > 4f;
		}

		var lit = _sinceTick < 0.08f;
		if ( _light.IsValid() ) _light.Enabled = lit;
		if ( _glow.IsValid() ) _glow.Enabled = lit;
	}
}

/// <summary>Lets explosions throw the corpse around.</summary>
public sealed class CombineRagdollBlast : Component, Component.IDamageable
{
	public ModelPhysics Physics { get; set; }

	public void OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || !Physics.IsValid() ) return;
		if ( !damage.Tags.Contains( DamageTags.Explosion ) || damage.Origin == Vector3.Zero ) return;

		var distance = WorldPosition.Distance( damage.Origin );
		var strength = MathX.Remap( distance, 0f, 512f, 1200f, 300f ).Clamp( 300f, 1200f );

		foreach ( var b in Physics.Bodies )
		{
			var rb = b.Component;
			if ( !rb.IsValid() ) continue;

			var direction = (rb.WorldPosition - damage.Origin).Normal + Vector3.Up * 0.6f;
			rb.ApplyImpulse( direction.Normal * strength * rb.Mass );
		}
	}
}
