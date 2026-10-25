using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// The aura as a liquid. Space around the holder is cut into cells; the liquid spreads out from the
/// holder from cell to cell (round corners, down stairwells, through doors into side rooms) wherever the
/// way between two cell centres is clear - never through a wall or a floor - until there's no more of it:
/// as much as a dome of the radius would hold. Going down is cheaper than going up, so it settles a
/// little and runs down stairs, but out in the open it's still near enough a ball. Cells fill and drain
/// smoothly, so it pours out when switched on and flows after the holder.
/// Everyone works it out themselves from the same walls, so it isn't networked.
/// </summary>
public sealed class AuraShape
{
	public const float Cell = 24f;
	public const int SizeX = 64, SizeY = 64, SizeZ = 32;
	private const int Total = SizeX * SizeY * SizeZ;

	/// <summary>How many ways between cells it checks per frame - the rest wait for the next frames.</summary>
	private const int TestsPerFrame = 1500;

	/// <summary>How fast it pours out when switched on.</summary>
	private const float PourSpeed = 900f;

	/// <summary>Within this many cells of the holder the liquid doesn't pour in, it's simply there.</summary>
	private const float SnapCost = 4f;

	private const float UpCost = 1.1f;
	private const float DownCost = 0.95f;

	// The way from each cell to its +X, +Y and +Z neighbour, two bits each: 0 unknown, 1 clear, 2 walled.
	// Kept per world cell as the holder moves about.
	private readonly Dictionary<long, byte> _known = new();
	private readonly Queue<int> _requests = new();   // cell * 3 + axis
	private readonly bool[] _requested = new bool[Total * 3];

	// The box of cells around the holder that the liquid lives in
	private Vector3Int _origin;
	private readonly byte[] _ways = new byte[Total];
	private readonly float[] _target = new float[Total];
	private readonly float[] _amount = new float[Total];
	private readonly float[] _amountBefore = new float[Total];
	private readonly byte[] _wall = new byte[Total];
	private readonly byte[] _texture = new byte[Total * 4];

	// Spreading
	private readonly float[] _cost = new float[Total];
	private readonly MinHeap _heap = new();
	private readonly List<int> _filled = new();
	private readonly List<int> _touched = new();
	private bool _placed;

	private static readonly (int X, int Y, int Z, float Cost)[] Steps = BuildSteps();

	// Looking ahead: every cell within this many cells of the holder, nearest first
	private const int LookAheadCells = 16;
	private static readonly Vector3Int[] LookAhead = BuildLookAhead();
	private Vector3Int _lookAheadFrom;
	private int _lookAhead;

	/// <summary>How far from the holder the liquid can be found right now.</summary>
	public float Reach { get; private set; }

	/// <summary>The world-space box round the cells that hold any liquid right now (where the shader looks).</summary>
	public Vector3 ActiveMin { get; private set; }
	public Vector3 ActiveMax { get; private set; }

	/// <summary>The world-space corner of the cell box, and its size.</summary>
	public Vector3 BoxMin => (Vector3)_origin * Cell;
	public Vector3 BoxSize => new( SizeX * Cell, SizeY * Cell, SizeZ * Cell );

	public AuraShape()
	{
		for ( var i = 0; i < Total; i++ ) _cost[i] = float.MaxValue;
	}

	/// <summary>Empties it: switched on again, it pours out from the holder anew.</summary>
	public void Reset()
	{
		Array.Clear( _target );
		Array.Clear( _amount );
		Reach = 0f;
	}

