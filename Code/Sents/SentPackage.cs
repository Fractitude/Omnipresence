namespace Sandbox;

/// <summary>
/// Marks the code of an imported sent: which package it came from and which namespaces its source declares.
/// <c>sent_import</c> writes one beside every import (<c>SentPackage.cs</c>), so nothing here is kept by hand.
/// <para>
/// In the sandbox game every sent is its own package, so several can each have a <c>CombineNpc</c>. Imported, they
/// share this game's one assembly, and a prefab that names a component by its plain name (<c>"CombineNpc"</c>) would
/// find only one of them. <see cref="SentBuilder"/> asks <see cref="SentPackages"/> for the namespaces of the sent it
/// is building and names each component by its full type.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Class )]
public sealed class SentPackageAttribute : Attribute
{
	/// <summary>
	/// The cloud ident of the package, <c>org.package</c>.
	/// </summary>
	public string Ident { get; }

	/// <summary>
	/// The namespaces the package's source declares. Empty for a sent whose code is all in the global namespace.
	/// </summary>
	public string[] Namespaces { get; }

	public SentPackageAttribute( string ident, params string[] namespaces )
	{
		Ident = ident;
		Namespaces = namespaces;
	}
}

/// <summary>
/// What the game knows about the sents whose code was imported.
/// </summary>
public static class SentPackages
{
	/// <summary>
	/// Whether the code of this sent was imported (<c>sent_import</c>).
	/// </summary>
	public static bool IsImported( string ident ) => Find( ident ) is not null;

	/// <summary>
	/// The namespaces of an imported sent's code, none for a sent that was not imported or has no namespace.
	/// </summary>
	public static IReadOnlyList<string> NamespacesOf( string ident ) => Find( ident )?.Namespaces ?? [];

	static SentPackageAttribute Find( string ident ) =>
		TypeLibrary.GetTypesWithAttribute<SentPackageAttribute>()
			.Select( x => x.Attribute )
			.FirstOrDefault( x => string.Equals( x.Ident, ident, StringComparison.OrdinalIgnoreCase ) );
}
