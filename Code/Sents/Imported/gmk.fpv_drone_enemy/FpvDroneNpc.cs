using Sandbox;
using Sandbox.Navigation;
using Sandbox.Npcs;
using Sandbox.Npcs.Layers;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// FPV-дрон-камикадзе с ИИ. Один класс — две версии (галка Friendly). Ведут себя одинаково, как Army Guy и
/// Army Guy Friendly, различаются только тем, кого считают врагами:
/// • без цели патрулирует над местом появления, летит проверить выстрелы и взрывы;
/// • заметил врага — секунду соображает, потом пикирует (если до врага свободно — хоть в окно). На последних
///   метрах уже не доворачивает: отскок вбок спасает, дрон выходит из пике и заходит снова;
/// • враг спрятался — ищет дорогу по навмешу на высоте над полом и залетает в дом через двери; помнит, где видел,
///   и слышит сквозь стены.
/// Вражеский охотится на игроков, их союзников и мирных; дружеский — на врагов, монстров и вражеские дроны,
/// своих его взрыв не трогает. Думает только хост; клиенты крутят винты и играют звук.
/// </summary>
public sealed class FpvDroneNpc : Npc, Component.ICollisionListener
{
	/// <summary>Дружеский (за игроков) или вражеский.</summary>
	[Property] public bool Friendly { get; set; }

	// ---------- Полёт ----------
	/// <summary>Скорость в пути, юнитов/с.</summary>
	[Property, Group( "Flight" )] public float CruiseSpeed { get; set; } = 380f;
	/// <summary>Скорость в пике на цель.</summary>
	[Property, Group( "Flight" )] public float DiveSpeed { get; set; } = 800f;
	/// <summary>Какое ускорение дрон себе «заказывает», юнитов/с². Реально выходит меньше — пока не наклонится.</summary>
	[Property, Group( "Flight" )] public float Acceleration { get; set; } = 1000f;
	/// <summary>Наибольший наклон корпуса в пути, градусы. Чем больше наклон — тем сильнее тяга вбок.</summary>
	[Property, Group( "Flight" )] public float MaxTilt { get; set; } = 35f;
	/// <summary>Наибольший наклон в пике. С ним дрон упирается в DiveSpeed — дальше не пускает воздух.</summary>
	[Property, Group( "Flight" )] public float DiveTilt { get; set; } = 60f;
	/// <summary>Как быстро корпус наклоняется, градусов в секунду (рука пилота на стиках).</summary>
	[Property, Group( "Flight" )] public float TiltRate { get; set; } = 220f;
	/// <summary>Как быстро разворачивает нос, градусов в секунду.</summary>
	[Property, Group( "Flight" )] public float YawRate { get; set; } = 120f;
	/// <summary>На какой высоте над полом лететь по маршруту (в двери и коридоры пролезает).</summary>
	[Property, Group( "Flight" )] public float CruiseHeight { get; set; } = 64f;
	/// <summary>Высота патруля над землёй.</summary>
	[Property, Group( "Flight" )] public float PatrolHeight { get; set; } = 200f;
	/// <summary>Как далеко от места появления патрулирует.</summary>
	[Property, Group( "Flight" )] public float PatrolRadius { get; set; } = 900f;

	// ---------- Бой ----------
	/// <summary>Видит цель на таком расстоянии, если ничего не загораживает.</summary>
	[Property, Group( "Combat" )] public float SightRange { get; set; } = 3500f;
	/// <summary>Слышит цель сквозь стены на таком расстоянии — так находит спрятавшихся в доме.</summary>
	[Property, Group( "Combat" )] public float HearingRange { get; set; } = 1000f;
	/// <summary>С какого расстояния пикирует, если до цели прямой свободный путь.</summary>
	[Property, Group( "Combat" )] public float DiveRange { get; set; } = 1000f;
	/// <summary>Неконтактный взрыватель: рвётся, подлетев к цели на это расстояние.</summary>
	[Property, Group( "Combat" )] public float TriggerDistance { get; set; } = 52f;
	[Property, Group( "Combat" )] public float BlastRadius { get; set; } = 230f;
	/// <summary>Урон в центре взрыва; к краю падает до нуля, за стеной — вчетверо слабее.</summary>
	[Property, Group( "Combat" )] public float BlastDamage { get; set; } = 125f;
	[Property, Group( "Combat" )] public float BlastForce { get; set; } = 900f;
	/// <summary>Сколько секунд дрон «соображает», заметив цель, прежде чем атаковать.</summary>
	[Property, Group( "Combat" )] public float ReactionTime { get; set; } = 0.8f;
	/// <summary>Ближе этого к цели уже не доворачивает — летит туда, где цель была (у манер ниже — ближе).</summary>
	[Property, Group( "Combat" )] public float CommitDistance { get; set; } = 280f;
	/// <summary>Промахнулся, но пролетает ближе этого — оператор всё равно жмёт подрыв в момент наибольшего сближения.</summary>
	[Property, Group( "Combat" )] public float MissFuseDistance { get; set; } = 150f;

	public enum AttackStyle { Random, Direct, TopAttack, Stalker, LowSkim }

	/// <summary>
	/// Манера атаки. Direct — прямо в пике. TopAttack — набирает высоту над целью и падает сверху почти отвесно.
	/// Stalker — кружит на расстоянии и бьёт, когда цель встала или отвернулась. LowSkim — подходит низко,
	/// на высоте груди, и бьёт с ходу. Random — каждый дрон выбирает свою и иногда меняет после промаха.
	/// </summary>
	[Property, Group( "Combat" )] public AttackStyle Style { get; set; } = AttackStyle.Random;

