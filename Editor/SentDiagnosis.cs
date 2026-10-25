using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Sandbox;

/// <summary>
/// What an import leaves behind that does not compile, said in terms of what to do about it. After <c>sent_import</c>
/// writes files it waits for the game to build, takes the errors in the imported folder and, for each type the code
/// uses that this game does not have, looks it up in the sandbox game's source (a clone of
/// <c>github.com/Facepunch/sandbox</c>, <see cref="SandboxClone"/>) and names the file to port it from.
/// </summary>
public static class SentDiagnosis
{
	/// <summary>
	/// Where the sandbox game's source is cloned, to look up the types an import needs.
	/// </summary>
	[ConVar( "omni.sandbox_clone", ConVarFlags.Saved )]
	public static string SandboxClone { get; set; } = @"C:\Users\benji\Projects\S&box\sandbox";

	static readonly Regex MissingType = new( @"type or namespace name '(\w+)' could not be found" );
	static readonly Regex MissingMember = new( @"'([\w.]+)' does not contain a definition for '(\w+)'" );
	static readonly Regex NoOverride = new( @"'([\w.]+)\.(\w+)\(.*\)': no suitable method found to override" );

	/// <summary>
	/// One compile error in the imported folder.
	/// </summary>
	public sealed record Problem( string Id, string Message, string File, int Line );

	/// <summary>
	/// Waits for the build that follows a change to the code, then returns the errors in <paramref name="folder"/>.
	/// Null if the build did not finish in time.
	/// </summary>
	public static async Task<List<Problem>> WaitForErrors( string folder, int seconds = 90 )
	{
		var group = Project.CompileGroup;

		if ( group is null )
			return null;

		// The editor notices new files a moment after they are written, so a build that has not started yet looks like
		// one that is done: wait to see one start, and if none does (nothing needed building) go on.
		var started = false;

		for ( var i = 0; i < 10 && !started; i++ )
		{
			await Task.Delay( 500 );
			started = group.IsBuilding || group.Compilers.Any( x => x.IsBuilding || x.NeedsBuild );
		}

		for ( var i = 0; i < seconds; i++ )
		{
			if ( !group.IsBuilding && !group.Compilers.Any( x => x.IsBuilding || x.NeedsBuild ) )
			{
				return group.Compilers
					.SelectMany( x => x.Diagnostics )
					.Where( x => x.Severity == DiagnosticSeverity.Error )
					.Select( x => (Diagnostic: x, Span: x.Location.GetLineSpan()) )
					.Where( x => x.Span.Path?.StartsWith( folder, StringComparison.OrdinalIgnoreCase ) == true )
					.Select( x => new Problem( x.Diagnostic.Id, x.Diagnostic.GetMessage(), x.Span.Path, x.Span.StartLinePosition.Line + 1 ) )
					.ToList();
			}

			await Task.Delay( 1000 );
		}

		return null;
	}

	/// <summary>
	/// The lines of a report on <paramref name="problems"/>: each kind of error once, with how many there are and what
	/// to do.
	/// </summary>
	public static List<string> Report( List<Problem> problems )
	{
		var lines = new List<string>();

		foreach ( var group in problems.GroupBy( x => Generalise( x.Message ) ).OrderByDescending( x => x.Count() ) )
		{
			var first = group.First();
			var advice = AdviceFor( first.Message );
			lines.Add( $"{group.Count()} x {first.Id} {first.Message} ({Path.GetFileName( first.File )}:{first.Line})"
			           + (advice is null ? "" : $"\n    -> {advice}") );
		}

		return lines;
	}

	// Errors that differ only in the place they are at are one kind.
	static string Generalise( string message ) => Regex.Replace( message, @"\(are you missing.*", "" ).Trim();

	static string AdviceFor( string message )
	{
		if ( MissingType.Match( message ) is { Success: true } type )
			return Upstream( type.Groups[1].Value );

		if ( MissingMember.Match( message ) is { Success: true } member )
		{
			var typeName = member.Groups[1].Value.Split( '.' ).Last();
			return $"{typeName} exists here without {member.Groups[2].Value}: add the member, or change the call. "
			       + (Upstream( typeName, mentionMissing: false ) ?? "");
		}

		if ( NoOverride.Match( message ) is { Success: true } hook )
			return $"{hook.Groups[2].Value} is a hook of a sandbox game base class that was not ported (in {hook.Groups[1].Value}): "
			       + "drop the override, or override what the base here has instead.";

		return null;
	}

	/// <summary>
	/// Where the sandbox game defines <paramref name="name"/>, if its source is there.
	/// </summary>
	static string Upstream( string name, bool mentionMissing = true )
	{
		var code = Path.Combine( SandboxClone ?? "", "Code" );

		if ( !Directory.Exists( code ) )
			return mentionMissing ? $"{name} is not in this game (omni.sandbox_clone does not point at the sandbox source, so it was not looked up)." : null;

		var pattern = new Regex( $@"\b(class|struct|enum|interface|record)\s+{Regex.Escape( name )}\b" );

		foreach ( var file in Directory.EnumerateFiles( code, "*.*", SearchOption.AllDirectories )
			         .Where( x => x.EndsWith( ".cs" ) || x.EndsWith( ".razor" ) ) )
		{
			if ( pattern.IsMatch( File.ReadAllText( file ) ) )
				return $"{(mentionMissing ? $"{name} is not in this game. " : "")}The sandbox game has it in {Path.GetRelativePath( SandboxClone, file )}: port it, or its use here.";
		}

		return mentionMissing ? $"{name} is not in this game, and not in the sandbox source either: it is the sent's own, or an engine type from a newer version." : null;
	}
}