	/// <summary>
	/// Works out where the liquid wants to be: spreads from the holder's chest from cell to cell, cheapest
	/// first, until the dome's worth of cells is used or it can't get further. <paramref name="secondsOn"/>
	/// limits how far it has got since it was switched on.
	/// </summary>
	public void Spread( Scene scene, GameObject ignore, Vector3 center, float radius, float secondsOn )
	{
		Recenter( center );

		var budget = (int)(2f / 3f * MathF.PI * radius * radius * radius / (Cell * Cell * Cell));
		var maxCost = MathF.Min( radius * 2.5f, PourSpeed * secondsOn ) / Cell;

		foreach ( var i in _filled ) _target[i] = 0f;
		foreach ( var i in _touched ) _cost[i] = float.MaxValue;
		_filled.Clear();
		_touched.Clear();
		_heap.Clear();

		// Only what this spread asks for, nearest first: old asks from further back wait no longer in front
		// of the ones right ahead of a running holder
		while ( _requests.Count > 0 ) _requested[_requests.Dequeue()] = false;
		_lookAheadFrom = Local( center );
		_lookAhead = 0;

		// It pours out of the cells round the holder's chest that the chest can reach: against a wall, the
		// chest's own cell can have its middle inside the wall, and nothing would come out of it at all
		var at = Local( center );
		var seeded = false;
		for ( var dz = -1; dz <= 1; dz++ )
			for ( var dy = -1; dy <= 1; dy++ )
				for ( var dx = -1; dx <= 1; dx++ )
				{
					var i = Index( at + new Vector3Int( dx, dy, dz ) );
					if ( i < 0 ) continue;
					var middle = CellCenter( i );
					if ( Blocked( scene, center, middle, ignore, 4f ) ) continue;
					var cost = (middle - center).Length / Cell;
					if ( cost >= _cost[i] ) continue;
					if ( _cost[i] == float.MaxValue ) _touched.Add( i );
					_cost[i] = cost;
					_heap.Push( i, cost );
					seeded = true;
				}
		if ( !seeded ) return;
		var reach = 0f;

		while ( _heap.Count > 0 && _filled.Count < budget )
		{
			var (i, cost) = _heap.Pop();
			if ( cost > _cost[i] || _target[i] > 0f ) continue;

			_target[i] = 1f;
			_filled.Add( i );
			reach = MathF.Max( reach, cost );

			// Right round the holder it's there at once - however fast they run, they're never out of it
			if ( cost <= SnapCost ) _amount[i] = 1f;

			int x = i % SizeX, y = i / SizeX % SizeY, z = i / (SizeX * SizeY);
			var touchesWall = false;
			foreach ( var (dx, dy, dz, step) in Steps )
			{
				var n = Index( new Vector3Int( x + dx, y + dy, z + dz ) );
				if ( n < 0 ) continue;

				var way = Reachable( x, y, z, dx, dy, dz );
				if ( way == 2 && Math.Abs( dx ) + Math.Abs( dy ) + Math.Abs( dz ) == 1 ) touchesWall = true;
				if ( way != 1 ) continue;

				var next = cost + step;
				if ( next > maxCost || next >= _cost[n] ) continue;
				if ( _cost[n] == float.MaxValue ) _touched.Add( n );
				_cost[n] = next;
				_heap.Push( n, next );
			}
			_wall[i] = touchesWall ? (byte)255 : (byte)0;
		}

		Reach = (reach + 2f) * Cell;
	}

	/// <summary>
	/// Can the liquid get from cell (x,y,z) to its neighbour (dx,dy,dz)? 1 yes, 2 walled, 0 not known yet
	/// (it has been asked for). A diagonal goes round by the straight ways, either way round - no
	/// squeezing through the crack where two walls meet.
	/// </summary>
	private int Reachable( int x, int y, int z, int dx, int dy, int dz )
	{
		var axes = (dx != 0 ? 1 : 0) + (dy != 0 ? 1 : 0) + (dz != 0 ? 1 : 0);
		if ( axes == 1 ) return Way( x, y, z, dx, dy, dz );

		// Split into its two straight steps: a, then b
		int ax, ay, az, bx, by, bz;
		if ( dx != 0 ) { ax = dx; ay = 0; az = 0; bx = 0; by = dy; bz = dz; }
		else { ax = 0; ay = dy; az = 0; bx = 0; by = 0; bz = dz; }

		var unknown = false;
		for ( var order = 0; order < 2; order++ )
		{
			var first = order == 0 ? Way( x, y, z, ax, ay, az ) : Way( x, y, z, bx, by, bz );
			if ( first == 0 ) { unknown = true; continue; }
			if ( first == 2 ) continue;

			var second = order == 0 ? Way( x + ax, y + ay, z + az, bx, by, bz ) : Way( x + bx, y + by, z + bz, ax, ay, az );
			if ( second == 1 ) return 1;
			if ( second == 0 ) unknown = true;
		}

		return unknown ? 0 : 2;
	}