	// ---------- Эффекты ----------
	[Property, Group( "Effects" )] public SoundEvent FlightSound { get; set; }
	[Property, Group( "Effects" )] public float PropellerSpeed { get; set; } = 3000f;

	public enum DroneMode { Idle, Patrol, Investigate, Hunt, Attack }

	/// <summary>Что дрон делает сейчас (решает хост).</summary>
	[Sync( SyncFlags.FromHost )] public DroneMode Mode { get; private set; }

	private const float BodyRadius = 12f;      // сфера, которой проверяем, пролезет ли дрон
	private const float MemoryTime = 8f;       // столько помнит, где видел цель
	private static readonly string[] NotObstacles = { "trigger", "drone" };
	private static readonly string[] NotFloor = { "trigger", "drone", "player", "npc" };

	private Rigidbody _rb;
	private bool _exploded;
	private Vector3 _home;
	private Vector3 _holdPos;

	// цель
	private GameObject _target;
	private Vector3 _targetPos;            // где цель (точка прицеливания); если не видно — где была
	private Vector3 _targetVel;
	private bool _targetVisible;
	private TimeSince _sinceSeen;
	private TimeSince _sincePerceive;

	// без цели
	private Vector3? _investigate;
	private TimeSince _sinceInvestigate;
	private bool _arrived;
	private TimeSince _sinceArrived;
	private Vector3? _wanderPoint;
	private TimeUntil _wanderWait;

	// маршрут по навмешу (точки уже подняты над полом)
	private readonly List<Vector3> _path = new();
	private int _pathIndex;
	private Vector3 _pathGoal;
	private TimeSince _sincePath;
	private TimeSince _sincePathSkip;

	// застревание
	private Vector3 _progressPos;
	private TimeSince _sinceProgress;
	private TimeUntil _unstick;
	private Vector3 _unstickDir;

	// атака
	private TimeSince _sinceAcquired;      // когда заметили цель: первые ReactionTime секунд только разворачиваемся
	private Vector3? _commit;              // последние метры пике — летим в эту точку, не доворачивая
	private TimeUntil _recover;            // промахнулись — выходим из пике, потом новый заход
	private float _closest;                // ближе всего к цели на последних метрах пике
	private AttackStyle _style;            // манера этого дрона
	private bool _perched;                 // TopAttack: высоту над целью набрал
	private float _orbit;                  // Stalker: где на круге вокруг цели
	private TimeSince _sinceStalk;         // Stalker: сколько уже кружит

	// схватили физганом
	private bool _held;
	private TimeSince _sinceHeldCheck;

	// курс носа (меняется не быстрее YawRate)
	private float _yaw;

	// визуал (у всех)
	private SoundHandle _sound;
	private readonly List<GameObject> _props = new();
	private Vector3 _speedSamplePos;
	private float _speedSampleTime;
	private float _visualSpeed;

	// ================= Фракции =================

	public override string Faction => Friendly ? Factions.Ally : Factions.Enemy;

	protected override void SetupRelationships()
	{
		if ( Friendly )
		{
			Likes( Factions.Player, Factions.Citizen );
			Hates( Factions.Enemy, Factions.Monster );
		}
		else
		{
			Hates( Factions.Player, Factions.Ally, Factions.Citizen );   // все на равных, как у Army Guy
		}
	}

	// ================= Жизненный цикл =================

