using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// <c>sent_test org.package</c>: spawns a sent in the game that is playing and watches it for a few seconds, then says
/// whether it works: did it spawn, are any of its components missing, and did anything throw or log an error. It is what
/// one does by hand after <c>sent_import</c>, in one command.
/// </summary>
public static class SentTest
{
	/// <summary>
	/// Log lines that are not about the sent.
	/// </summary>
	static readonly string[] Noise = ["Texture", "Vulkan", "NavMesh", "Added new", "joined the game", "AssetSystem"];

	[ConCmd( "sent_test" )]
	public static void Test( string ident, string seconds = "10" ) => _ = TestAsync( ident?.Trim().ToLowerInvariant(), seconds );

	static async Task TestAsync( string ident, string seconds )
	{
		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_test: give a cloud ident, e.g. sent_test hl2npcs.metrocop" );
			return;
		}

		if ( !Game.IsPlaying || !Game.ActiveScene.IsValid() )
		{
			Log.Warning( "sent_test: start the game first (Play), the sent is spawned in it." );
			return;
		}

		var scene = Game.ActiveScene;
		var wait = int.TryParse( seconds, out var s ) ? Math.Clamp( s, 3, 60 ) : 10;
		var logFile = Path.Combine( Path.GetDirectoryName( Environment.ProcessPath ), "logs", "sbox-dev.log" );
		var from = LogLength( logFile );
		var missingBefore = scene.GetAllComponents<MissingComponent>().Count();

		ConsoleSystem.Run( $"spawn_sent {ident}" );
		await Task.Delay( wait * 1000 );

		var lines = NewLines( logFile, from );
		var spawned = lines.Any( x => x.Contains( "spawn_sent:" ) && x.Contains( " spawned " ) );
		var missing = Game.ActiveScene.IsValid() ? Game.ActiveScene.GetAllComponents<MissingComponent>().Count() - missingBefore : 0;

		var problems = lines
			.Where( x => x.Length > 24 && char.IsDigit( x[0] ) )
			.Where( IsProblem )
			.Where( x => !x.Contains( "spawn_sent: too fast" ) && !Noise.Any( x.Contains ) )
			.GroupBy( x => x[24..Math.Min( x.Length, 200 )] )
			.OrderByDescending( x => x.Count() )
			.Take( 8 )
			.Select( x => $"{x.Count()} x {x.Key.Trim()}" )
			.ToList();

		var ok = spawned && missing == 0 && problems.Count == 0;
		var report = new StringBuilder( $"sent_test: {ident}: {(ok ? "OK" : "PROBLEMS")}. " )
			.Append( spawned ? "Spawned" : "Did not spawn" )
			.Append( $", {missing} missing component(s), watched {wait} s." );

		foreach ( var problem in problems )
			report.Append( "\n    " ).Append( problem );

		if ( ok )
			Log.Info( report.ToString() );
		else
			Log.Warning( report.ToString() );
	}

	// The log file does not say which lines were warnings, so go by what they say.
	static bool IsProblem( string line ) =>
		new[] { "Exception", "Error", "failed", "Failed", "Missing", "not found", "could not", "Couldn't", "not a sent", "too fast" }
			.Any( line.Contains )
		|| line.Contains( "spawn_sent:" ) && !line.Contains( " spawned " );

	static long LogLength( string file )
	{
		using var stream = new FileStream( file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
		return stream.Length;
	}

	// What the game wrote to its log since <from>. The file is open in the game, so it is read without locking it.
	static List<string> NewLines( string file, long from )
	{
		using var stream = new FileStream( file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
		stream.Seek( Math.Min( from, stream.Length ), SeekOrigin.Begin );

		using var reader = new StreamReader( stream, Encoding.UTF8 );
		return reader.ReadToEnd().Split( '\n' ).Select( x => x.TrimEnd( '\r' ) ).ToList();
	}
}