	/// <summary>The straight way from a cell one step along one axis, either direction.</summary>
	private int Way( int x, int y, int z, int dx, int dy, int dz )
	{
		// Each way is kept on the cell at its lower end
		if ( dx < 0 || dy < 0 || dz < 0 ) { x += dx; y += dy; z += dz; dx = -dx; dy = -dy; dz = -dz; }
		var i = Index( new Vector3Int( x, y, z ) );
		if ( i < 0 || Index( new Vector3Int( x + dx, y + dy, z + dz ) ) < 0 ) return 2;

		var axis = dx != 0 ? 0 : dy != 0 ? 1 : 2;
		var state = (_ways[i] >> (axis * 2)) & 3;
		if ( state == 0 ) Request( i * 3 + axis );
		return state;
	}

	/// <summary>Checks some of the ways the liquid wants to go: clear, or is there a wall in between?</summary>
	/// <remarks>
	/// First what the last spread asked for; then, with what's left of the frame, everything round the
	/// holder nearest first, before the liquid gets there. Waiting for the liquid to arrive meant it could
	/// only find its way one cell further each spread - slower than someone running into new ground.
	/// </remarks>
	public void TestCells( Scene scene, GameObject ignore )
	{
		var done = 0;
		TestOpenBlocks( scene, ignore );
		while ( done < TestsPerFrame && _requests.Count > 0 )
		{
			var request = _requests.Dequeue();
			_requested[request] = false;
			int i = request / 3, axis = request % 3;
			if ( ((_ways[i] >> (axis * 2)) & 3) != 0 ) continue;
			TestWay( scene, i, axis, ignore );
			done++;
		}

		var checkedCells = 0;
		while ( done < TestsPerFrame && _lookAhead < LookAhead.Length && checkedCells < 20000 )
		{
			var offset = LookAhead[_lookAhead++];
			checkedCells++;
			var i = Index( _lookAheadFrom + offset );
			if ( i < 0 ) continue;
			for ( var axis = 0; axis < 3; axis++ )
			{
				if ( ((_ways[i] >> (axis * 2)) & 3) != 0 ) continue;
				if ( Index( _lookAheadFrom + offset + (axis == 0 ? new Vector3Int( 1, 0, 0 ) : axis == 1 ? new Vector3Int( 0, 1, 0 ) : new Vector3Int( 0, 0, 1 )) ) < 0 ) continue;
				TestWay( scene, i, axis, ignore );
				done++;
			}
		}
	}

	// Open air in whole blocks: one look at a 4x4x4 block of cells, and if nothing solid is anywhere in
	// it, every way inside it is clear at once. Flying or falling, the liquid kept moving into air whose
	// ways it hadn't checked yet - 1500 checks a frame can't keep up with someone dropping fast, so it
	// shrank back to the holder and poured out again, over and over (it flickered). Near walls and floors
	// a block isn't empty, and the ways are checked one by one as before.
	private const int Block = 4;
	private const int BlocksPerFrame = 48;
	private const int BlockReach = 5;
	private static readonly Vector3Int[] BlockOrder = BuildBlockOrder();