	protected override void OnStart()
	{
		base.OnStart();
		GameObject.Tags.Add( "drone" );

		_rb = GetComponent<Rigidbody>();
		_home = WorldPosition;
		_holdPos = WorldPosition;
		_yaw = WorldRotation.Angles().yaw;
		_style = Style == AttackStyle.Random ? RandomStyle() : Style;

		// Слой чувств песочницы смотрит «глазами» на высоте человека — дрону это не подходит, видим сами (Perceive).
		// От слоя берём только стимулы (выстрелы, взрывы, смерти); дальность ему не нужна — пусть не сканирует зря.
		if ( Senses.IsValid() )
		{
			Senses.SightRange = 1f;
			Senses.HearingRange = 1f;
		}

		foreach ( var child in GameObject.Children )
			if ( child.Name.StartsWith( "prop" ) ) _props.Add( child );


		_speedSamplePos = WorldPosition;
		_speedSampleTime = Time.Now;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if ( _sound.IsValid() ) _sound.Stop();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdateVisuals();

		if ( ShowDebugOverlay && !IsProxy )
		{
			if ( _path.Count > 1 ) DebugOverlay.Line( _path, Color.Yellow );
			if ( _target.IsValid() ) DebugOverlay.Line( WorldPosition, _targetPos, _targetVisible ? Color.Red : new Color( 1f, 0.5f, 0f ) );
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( IsProxy || _exploded || !_rb.IsValid() || !_rb.MotionEnabled ) return;

		// Держат физганом или привязали — моторы молчат, пусть несут
		if ( _sinceHeldCheck > 0.1f )
		{
			_sinceHeldCheck = 0;
			_held = HeldByPhysgun() || _rb.Joints.Count > 0;
		}
		if ( _held )
		{
			_holdPos = WorldPosition;
			_yaw = WorldRotation.Angles().yaw;
			return;
		}

		// ИИ песочницы выключен (sandbox.ai.enabled 0) — просто висим на месте
		if ( !NpcConVars.Enabled )
		{
			Mode = DroneMode.Idle;
			Fly( Hold( _holdPos ), _rb.WorldRotation.Forward, false );
			return;
		}

		if ( _sincePerceive > 0.3f )
		{
			_sincePerceive = 0;
			Perceive();
		}

		var wish = Think( out var face, out var diving );
		if ( _exploded ) return;

		CheckStuck( wish );
		if ( !_unstick && !diving ) wish = _unstickDir * CruiseSpeed;

		Fly( wish, face, diving );
		_holdPos = WorldPosition;
	}

	// ================= Восприятие =================

	/// <summary>
	/// Раз в 0.3 с: кого видим (луч от дрона до цели) и кого слышим (ближе HearingRange, хоть за стеной).
	/// Все враги равны (игрок не важнее NPC): видимые важнее, ближние важнее, текущую цель не бросаем зря.
	/// </summary>
	private void Perceive()
	{
		// выстрел, взрыв, смерть рядом — летим проверить (как солдаты песочницы)
		if ( !_target.IsValid() && Senses.IsValid() && Senses.Disturbance is { } stimulus )
		{
			if ( _investigate is null || _sinceInvestigate > 3f )
				StartInvestigate( stimulus.Position );
		}

		if ( NpcConVars.NoTarget )
		{
			LoseTarget( false );
			return;
		}

		var pos = WorldPosition;
		GameObject best = null;
		var bestScore = float.MinValue;
		var bestVisible = false;
		var bestAim = Vector3.Zero;

		void Consider( GameObject go )
		{
			if ( !go.IsValid() || go == GameObject ) return;
			if ( GetDisposition( go ) != Disposition.Hostile ) return;
			if ( !IsAlive( go ) ) return;

			var aim = AimPoint( go );
			var dist = pos.Distance( aim );
			if ( dist > SightRange && dist > HearingRange ) return;

			var visible = dist <= SightRange && CanSee( pos, aim, go );
			if ( !visible && dist > HearingRange ) return;

			var score = GetRelationship( go ).Priority * 5000f - dist;
			if ( visible ) score += 1500f;
			if ( go == _target ) score += 400f;
			if ( score <= bestScore ) return;

			best = go;
			bestScore = score;
			bestVisible = visible;
			bestAim = aim;
		}

		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
			Consider( player.GameObject );

		var npcs = Scene.GetSystem<NpcSystem>();
		if ( npcs is not null )
			foreach ( var npc in npcs.Npcs )
				if ( npc != this ) Consider( npc.GameObject );

		if ( best.IsValid() )
		{
			if ( best != _target ) NewTarget();
			_target = best;
			_targetPos = bestAim;
			_targetVel = VelocityOf( best );
			_targetVisible = bestVisible;
			_sinceSeen = 0;
			_investigate = null;
		}
		else if ( _target.IsValid() )
		{
			_targetVisible = false;   // помним, где была; Think забудет через MemoryTime
		}
	}

	private void LoseTarget( bool search = true )
	{
		if ( _target is null ) return;
		var last = _targetPos;
		_target = null;
		_targetVisible = false;
		_path.Clear();
		_commit = null;
		if ( search ) StartInvestigate( last );
	}

	private void NewTarget()
	{
		_path.Clear();
		_sinceAcquired = 0;
		_commit = null;
		NewApproach();
	}

	/// <summary>Новый заход: TopAttack снова набирает высоту, Stalker снова выжидает.</summary>
	private void NewApproach()
	{
		_perched = false;
		_sinceStalk = 0;
		var from = ( WorldPosition - _targetPos ).WithZ( 0f );
		_orbit = from.Length > 1f ? Rotation.LookAt( from.Normal ).Angles().yaw : 0f;
	}

	private static AttackStyle RandomStyle() => (AttackStyle)Random.Shared.Int( 1, 4 );

	private void StartInvestigate( Vector3 where )
	{
		var floor = FloorBelow( where ) ?? where;
		_investigate = LiftTo( floor, 110f );
		_sinceInvestigate = 0;
		_arrived = false;
		_path.Clear();
	}

	private bool HeldByPhysgun()
	{
		foreach ( var gun in Scene.GetAllComponents<Physgun>() )
		{
			var state = gun._state;
			if ( state.Active && state.GameObject.IsValid() && state.GameObject.Root == GameObject ) return true;
		}
		return false;
	}

	// ================= Решение: куда лететь =================

	/// <summary>Желаемая скорость на этот такт. face — куда смотреть носом, diving — идём в пике.</summary>
	private Vector3 Think( out Vector3 face, out bool diving )
	{
		diving = false;
		var pos = WorldPosition;
		face = _rb.Velocity;

		if ( _target is not null && ( !_target.IsValid() || !IsAlive( _target ) || _sinceSeen > MemoryTime ) )
			LoseTarget();

		if ( _target.IsValid() )
		{
			// цель на виду — следим за ней каждый такт, а не раз в 0.3 с
			if ( _targetVisible ) _targetPos = AimPoint( _target );

			if ( _targetVisible && NearTarget( pos ) )
			{
				Detonate();
				return Vector3.Zero;
			}

			// последние метры пике: летим в запомненную точку, не доворачивая. Промах удар не отменяет: в момент
			// наибольшего сближения оператор жмёт подрыв, если цель ближе MissFuseDistance. Ушёл дальше — новый заход.
			if ( _commit is { } point )
			{
				var to = point - pos;
				var gap = pos.Distance( _targetPos );
				var passed = to.Length < 24f || to.Dot( _rb.Velocity ) < 0f;
				if ( ( passed || gap > _closest + 4f ) && _closest < MissFuseDistance )
				{
					Detonate();
					return Vector3.Zero;
				}
				_closest = MathF.Min( _closest, gap );

				if ( passed )
				{
					_commit = null;
					_recover = 1.2f;
					if ( Style == AttackStyle.Random && Random.Shared.Float( 0f, 1f ) < 0.35f ) _style = RandomStyle();
					NewApproach();
				}
				else
				{
					Mode = DroneMode.Attack;
					diving = true;
					face = to;
					return to.Normal * DiveSpeed;
				}
			}

			// промах: по инерции уходим вперёд и вверх, потом новый заход
			if ( !_recover )
			{
				Mode = DroneMode.Hunt;
				var ahead = _rb.Velocity.WithZ( 0f );
				face = ahead;
				return Steer( ahead.ClampLength( CruiseSpeed ) + Vector3.Up * CruiseSpeed * 0.6f );
			}

			var aim = _targetVisible ? Lead( pos ) : _targetPos;
			var dist = pos.Distance( aim );
			var open = _targetVisible && CanFly( pos, aim, _target );

			// только что заметили — тормозим и разворачиваемся к цели
			if ( _sinceAcquired < ReactionTime )
			{
				Mode = DroneMode.Hunt;
				face = aim - pos;
				return Steer( Hold( pos ) );
			}

			if ( open && dist < DiveRange )
				return AttackRun( pos, aim, dist, out face, out diving );

			Mode = DroneMode.Hunt;
			Vector3 v;
			if ( open )
			{
				// далеко, но на виду: подходим чуть выше цели, чтобы потом пикировать сверху
				v = TravelTo( aim + Vector3.Up * MathF.Min( 220f, dist * 0.2f ), CruiseSpeed * 1.3f, _target );
			}
			else
			{
				// не видно или напрямую не пролезть — по маршруту туда, где цель (или где была)
				v = TravelTo( _targetPos, CruiseSpeed * 1.15f, _target );
			}
			face = v;
			return Steer( v, _target );
		}

		if ( _investigate is { } spot )
		{
			if ( _sinceInvestigate > 15f )
			{
				_investigate = null;
			}
			else
			{
				Mode = DroneMode.Investigate;
				if ( pos.Distance( spot ) < 100f )
				{
					if ( !_arrived ) { _arrived = true; _sinceArrived = 0; }
					if ( _sinceArrived > 3f ) _investigate = null;
					face = Rotation.FromYaw( Time.Now * 35f ).Forward;   // осматривается
					return Steer( Hold( spot ) );
				}
				var v = TravelTo( spot, CruiseSpeed );
				face = v;
				return Steer( v );
			}
		}

		Mode = DroneMode.Patrol;
		return Steer( Patrol( out face ) );
	}

	/// <summary>Заход на видимую цель в манере этого дрона. Возвращает желаемую скорость.</summary>
	private Vector3 AttackRun( Vector3 pos, Vector3 aim, float dist, out Vector3 face, out bool diving )
	{
		face = aim - pos;
		diving = false;
		var commit = CommitDistance;

		switch ( _style )
		{
			case AttackStyle.TopAttack:
				commit *= 0.7f;
				if ( !_perched )
				{
					// набираем высоту над целью, чуть со своей стороны, потом почти отвесно вниз
					var side = ( pos - aim ).WithZ( 0f );
					side = side.Length > 1f ? side.Normal : Vector3.Forward;
					var perch = aim + Vector3.Up * 330f + side * 140f;
					var high = pos.z - aim.z > 240f && ( pos - aim ).WithZ( 0f ).Length < 320f;
					if ( high || pos.Distance( perch ) < 90f || !CanFly( pos, perch ) || !CanFly( perch, aim, _target ) )
					{
						_perched = true;   // набрали — или под крышей набирать некуда: сразу в атаку
					}
					else
					{
						Mode = DroneMode.Hunt;
						return Steer( Arrive( perch, CruiseSpeed * 1.3f ) );
					}
				}
				break;

			case AttackStyle.Stalker:
			{
				commit *= 0.6f;
				// кружит на расстоянии и ждёт: цель встала, отвернулась или ждать надоело
				var still = _targetVel.WithZ( 0f ).Length < 60f;
				var behind = LookDirection( _target ).Dot( ( pos - aim ).WithZ( 0f ).Normal ) < -0.2f;
				if ( still || behind || _sinceStalk > 7f ) break;

				_orbit += 25f * Time.Delta;
				var spot = aim + Rotation.FromYaw( _orbit ).Forward * 520f + Vector3.Up * 140f;
				if ( !CanFly( pos, spot ) ) break;   // кружить негде — бьём
				Mode = DroneMode.Hunt;
				return Steer( Arrive( spot, CruiseSpeed ), _target );
			}

			case AttackStyle.LowSkim:
				commit *= 0.8f;
				if ( dist > 360f )
				{
					// низко, на высоте груди цели, с ходу
					Mode = DroneMode.Attack;
					diving = true;
					var run = ( aim - pos ).WithZ( 0f ).Normal * DiveSpeed + Vector3.Up * ( aim.z + 15f - pos.z ) * 2f;
					return Steer( run, _target );
				}
				break;
		}

		Mode = DroneMode.Attack;
		diving = true;
		if ( dist < commit && _commit is null )
		{
			_commit = aim;
			_closest = float.MaxValue;
		}
		return ( aim - pos ).Normal * DiveSpeed;
	}

	private static Vector3 LookDirection( GameObject go )
	{
		if ( go.GetComponent<PlayerController>() is { } player ) return player.EyeAngles.ToRotation().Forward.WithZ( 0f ).Normal;
		return go.WorldRotation.Forward.WithZ( 0f ).Normal;
	}

	/// <summary>Без цели: облетает точки вокруг места появления, на каждой висит и озирается.</summary>
	private Vector3 Patrol( out Vector3 face )
	{
		var pos = WorldPosition;
		face = Rotation.FromYaw( Time.Now * 25f + _home.x ).Forward;   // медленно крутит головой

		if ( _wanderPoint is null )
		{
			if ( !_wanderWait ) return Hold( _holdPos );
			_wanderPoint = PickWanderPoint();
			if ( _wanderPoint is null )
			{
				_wanderWait = 1f;
				return Hold( _holdPos );
			}
		}

		var wp = _wanderPoint.Value;
		if ( pos.Distance( wp ) < 64f )
		{
			_wanderPoint = null;
			_wanderWait = Random.Shared.Float( 1.5f, 4f );
			return Hold( wp );
		}

		var v = TravelTo( wp, CruiseSpeed * 0.6f );
		face = v;
		return v;
	}

	private Vector3? PickWanderPoint()
	{
		Vector3? floor = null;
		if ( NavReady ) floor = Scene.NavMesh.GetRandomPoint( _home, PatrolRadius );
		if ( floor is null )
		{
			var p = _home + Rotation.FromYaw( Random.Shared.Float( 0f, 360f ) ).Forward * Random.Shared.Float( 200f, PatrolRadius );
			floor = FloorBelow( p.WithZ( _home.z + 200f ) );
		}
		return floor is { } f ? LiftTo( f, PatrolHeight ) : null;
	}

	// ================= Дорога =================

	private bool NavReady => Scene.NavMesh is { IsEnabled: true, IsGenerating: false };

	/// <summary>
	/// Скорость к точке: напрямую, если сфера размером с дрон пролетает; иначе по навмешу — точки маршрута подняты
	/// над полом (под низким потолком ниже), углы срезаются, если дальняя точка видна. Навмеша нет — напрямик,
	/// а облёт препятствий (и подъём над стенами) сделает Steer.
	/// </summary>
	private Vector3 TravelTo( Vector3 goal, float speed, GameObject allow = null )
	{
		var pos = WorldPosition;
		if ( CanFly( pos, goal, allow ) )
		{
			_path.Clear();
			return Arrive( goal, speed );
		}

		if ( _path.Count == 0 || _pathIndex >= _path.Count || _sincePath > 2f || ( _sincePath > 0.5f && _pathGoal.Distance( goal ) > 96f ) )
			BuildPath( goal );

		if ( _pathIndex < _path.Count )
		{
			if ( _sincePathSkip > 0.1f )
			{
				_sincePathSkip = 0;
				for ( var i = Math.Min( _path.Count - 1, _pathIndex + 4 ); i > _pathIndex; i-- )
				{
					if ( !CanFly( pos, _path[i], allow ) ) continue;
					_pathIndex = i;
					break;
				}
			}

			if ( pos.Distance( _path[_pathIndex] ) < 40f ) _pathIndex++;
			if ( _pathIndex < _path.Count )
			{
				var wp = _path[_pathIndex];
				if ( _pathIndex == _path.Count - 1 ) return Arrive( wp, speed );

				// перед поворотом сбрасываем скорость — в дверях без заносов
				var turn = Vector3.GetAngle( wp - pos, _path[_pathIndex + 1] - wp );
				var near = Math.Clamp( 1f - pos.Distance( wp ) / 200f, 0f, 1f );
				return ( wp - pos ).Normal * speed * ( 1f - near * Math.Clamp( turn / 90f, 0f, 1f ) * 0.5f );
			}
		}

		return ( goal - pos ).Normal * speed;
	}

	private void BuildPath( Vector3 goal )
	{
		_path.Clear();
		_pathIndex = 0;
		_pathGoal = goal;
		_sincePath = 0;
		if ( !NavReady ) return;

		var nav = Scene.NavMesh;
		var from = FloorBelow( WorldPosition ) ?? WorldPosition;
		var to = FloorBelow( goal ) ?? goal;
		if ( nav.GetClosestPoint( from, 300f ) is not { } a ) return;
		if ( nav.GetClosestPoint( to, 300f ) is not { } b ) return;

		var result = nav.CalculatePath( new CalculatePathRequest { Start = a, Target = b } );
		if ( result.Points is null || result.Points.Count == 0 ) return;
		if ( result.Status is NavMeshPathStatus.PathNotFound or NavMeshPathStatus.StartNotFound or NavMeshPathStatus.TargetNotFound ) return;

		foreach ( var p in result.Points )
			_path.Add( LiftTo( p.Position, CruiseHeight ) );
		_path.Add( goal );   // последняя точка — сама цель, а не пол под ней
	}

	/// <summary>Точка над полом на высоте height, но не выше середины до потолка.</summary>
	private Vector3 LiftTo( Vector3 floor, float height )
	{
		var up = Scene.Trace.Ray( floor + Vector3.Up * 4f, floor + Vector3.Up * ( height * 2f ) ).WithoutTags( NotFloor ).Run();
		var room = up.Hit ? up.Distance + 4f : height * 2f;
		return floor + Vector3.Up * Math.Clamp( room * 0.5f, 20f, height );
	}

	private Vector3? FloorBelow( Vector3 p )
	{
		var tr = Scene.Trace.Ray( p + Vector3.Up * 8f, p + Vector3.Down * 4000f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( NotFloor )
			.Run();
		return tr.Hit ? tr.EndPosition : null;
	}

	/// <summary>Пролетит ли дрон (сфера BodyRadius) по прямой. allow — во что упереться можно (в цель).</summary>
	private bool CanFly( Vector3 from, Vector3 to, GameObject allow = null )
	{
		var tr = Scene.Trace.Sphere( BodyRadius, from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		if ( tr.StartedSolid )   // прижались к чему-то вплотную — проверим тонким лучом
			tr = Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		return !tr.Hit || ( allow.IsValid() && IsPartOf( tr.GameObject, allow ) );
	}

	private bool CanSee( Vector3 from, Vector3 to, GameObject target )
	{
		var tr = Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		return !tr.Hit || IsPartOf( tr.GameObject, target );
	}

	private static bool IsPartOf( GameObject hit, GameObject target )
	{
		if ( !hit.IsValid() || !target.IsValid() ) return false;
		return hit == target || hit.Root == target.Root;
	}

	private Vector3 Arrive( Vector3 goal, float speed )
	{
		var d = goal - WorldPosition;
		var dist = d.Length;
		if ( dist < 1f ) return Vector3.Zero;
		return d / dist * MathF.Min( speed, dist * 1.5f );
	}

	private Vector3 Hold( Vector3 spot ) => ( ( spot - WorldPosition ) * 1.5f ).ClampLength( CruiseSpeed );

	/// <summary>
	/// Поправки к желаемой скорости: препятствие впереди — скользим вдоль него и отходим (упёрлись лбом — вверх,
	/// через забор); не цепляем пол; не толкаемся с другими дронами.
	/// </summary>
	private Vector3 Steer( Vector3 wish, GameObject allow = null )
	{
		var pos = WorldPosition;
		var speed = wish.Length;

		if ( speed > 1f )
		{
			var dir = wish / speed;
			var look = MathF.Max( 90f, _rb.Velocity.Length * 0.7f );
			var tr = Scene.Trace.Sphere( BodyRadius, pos, pos + dir * look ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
			if ( tr.Hit && !tr.StartedSolid && !( allow.IsValid() && IsPartOf( tr.GameObject, allow ) ) )
			{
				var n = tr.Normal;
				var slide = dir - n * dir.Dot( n );
				if ( slide.Length < 0.25f ) slide = Vector3.Up;
				var close = 1f - tr.Fraction;
				dir = ( slide.Normal + n * close * 1.2f ).Normal;
				wish = dir * speed * ( 1f - close * 0.5f );
			}
		}

		var down = Scene.Trace.Ray( pos, pos + Vector3.Down * 40f ).IgnoreGameObjectHierarchy( GameObject ).WithoutTags( NotObstacles ).Run();
		if ( down.Hit && !( allow.IsValid() && IsPartOf( down.GameObject, allow ) ) )
			wish += Vector3.Up * ( 40f - down.Distance ) * 6f;

		if ( Scene.GetSystem<NpcSystem>() is { } npcs )
		{
			foreach ( var npc in npcs.Npcs )
			{
				if ( npc == this || npc is not FpvDroneNpc ) continue;
				var away = pos - npc.WorldPosition;
				var d = away.Length;
				if ( d < 1f || d > 90f ) continue;
				wish += away / d * ( 90f - d ) * 5f;
			}
		}

		return wish;
	}

	/// <summary>Хотим лететь, а стоим на месте больше секунды — короткий рывок вверх-вбок и новый маршрут.</summary>
	private void CheckStuck( Vector3 wish )
	{
		var pos = WorldPosition;
		if ( wish.Length < 120f || pos.Distance( _progressPos ) > 40f )
		{
			_progressPos = pos;
			_sinceProgress = 0;
			return;
		}
		if ( _sinceProgress < 1.2f ) return;

		_progressPos = pos;
		_sinceProgress = 0;
		_unstickDir = ( Vector3.Up + Rotation.FromYaw( Random.Shared.Float( 0f, 360f ) ).Forward ).Normal;
		_unstick = 0.5f;
		_path.Clear();
	}

	// ================= Полёт =================

	/// <summary>
	/// Полёт как у настоящего квадрокоптера. Винты тянут только вдоль «верха» корпуса: чтобы полететь вбок,
	/// затормозить или повернуть, дрон сначала наклоняется — не быстрее TiltRate, — и только потом тяга уводит
	/// его в нужную сторону. Отсюда дуги, занос на поворотах, торможение «на спину». Воздух тормозит квадратично,
	/// поэтому у каждого наклона своя предельная скорость (в пике — DiveSpeed). Нос смотрит по курсу или на цель
	/// и разворачивается не быстрее YawRate.
	/// </summary>
	private void Fly( Vector3 wish, Vector3 face, bool diving )
	{
		if ( _rb.Sleeping ) _rb.Sleeping = false;
		var dt = Time.Delta;
		var v = _rb.Velocity;
		var gravity = _rb.Gravity ? Scene.PhysicsWorld.Gravity * _rb.GravityScale : Vector3.Zero;
		var g = MathF.Max( gravity.Length, 1f );

		// воздух: при наклоне DiveTilt дрон разгоняется ровно до DiveSpeed
		var dragK = g * MathF.Tan( DiveTilt * MathF.PI / 180f ) / MathF.Max( DiveSpeed * DiveSpeed, 1f );
		var drag = -v * v.Length * dragK;

		// какое ускорение хотим -> какая для этого нужна тяга (без веса и воздуха)
		wish = wish.ClampLength( diving ? DiveSpeed : CruiseSpeed * 1.5f );
		var acc = ( ( wish - v ) * 2.5f ).ClampLength( Acceleration );
		var thrust = acc - gravity - drag;

		// наклон ограничен; винты не тянут вниз
		var vert = MathF.Max( thrust.z, g * 0.25f );
		var horiz = thrust.WithZ( 0f );
		var maxHoriz = vert * MathF.Tan( ( diving ? DiveTilt : MaxTilt ) * MathF.PI / 180f );
		if ( horiz.Length > maxHoriz ) horiz = horiz.Normal * maxHoriz;
		var wantUp = ( horiz + Vector3.Up * vert ).Normal;

		// курс носа — с ограниченной скоростью разворота
		var flat = face.WithZ( 0f );
		if ( flat.Length > 1f )
		{
			var delta = Angles.NormalizeAngle( Rotation.LookAt( flat.Normal ).Angles().yaw - _yaw );
			_yaw += Math.Clamp( delta * MathF.Min( 1f, dt * 4f ), -YawRate * dt, YawRate * dt );
		}

		// желаемая ориентация: «верх» по тяге, нос по курсу; корпус догоняет её не быстрее TiltRate
		var fwd = Rotation.FromYaw( _yaw ).Forward;
		fwd = ( fwd - wantUp * fwd.Dot( wantUp ) ).Normal;
		var want = Rotation.LookAt( fwd, wantUp );
		var current = _rb.WorldRotation;
		var angle = AngleBetween( current, want );
		var step = MathF.Min( TiltRate * dt, angle * MathF.Min( 1f, dt * 10f ) );
		var next = angle > 0.01f ? Rotation.Slerp( current, want, step / angle ) : want;
		_rb.SmoothRotate( next, dt, dt );

		// тяга — вдоль того «верха», что есть сейчас, а не того, что хотим
		var bodyUp = current.Up;
		var power = Math.Clamp( thrust.Dot( bodyUp ), 0f, g * 3f );
		_rb.ApplyForce( ( bodyUp * power + drag ) * _rb.Mass );
	}

	/// <summary>Угол между двумя поворотами, градусы.</summary>
	private static float AngleBetween( Rotation a, Rotation b )
	{
		var dot = MathF.Abs( a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w );
		return 2f * MathF.Acos( MathF.Min( dot, 1f ) ) * 180f / MathF.PI;
	}

	// ================= Цель =================

	private static bool IsAlive( GameObject go )
	{
		if ( go.GetComponent<PlayerController>() is not null ) return go.GetComponent<PlayerHealth>() is not { } health || health.Health > 0f;
		if ( go.GetComponent<Npc>() is { } npc ) return npc.Health >= 1f;
		return true;
	}

	/// <summary>Куда целиться: у игрока — в грудь, у дрона — в центр, у остальных — в середину габаритов.</summary>
	private static Vector3 AimPoint( GameObject go )
	{
		if ( go.GetComponent<PlayerController>() is { } player )
			return Vector3.Lerp( player.WorldPosition, player.EyePosition, 0.6f );
		if ( go.GetComponent<FpvDroneNpc>() is not null )
			return go.WorldPosition;
		return go.GetBounds().Center;
	}

	private static Vector3 VelocityOf( GameObject go )
	{
		if ( go.GetComponent<NavMeshAgent>() is { } agent && agent.Velocity.Length > 1f ) return agent.Velocity;
		return SensesLayer.GetVelocity( go );
	}

	/// <summary>Упреждение: куда цель успеет сместиться, пока мы долетим.</summary>
	private Vector3 Lead( Vector3 pos )
	{
		var t = Math.Clamp( pos.Distance( _targetPos ) / DiveSpeed, 0f, 0.5f ) * 0.6f;
		return _targetPos + _targetVel * t;
	}

	/// <summary>Неконтактный взрыватель: близко к точке прицеливания или вплотную к габаритам цели.</summary>
	private bool NearTarget( Vector3 pos )
	{
		if ( pos.Distance( _targetPos ) < TriggerDistance ) return true;
		var bounds = _target.GetBounds();
		return bounds.ClosestPoint( pos ).Distance( pos ) < BodyRadius + 10f;
	}

	// ================= Урон и взрыв =================

	protected override void OnHurt( in DamageInfo damage )
	{
		// Кто нас ранил — тот враг, даже если был своим (как у Army Guy): запоминаем его лично с высоким
		// приоритетом и летим к нему. Другие дроны на него не обижаются — только этот.
		var attacker = damage.Attacker;
		if ( _exploded || !attacker.IsValid() ) return;
		attacker = attacker.GetComponentInParent<PlayerController>()?.GameObject ?? attacker.GetComponentInParent<Npc>()?.GameObject ?? attacker;
		if ( attacker == GameObject ) return;
		SetDisposition( attacker, Disposition.Hostile, priority: 10 );

		if ( attacker != _target ) NewTarget();
		_target = attacker;
		_targetPos = AimPoint( attacker );
		_sinceSeen = 0;
	}

	protected override void Die( in DamageInfo damage )
	{
		if ( _exploded ) return;

		// сбили — заряд всё равно рвётся (по своим не бьёт, но стрелять в упор не стоит)
		Detonate( destroy: false );
		base.Die( damage );   // килфид, стимул «смерть», удаление
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy || _exploded ) return;

		var other = collision.Other.GameObject;
		var speed = collision.Contact.Speed.Length;

		// врезались во врага — подрыв
		if ( other.IsValid() && GetDisposition( other ) == Disposition.Hostile )
		{
			Detonate();
			return;
		}

		// в пике задели что-то у самой цели (машину, в которой она сидит, стену, за которую прячется) — тоже подрыв
		if ( Mode == DroneMode.Attack && speed > 250f && _target.IsValid() && WorldPosition.Distance( _targetPos ) < 180f )
		{
			Detonate();
			return;
		}

		// сильный удар (швырнули физганом в стену и т.п.)
		if ( speed > 900f ) Detonate();
	}

	/// <summary>Подрыв: взрыв виден всем, урон и толчок вокруг (кроме своих), стимул для NPC, дрон исчезает.</summary>
	private void Detonate( bool destroy = true )
	{
		if ( _exploded || IsProxy ) return;
		_exploded = true;
		var origin = WorldPosition;

		var fx = GameObject.Children.FirstOrDefault( c => c.Name == "explosion" );
		if ( fx.IsValid() )
		{
			var clone = fx.Clone( origin, Rotation.FromYaw( WorldRotation.Angles().yaw ) );
			clone.Enabled = true;
			clone.NetworkSpawn( true, null );
		}

		DealBlast( origin );
		EmitStimulus( StimulusKind.Explosion, 2048f, 2f );
		if ( _sound.IsValid() ) _sound.Stop();
		if ( destroy ) GameObject.Destroy();
	}

	/// <summary>
	/// Урон к краю радиуса падает до нуля, за стеной — вчетверо слабее. Своих не трогаем: у дружеского — игроки,
	/// союзники и мирные; у вражеского — свои враги. Пропы и мир — всем.
	/// </summary>
	private void DealBlast( Vector3 origin )
	{
		var damaged = new HashSet<Component>();
		var pushed = new HashSet<Rigidbody>();

		foreach ( var go in Scene.FindInPhysics( new Sphere( origin, BlastRadius ) ) )
		{
			if ( !go.IsValid() || go.Root == GameObject ) continue;

			var damageable = go.GetComponentInParent<Component.IDamageable>();
			var body = go.GetComponentInParent<Rigidbody>();
			var owner = ( damageable as Component )?.GameObject ?? body?.GameObject ?? go;
			if ( IsFriend( owner ) ) continue;

			var bounds = owner.GetBounds();
			var closest = bounds.ClosestPoint( origin );
			var frac = 1f - Math.Clamp( origin.Distance( closest ) / BlastRadius, 0f, 1f );
			if ( frac <= 0f ) continue;
			if ( !CanSee( origin, bounds.Center, owner ) ) frac *= 0.25f;

			if ( damageable is Component dc && damaged.Add( dc ) )
			{
				var info = new DamageInfo( BlastDamage * frac, GameObject, GameObject );
				info.Tags.Add( DamageTags.Explosion );
				info.Origin = origin;
				info.Position = closest;
				damageable.OnDamage( info );
			}

			if ( body.IsValid() && body.MotionEnabled && pushed.Add( body ) )
			{
				var dir = body.WorldPosition - origin;
				dir = dir.IsNearlyZero() ? Vector3.Up : dir.Normal;
				body.ApplyImpulse( ( dir + Vector3.Up * 0.3f ).Normal * BlastForce * frac * MathF.Min( body.Mass, 1000f ) );
			}
		}
	}

	private bool IsFriend( GameObject go )
	{
		var faction = GetFactionOf( go );
		if ( faction is null ) return false;
		return faction == Faction || GetDisposition( go ) == Disposition.Friendly;
	}

	// ================= Визуал (у всех) =================

	private void UpdateVisuals()
	{
		// скорость — по смещению за короткое окно: у клиентов позиция приходит шагами сети
		var elapsed = Time.Now - _speedSampleTime;
		if ( elapsed >= 0.15f )
		{
			_visualSpeed = ( WorldPosition - _speedSamplePos ).Length / elapsed;
			_speedSamplePos = WorldPosition;
			_speedSampleTime = Time.Now;
		}
		var k = Math.Clamp( _visualSpeed / MathF.Max( DiveSpeed, 1f ), 0f, 1f );

		var spin = PropellerSpeed * ( 0.45f + 0.55f * k ) * Time.Delta;
		foreach ( var prop in _props )
			if ( prop.IsValid() ) prop.LocalRotation *= Rotation.FromYaw( spin );

		if ( FlightSound is not null && !_exploded && ( !_sound.IsValid() || _sound.IsStopped ) )
		{
			_sound = Sound.Play( FlightSound, WorldPosition );
			if ( _sound.IsValid() )
			{
				_sound.Parent = GameObject;
				_sound.FollowParent = true;
			}
		}
		if ( _sound.IsValid() )
		{
			_sound.Pitch = 0.95f + k * 0.7f;
			_sound.Volume = 0.45f + k * 0.55f;
		}
	}
}
