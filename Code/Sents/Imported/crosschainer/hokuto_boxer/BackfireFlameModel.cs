using Sandbox;

namespace CuratedVehicles;

/// <summary>A short crossed-quad flame rendered through the same model path as the car body.</summary>
internal static class BackfireFlameModel
{
	private static Model orangeModel;
	private static Model blueModel;

	public static bool TryGet( out Model orangeFlame, out Model blueFlame )
	{
		orangeFlame = blueFlame = null;
		var blueMaterial = Material.Load( "drift_district/effects/backfire_blue.vmat" );
		var orangeMaterial = Material.Load( "drift_district/effects/backfire_orange.vmat" );
		if ( blueMaterial is null || !blueMaterial.IsValid() || orangeMaterial is null || !orangeMaterial.IsValid() ) return false;
		if ( !orangeModel.IsValid() ) orangeModel = Build( orangeMaterial, "drift_district/backfire_orange" );
		if ( !blueModel.IsValid() ) blueModel = Build( blueMaterial, "drift_district/backfire_blue" );
		orangeFlame = orangeModel;
		blueFlame = blueModel;
		return orangeFlame.IsValid() && !orangeFlame.IsError && blueFlame.IsValid() && !blueFlame.IsError;
	}

	private static Model Build( Material material, string name )
	{
		var vertices = new Vertex[8];
		var indices = new int[24];
		for ( var plane = 0; plane < 2; plane++ )
		{
			var side = plane == 0 ? Vector3.Left : Vector3.Up;
			var normal = plane == 0 ? Vector3.Up : Vector3.Left;
			var offset = plane * 4;
			vertices[offset] = MakeVertex( side * -0.5f, normal, new Vector2( 0, 0 ) );
			vertices[offset + 1] = MakeVertex( Vector3.Forward + side * -0.5f, normal, new Vector2( 1, 0 ) );
			vertices[offset + 2] = MakeVertex( Vector3.Forward + side * 0.5f, normal, new Vector2( 1, 1 ) );
			vertices[offset + 3] = MakeVertex( side * 0.5f, normal, new Vector2( 0, 1 ) );
			var index = plane * 12;
			indices[index] = offset; indices[index + 1] = offset + 1; indices[index + 2] = offset + 2;
			indices[index + 3] = offset; indices[index + 4] = offset + 2; indices[index + 5] = offset + 3;
			indices[index + 6] = offset + 2; indices[index + 7] = offset + 1; indices[index + 8] = offset;
			indices[index + 9] = offset + 3; indices[index + 10] = offset + 2; indices[index + 11] = offset;
		}
		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Length, vertices );
		mesh.CreateIndexBuffer( indices.Length, indices );
		mesh.Bounds = new BBox( new Vector3( 0, -0.5f, -0.5f ), new Vector3( 1, 0.5f, 0.5f ) );
		return Model.Builder.WithName( name ).AddMesh( mesh ).Create();
	}

	private static Vertex MakeVertex( Vector3 position, Vector3 normal, Vector2 uv ) =>
		new( position, normal, Vector3.Forward, new Vector4( uv.x, uv.y, 0, 0 ) );
}