	private void TestOpenBlocks( Scene scene, GameObject ignore )
	{
		var looked = 0;
		var home = new Vector3Int( FloorDiv( _lookAheadFrom.x, Block ), FloorDiv( _lookAheadFrom.y, Block ), FloorDiv( _lookAheadFrom.z, Block ) );
		foreach ( var offset in BlockOrder )
		{
			if ( looked >= BlocksPerFrame ) return;
			var corner = (home + offset) * Block;
			if ( !HasUnknown( corner ) ) continue;
			looked++;

			// From the first cell centre to one cell past the last, so the ways out of the block's far
			// sides are covered too, and as wide as the ball the single ways are checked with
			var min = CellCenterLocal( corner ) - 6f;
			var max = CellCenterLocal( corner + new Vector3Int( Block, Block, Block ) ) + 6f;
			var middle = (min + max) * 0.5f;
			var box = new BBox( min - middle, max - middle );
			var tr = scene.Trace.Box( box, middle, middle + Vector3.Up * 0.1f )
				.IgnoreDynamic().IgnoreKeyframed().IgnoreGameObjectHierarchy( ignore )
				.WithoutTags( "player", "npc", "trigger", "ragdoll", "playercontroller", "debris" )
				.Run();
			if ( tr.Hit || tr.StartedSolid ) continue;

			for ( var z = 0; z < Block; z++ )
				for ( var y = 0; y < Block; y++ )
					for ( var x = 0; x < Block; x++ )
					{
						var c = corner + new Vector3Int( x, y, z );
						var i = Index( c );
						if ( i < 0 ) continue;
						var ways = _ways[i];
						for ( var axis = 0; axis < 3; axis++ )
						{
							if ( ((ways >> (axis * 2)) & 3) != 0 ) continue;
							var n = c + (axis == 0 ? new Vector3Int( 1, 0, 0 ) : axis == 1 ? new Vector3Int( 0, 1, 0 ) : new Vector3Int( 0, 0, 1 ));
							if ( Index( n ) < 0 ) continue;
							ways = (byte)(ways | (1 << (axis * 2)));
						}
						if ( ways == _ways[i] ) continue;
						_ways[i] = ways;
						_known[Key( _origin + c )] = ways;
					}
		}
	}

	private bool HasUnknown( Vector3Int corner )
	{
		for ( var z = 0; z < Block; z++ )
			for ( var y = 0; y < Block; y++ )
				for ( var x = 0; x < Block; x++ )
				{
					var c = corner + new Vector3Int( x, y, z );
					var i = Index( c );
					if ( i < 0 ) continue;
					var w = _ways[i];
					for ( var axis = 0; axis < 3; axis++ )
					{
						if ( ((w >> (axis * 2)) & 3) != 0 ) continue;
						var n = c + (axis == 0 ? new Vector3Int( 1, 0, 0 ) : axis == 1 ? new Vector3Int( 0, 1, 0 ) : new Vector3Int( 0, 0, 1 ));
						if ( Index( n ) >= 0 ) return true;
					}
				}
		return false;
	}

	private Vector3 CellCenterLocal( Vector3Int local ) => ((Vector3)(_origin + local) + 0.5f) * Cell;

	private static int FloorDiv( int a, int b ) => (int)MathF.Floor( a / (float)b );

	private static Vector3Int[] BuildBlockOrder()
	{
		var list = new List<Vector3Int>();
		for ( var z = -BlockReach / 2 - 1; z <= BlockReach / 2 + 1; z++ )
			for ( var y = -BlockReach; y <= BlockReach; y++ )
				for ( var x = -BlockReach; x <= BlockReach; x++ )
					list.Add( new Vector3Int( x, y, z ) );
		list.Sort( ( a, b ) => (a.x * a.x + a.y * a.y + a.z * a.z).CompareTo( b.x * b.x + b.y * b.y + b.z * b.z ) );
		return list.ToArray();
	}

