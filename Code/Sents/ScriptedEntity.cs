namespace Sandbox;

/// <summary>
/// A sent: a prefab with a title, published to the cloud as a package of type <c>sent</c>. Ported from Facepunch's
/// sandbox game (<c>ScriptedEntity</c>); the class name and the <c>sent</c> extension must stay the same, because the
/// packages on the cloud are made of this type. <see cref="Sents"/> spawns them.
/// </summary>
[AssetType( Name = "Sandbox Entity", Extension = "sent", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class ScriptedEntity : GameResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property]
	public string Category { get; set; }

	/// <summary>
	/// Set by the author when the entity brings its own code, which then runs in this game.
	/// </summary>
	[Property]
	public bool IncludeCode { get; set; }

	[Property]
	public bool Developer { get; set; }
}
