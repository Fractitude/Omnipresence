namespace Sandbox;

/// <summary>
/// Which abilities sit in which hotbar slot, on this computer, and when each can next be used. Saved to the local data
/// folder (<c>abilities.json</c>): the layout is the player's own setting, so it never goes to the host.
/// </summary>
/// <remarks>
/// Not <c>readonly</c>: a hot reload would reset these (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class AbilityLoadout
{
	public const int SlotCount = 10;

	const string FileName = "abilities.json";

	static string[] slots = new string[SlotCount];
	static Dictionary<string, float> readyAt = new();
	static bool loaded;

	/// <summary>
	/// The id of the ability being dragged from the menu, or null.
	/// </summary>
	public static string Dragging { get; set; }

	/// <summary>
	/// The slot the dragged ability came from, or -1 when it comes from the menu.
	/// </summary>
	public static int DragFrom { get; set; } = -1;

	/// <summary>
	/// Start dragging an ability, from a hotbar slot or, with -1, from the menu.
	/// </summary>
	public static void BeginDrag( Ability ability, int from )
	{
		Dragging = ability?.Id;
		DragFrom = from;
	}

	/// <summary>
	/// End the drag over a slot, or over none (-1). From the menu it fills the slot; from the bar it swaps with the
	/// slot, or empties the one it came from when dropped outside the bar.
	/// </summary>
	public static void Drop( int slot )
	{
		var ability = AbilityCatalog.Find( Dragging );
		var from = DragFrom;

		Dragging = null;
		DragFrom = -1;

		if ( ability is null )
			return;

		if ( from < 0 )
		{
			if ( slot >= 0 )
				Set( slot, ability );
		}
		else if ( slot < 0 )
		{
			Clear( from );
		}
		else if ( slot != from )
		{
			Swap( from, slot );
		}
	}

	static void Swap( int a, int b )
	{
		Load();
		(slots[a], slots[b]) = (slots[b], slots[a]);
		Changed();
	}

	/// <summary>
	/// Bumps whenever a slot changes, so a panel knows to redraw.
	/// </summary>
	public static int Version { get; private set; }

	/// <summary>
	/// The ability in a slot, or null if it is empty.
	/// </summary>
	public static Ability At( int slot )
	{
		Load();
		return slot is >= 0 and < SlotCount ? AbilityCatalog.Find( slots[slot] ) : null;
	}

	/// <summary>
	/// Put an ability in a slot. It leaves any other slot it was in, so it is on the bar once.
	/// </summary>
	public static void Set( int slot, Ability ability )
	{
		Load();

		if ( ability is null || slot is < 0 or >= SlotCount )
			return;

		for ( var i = 0; i < SlotCount; i++ )
		{
			if ( slots[i] == ability.Id )
				slots[i] = null;
		}

		slots[slot] = ability.Id;
		Changed();
	}

	public static void Clear( int slot )
	{
		Load();

		if ( slot is < 0 or >= SlotCount || slots[slot] is null )
			return;

		slots[slot] = null;
		Changed();
	}

	public static bool IsSlotted( Ability ability )
	{
		Load();
		return ability is not null && slots.Contains( ability.Id );
	}

	/// <summary>
	/// Seconds until the ability can be used, or 0.
	/// </summary>
	public static float CooldownLeft( Ability ability )
	{
		if ( ability is null || !readyAt.TryGetValue( ability.Id, out var at ) )
			return 0f;

		return System.MathF.Max( at - Time.Now, 0f );
	}

	/// <summary>
	/// How much of the cooldown is left, from 1 down to 0.
	/// </summary>
	public static float CooldownFraction( Ability ability )
	{
		return ability is { Cooldown: > 0f } ? CooldownLeft( ability ) / ability.Cooldown : 0f;
	}

	/// <summary>
	/// Use the ability in a slot. Returns whether it was used: not from an empty slot, not while it cools down.
	/// </summary>
	public static bool TryActivate( int slot )
	{
		var ability = At( slot );

		if ( ability is null || CooldownLeft( ability ) > 0f )
			return false;

		if ( ability.Cooldown > 0f )
			readyAt[ability.Id] = Time.Now + ability.Cooldown;

		ability.Use?.Invoke( PlayerProgress.Local?.GameObject );
		return true;
	}

	/// <summary>
	/// The key that uses a slot, as the player reads it: 1 to 9, then 0.
	/// </summary>
	public static string KeyLabel( int slot ) => ((slot + 1) % 10).ToString();

	public static string ActionName( int slot ) => $"Slot{(slot + 1) % 10}";

	static void Changed()
	{
		Version++;

		try
		{
			FileSystem.Data.WriteJson( FileName, slots );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"AbilityLoadout: couldn't save the hotbar: {e.Message}" );
		}
	}

	static void Load()
	{
		if ( loaded )
			return;

		loaded = true;

		try
		{
			var saved = FileSystem.Data.ReadJsonOrDefault<string[]>( FileName, null );

			for ( var i = 0; i < SlotCount && saved is not null && i < saved.Length; i++ )
				slots[i] = saved[i];
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"AbilityLoadout: couldn't read the hotbar: {e.Message}" );
		}
	}
}