	private void TestWay( Scene scene, int i, int axis, GameObject ignore )
	{
		var from = CellCenter( i );
		var to = from + (axis == 0 ? Vector3.Forward : axis == 1 ? Vector3.Left : Vector3.Up) * Cell;
		// Both ways: a floor or a wall is often only solid from one side, and a ray from behind misses it
		var state = Blocked( scene, from, to, ignore ) || Blocked( scene, to, from, ignore ) ? 2 : 1;
		_ways[i] = (byte)(_ways[i] | (state << (axis * 2)));
		_known[Key( _origin + Coords( i ) )] = _ways[i];
	}

	/// <summary>
	/// A ball, not a thin ray: a ray slips through cracks and seams in the ground (the liquid got under
	/// the floor that way). Half a cell across is still far narrower than any way a player can go.
	/// </summary>
	private static bool Blocked( Scene scene, Vector3 from, Vector3 to, GameObject ignore, float radius = 6f )
	{
		var tr = scene.Trace.Sphere( radius, from, to )
			.IgnoreDynamic().IgnoreKeyframed().IgnoreGameObjectHierarchy( ignore )
			.WithoutTags( "player", "npc", "trigger", "ragdoll", "playercontroller", "debris" )
			.Run();
		return tr.Hit || tr.StartedSolid;
	}

	/// <summary>Fills and drains toward where it wants to be: it pours in, and seeps away a bit faster.</summary>
	public void Flow( float dt )
	{
		var fill = 1f - MathF.Exp( -22f * dt );
		var drain = 1f - MathF.Exp( -28f * dt );
		int minX = SizeX, minY = SizeY, minZ = SizeZ, maxX = -1, maxY = -1, maxZ = -1;
		for ( var i = 0; i < Total; i++ )
		{
			var a = _amount[i];
			var t = _target[i];
			if ( a != t )
			{
				a += (t - a) * (t > a ? fill : drain);
				if ( MathF.Abs( a - t ) < 0.01f ) a = t;
				_amount[i] = a;
			}
			if ( a <= 0f ) continue;

			int x = i % SizeX, y = i / SizeX % SizeY, z = i / (SizeX * SizeY);
			if ( x < minX ) minX = x; if ( x > maxX ) maxX = x;
			if ( y < minY ) minY = y; if ( y > maxY ) maxY = y;
			if ( z < minZ ) minZ = z; if ( z > maxZ ) maxZ = z;
		}

		if ( maxX < 0 ) { ActiveMin = ActiveMax = BoxMin; return; }
		ActiveMin = BoxMin + new Vector3( minX - 1, minY - 1, minZ - 1 ) * Cell;
		ActiveMax = BoxMin + new Vector3( maxX + 2, maxY + 2, maxZ + 2 ) * Cell;
	}

	/// <summary>Is this point (world space) in the liquid?</summary>
	public bool Contains( Vector3 point )
	{
		var i = Index( Local( point ) );
		return i >= 0 && _amount[i] > 0.4f;
	}

	/// <summary>The cells as the shader reads them: R how full, G whether it's pressed against a wall.</summary>
	public byte[] TextureData()
	{
		for ( var i = 0; i < Total; i++ )
		{
			_texture[i * 4] = (byte)(_amount[i] * 255f);
			_texture[i * 4 + 1] = _wall[i];
			_texture[i * 4 + 2] = 0;
			_texture[i * 4 + 3] = 255;
		}
		return _texture;
	}

	// ---------------------------------------------------------------- the cell box

