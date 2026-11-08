using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Brings a compiled shader (<c>.shader_c</c>) that an older engine made up to date, for the shaders of a sent that
/// <c>sent_assets</c> copies into the project. The engine does this itself for a package it installs
/// (<c>Sandbox.Engine.Shaders.ShaderUpgrade</c>: an outdated shader won't load, so it is recompiled from the source
/// embedded in it against today's includes), but a sent we integrate is plain files in <c>Assets/</c>, never a mounted
/// package, so nothing upgraded them and every car's paint, glass and lamps drew as a red checker after an engine update.
/// <para>
/// <c>ShaderUpgrade</c> is internal, so it is reached by reflection: if the engine renames something here, the call
/// logs that it could not be reached and leaves the file as it was, it never throws.
/// </para>
/// <c>shader_upgrade</c> runs it over every <c>.shader_c</c> in the project that has no <c>.shader</c> beside it.
/// </summary>
public static class ShaderUpgrades
{
	const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	static Type upgrade;
	static Type candidateType;
	static Type progressType;
	static MethodInfo getState;
	static MethodInfo getJob;
	static MethodInfo compile;
	static object current;
	static bool resolved;

	// What the engine's ShaderUpgrade is made of, found once; false if any piece is missing.
	static bool Resolve()
	{
		if ( resolved )
			return upgrade is not null;

		resolved = true;

		try
		{
			upgrade = typeof( Shader ).Assembly.GetType( "Sandbox.Engine.Shaders.ShaderUpgrade" );
			candidateType = upgrade?.GetNestedType( "Candidate", Any );
			progressType = upgrade?.GetNestedType( "Progress", Any );
			var versions = upgrade?.GetNestedType( "Versions", Any );

			current = versions?.GetProperty( "Current", Any )?.GetValue( null );
			getState = upgrade?.GetMethods( Any ).FirstOrDefault( x => x.Name == "GetState"
				&& x.GetParameters()[0].ParameterType == typeof( string ) );
			getJob = upgrade?.GetMethod( "GetJob", Any );
			compile = upgrade?.GetMethod( "CompileAsync", Any );

			if ( current is null || getState is null || getJob is null || compile is null
			     || candidateType is null || progressType is null )
				upgrade = null;
		}
		catch ( Exception e )
		{
			Log.Warning( $"ShaderUpgrades: could not reach the engine's ShaderUpgrade: {e.Message}" );
			upgrade = null;
		}

		if ( upgrade is null )
			Log.Warning( "ShaderUpgrades: the engine's ShaderUpgrade is not where it was; shaders are left as they are." );

		return upgrade is not null;
	}

	/// <summary>
	/// True if the engine can load this compiled shader as it is. Unknown (the upgrade is out of reach) counts as true,
	/// so nothing is rewritten on a guess.
	/// </summary>
	public static bool IsCurrent( string file )
	{
		if ( !Resolve() || !File.Exists( file ) )
			return true;

		try
		{
			return getState.Invoke( null, [file, current] )?.ToString() == "Current";
		}
		catch ( Exception )
		{
			return true;
		}
	}

	/// <summary>
	/// Recompiles an outdated shader and puts the result over the file. <paramref name="path"/> is its path in the
	/// project's assets (<c>shaders/drift_glass.shader_c</c>). True if the file now loads; false if it could not be
	/// upgraded (the engine's reason is in the log) and was left as it was.
	/// </summary>
	public static async Task<bool> UpgradeAsync( string file, string path )
	{
		if ( IsCurrent( file ) )
			return true;

		try
		{
			var candidate = Activator.CreateInstance( candidateType, path, file.Replace( '\\', '/' ) );
			var job = getJob.Invoke( null, [candidate, current] );
			var progress = Activator.CreateInstance( progressType, Any, null, [1L], null );
			var task = (Task)compile.Invoke( null, [job, progress, CancellationToken.None] );

			await task;

			var result = task.GetType().GetProperty( "Result" )?.GetValue( task ) as string;

			if ( result is null || !File.Exists( result ) )
			{
				Log.Warning( $"ShaderUpgrades: {path} is outdated and could not be recompiled (no source in it, or it does not "
				             + "compile against today's includes). It will not load: rebuild a .shader from it by hand." );
				return false;
			}

			File.Copy( result, file, true );
			Log.Info( $"ShaderUpgrades: {path} was made by an older engine; recompiled from the source inside it." );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"ShaderUpgrades: {path}: {e.Message}" );
			return false;
		}
	}

	[ConCmd( "shader_upgrade" )]
	public static void Run() => _ = UpgradeProjectAsync();

	/// <summary>
	/// Upgrades every compiled shader in the project's assets that has no <c>.shader</c> source (a shader with source is
	/// recompiled by the editor itself). Returns how many were upgraded.
	/// </summary>
	public static async Task<int> UpgradeProjectAsync()
	{
		var assets = Path.Combine( Project.Current.GetRootPath(), "Assets" );
		var upgraded = 0;
		var failed = 0;

		foreach ( var file in Directory.EnumerateFiles( assets, "*.shader_c", SearchOption.AllDirectories ).ToList() )
		{
			if ( File.Exists( file[..^2] ) || IsCurrent( file ) )
				continue;

			var path = Path.GetRelativePath( assets, file ).Replace( '\\', '/' );

			if ( await UpgradeAsync( file, path ) )
				upgraded++;
			else
				failed++;
		}

		Log.Info( $"shader_upgrade: {upgraded} compiled shader(s) upgraded, {failed} could not be." );
		return upgraded;
	}
}