	/// <summary>Keeps the box centred on the holder; what was known about the walls comes along.</summary>
	private void Recenter( Vector3 center )
	{
		var c = new Vector3Int( (int)MathF.Floor( center.x / Cell ), (int)MathF.Floor( center.y / Cell ), (int)MathF.Floor( center.z / Cell ) );
		var wanted = c - new Vector3Int( SizeX / 2, SizeY / 2, SizeZ / 2 );
		var shift = wanted - _origin;
		if ( _placed && Math.Abs( shift.x ) < SizeX / 4 && Math.Abs( shift.y ) < SizeY / 4 && Math.Abs( shift.z ) < SizeZ / 4 ) return;
		_placed = true;

		for ( var i = 0; i < Total; i++ ) _amountBefore[i] = _amount[i];
		var oldOrigin = _origin;
		_origin = wanted;
		_requests.Clear();
		Array.Clear( _requested );

		for ( var i = 0; i < Total; i++ )
		{
			var world = _origin + Coords( i );
			_ways[i] = _known.TryGetValue( Key( world ), out var w ) ? w : (byte)0;

			// Liquid already out stays where it is in the world
			var oi = Index( world - oldOrigin );
			_amount[i] = oi >= 0 ? _amountBefore[oi] : 0f;
			_target[i] = 0f;
			_cost[i] = float.MaxValue;
			_wall[i] = 0;
		}
		_filled.Clear();
		_touched.Clear();
	}

	private void Request( int request )
	{
		if ( _requested[request] ) return;
		_requested[request] = true;
		_requests.Enqueue( request );
	}

	private Vector3Int Local( Vector3 world ) =>
		new Vector3Int( (int)MathF.Floor( world.x / Cell ), (int)MathF.Floor( world.y / Cell ), (int)MathF.Floor( world.z / Cell ) ) - _origin;

	private static int Index( Vector3Int c ) =>
		c.x < 0 || c.y < 0 || c.z < 0 || c.x >= SizeX || c.y >= SizeY || c.z >= SizeZ ? -1 : c.x + c.y * SizeX + c.z * SizeX * SizeY;

	private static Vector3Int Coords( int i ) => new( i % SizeX, i / SizeX % SizeY, i / (SizeX * SizeY) );

	private Vector3 CellCenter( int i ) => ((Vector3)(_origin + Coords( i )) + 0.5f) * Cell;

	private static long Key( Vector3Int c ) => ((long)(c.x & 0x1FFFFF) << 42) | ((long)(c.y & 0x1FFFFF) << 21) | (long)(c.z & 0x1FFFFF);

	private static Vector3Int[] BuildLookAhead()
	{
		var list = new List<Vector3Int>();
		for ( var z = -LookAheadCells / 2; z <= LookAheadCells / 2; z++ )
			for ( var y = -LookAheadCells; y <= LookAheadCells; y++ )
				for ( var x = -LookAheadCells; x <= LookAheadCells; x++ )
					if ( x * x + y * y + z * z * 4 <= LookAheadCells * LookAheadCells ) list.Add( new Vector3Int( x, y, z ) );
		list.Sort( ( a, b ) => (a.x * a.x + a.y * a.y + a.z * a.z).CompareTo( b.x * b.x + b.y * b.y + b.z * b.z ) );
		return list.ToArray();
	}

	/// <summary>The 18 neighbours (no corner diagonals - round enough) and what a step to each costs.</summary>
	private static (int, int, int, float)[] BuildSteps()
	{
		var list = new List<(int, int, int, float)>();
		for ( var dz = -1; dz <= 1; dz++ )
			for ( var dy = -1; dy <= 1; dy++ )
				for ( var dx = -1; dx <= 1; dx++ )
				{
					if ( dx == 0 && dy == 0 && dz == 0 ) continue;
					if ( dx != 0 && dy != 0 && dz != 0 ) continue;
					var length = MathF.Sqrt( dx * dx + dy * dy + dz * dz );
					list.Add( (dx, dy, dz, length * (dz > 0 ? UpCost : dz < 0 ? DownCost : 1f)) );
				}
		return list.ToArray();
	}

	/// <summary>A small binary heap of cells by cost.</summary>
	private sealed class MinHeap
	{
		private readonly List<(int Item, float Cost)> _items = new();
		public int Count => _items.Count;
		public void Clear() => _items.Clear();

		public void Push( int item, float cost )
		{
			_items.Add( (item, cost) );
			var i = _items.Count - 1;
			while ( i > 0 )
			{
				var parent = (i - 1) / 2;
				if ( _items[parent].Cost <= _items[i].Cost ) break;
				(_items[parent], _items[i]) = (_items[i], _items[parent]);
				i = parent;
			}
		}

		public (int Item, float Cost) Pop()
		{
			var top = _items[0];
			var last = _items[^1];
			_items.RemoveAt( _items.Count - 1 );
			if ( _items.Count > 0 )
			{
				_items[0] = last;
				var i = 0;
				while ( true )
				{
					int l = i * 2 + 1, r = l + 1, m = i;
					if ( l < _items.Count && _items[l].Cost < _items[m].Cost ) m = l;
					if ( r < _items.Count && _items[r].Cost < _items[m].Cost ) m = r;
					if ( m == i ) break;
					(_items[m], _items[i]) = (_items[i], _items[m]);
					i = m;
				}
			}
			return top;
		}
	}
}

/// <summary>
/// Draws an <see cref="AuraShape"/> as a glowing liquid (shaders/aura_volume.shader): the back of its
/// cell box, the shader doing the rest from the cells' 3D texture.
/// </summary>
public sealed class AuraShapeObject : SceneCustomObject
{
	public Material Material { get; set; }
	public float Beat { get; set; }
	public float Opacity { get; set; } = 1f;

	private readonly AuraShape _shape;
	private readonly Texture _texture;
	private readonly Vertex[] _box = new Vertex[36];
	private Vector3 _min, _size, _activeMin, _activeMax;

	public AuraShapeObject( SceneWorld world, AuraShape shape ) : base( world )
	{
		_shape = shape;
		RenderLayer = SceneRenderLayer.Default;
		_texture = Texture.CreateVolume( AuraShape.SizeX, AuraShape.SizeY, AuraShape.SizeZ, ImageFormat.RGBA8888 )
			.WithDynamicUsage().Finish();
	}

	/// <summary>Sends this frame's cells to the GPU. Called on the main thread, before drawing.</summary>
	/// <summary>When the shape was last sent to the GPU.</summary>
	public float UploadedAt { get; private set; }

	public void Upload()
	{
		UploadedAt = Time.Now;
		_texture.Update3D( _shape.TextureData(), 0, 0, 0, AuraShape.SizeX, AuraShape.SizeY, AuraShape.SizeZ );
		_min = _shape.BoxMin;
		_size = _shape.BoxSize;
		_activeMin = _shape.ActiveMin;
		_activeMax = _shape.ActiveMax;
		Transform = new Transform( (_activeMin + _activeMax) * 0.5f );
		Bounds = new BBox( _activeMin, _activeMax );
	}

	public override void RenderSceneObject()
	{
		if ( Material is null || _activeMax.x <= _activeMin.x ) return;

		var e = (_activeMax - _activeMin) * 0.5f;
		Vector3[] c =
		{
			new( -e.x, -e.y, -e.z ), new( e.x, -e.y, -e.z ), new( e.x, e.y, -e.z ), new( -e.x, e.y, -e.z ),
			new( -e.x, -e.y, e.z ), new( e.x, -e.y, e.z ), new( e.x, e.y, e.z ), new( -e.x, e.y, e.z ),
		};
		int[] faces = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
		for ( var i = 0; i < faces.Length; i++ ) _box[i] = new Vertex( c[faces[i]] );

		var attributes = new RenderAttributes();
		attributes.Set( "AuraVolume", _texture );
		attributes.Set( "AuraBoxMin", _min );
		attributes.Set( "AuraBoxSize", _size );
		attributes.Set( "AuraMarchMin", _activeMin );
		attributes.Set( "AuraMarchMax", _activeMax );
		attributes.Set( "AuraBeat", Beat );
		attributes.Set( "AuraOpacity", Opacity );

		Graphics.Draw( _box.AsSpan(), _box.Length, Material, attributes, Graphics.PrimitiveType.Triangles );
	}
}
