namespace Sandbox;

/// <summary>
/// An item lying in the world: what <see cref="PlayerInventory.Drop"/> makes, and what the player picks up again. It
/// carries the whole <see cref="ItemData"/> as synced JSON, so kind, count, quality, name, text, paths, order, addressee
/// and a parcel's contents come back exactly as they left. Its prefab (see <see cref="ItemDef.WorldPrefab"/>) has a
/// collider, a rigidbody so it falls and rests, a network object and an <see cref="Interactable"/> with a Pick up option.
/// <para>
/// The host runs the pickup: it checks the interactor's reach (the Interactable does), fits as much as it can into the
/// bag (<see cref="PlayerInventory.AddPartial"/>), and either destroys the object, or keeps it with the remainder. World
/// items are kept in the world file (<see cref="WorldItemRegistry"/>) and made again in the next session.
/// </para>
/// </summary>
public sealed class WorldItem : Component, IInteractionHandler
{
	/// <summary>
	/// The <see cref="InteractionOption.Id"/> of the Pick up option.
	/// </summary>
	public const string PickupId = "pickup";

	/// <summary>
	/// The most world items a scene holds. A drop is refused beyond it, so nobody can fill the map with them.
	/// </summary>
	public const int MaxInScene = 300;

	/// <summary>
	/// The item as JSON, set by the host. Read it through <see cref="Data"/>.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	string ItemJson { get; set; } = "";

	ItemData data;
	string parsedFrom;
	bool taken;
	bool tinted;

	/// <summary>
	/// What this lies as, or null if it carries nothing yet. Changing the returned object does not sync: use
	/// <see cref="Set"/>.
	/// </summary>
	public ItemData Data
	{
		get
		{
			if ( parsedFrom != ItemJson )
			{
				parsedFrom = ItemJson;
				data = string.IsNullOrEmpty( ItemJson ) ? null : Json.Deserialize<ItemData>( ItemJson );
			}

			return data;
		}
	}

	/// <summary>
	/// Put an item in it (a copy is kept). Host only.
	/// </summary>
	public void Set( ItemData item )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		ItemJson = Json.Serialize( item );
		GameObject.Name = $"World item: {item.Name}";
	}

	/// <summary>
	/// How many world items the scene has.
	/// </summary>
	public static int CountInScene( Scene scene ) => scene.GetAllComponents<WorldItem>().Count();

	/// <summary>
	/// Make the world item for <paramref name="item"/> in front of a player and toss it a little: a trace from their
	/// eye forward with a sphere keeps it out of a wall and out of the player. Returns null when it cannot be made.
	/// Host only.
	/// </summary>
	public static GameObject SpawnFrom( GameObject player, ItemData item )
	{
		var controller = player.Components.Get<PlayerController>();
		var eye = controller.IsValid() ? controller.EyePosition : player.WorldPosition + Vector3.Up * 64f;
		var angles = controller.IsValid() ? controller.EyeAngles : player.WorldRotation.Angles();
		var forward = angles.ToRotation().Forward;

		var trace = player.Scene.Trace.Sphere( 8f, eye, eye + forward * 60f )
			.IgnoreGameObjectHierarchy( player )
			.WithoutTags( "trigger" )
			.Run();

		var velocity = trace.Hit ? Vector3.Zero : forward * 140f + Vector3.Up * 60f;
		return Spawn( item, trace.EndPosition, Rotation.FromYaw( angles.yaw ), velocity );
	}

	/// <summary>
	/// Make a world item for <paramref name="item"/> (a copy is kept) at a place and network-spawn it. A kind uses its
	/// own prefab; an ordinary item that came from a prefab with a <see cref="PickupItem"/> drops as that prefab; the rest
	/// use the shared prefab. Returns null when nothing could be made. The object is tracked by the
	/// <see cref="WorldItemRegistry"/> so it is saved; a restored item passes the id it was saved under. Host only.
	/// </summary>
	public static GameObject Spawn( ItemData item, Vector3 position, Rotation rotation, Vector3 velocity,
		string restoreId = null )
	{
		if ( item is null || item.Count < 1 || Networking.IsActive && !Networking.IsHost )
			return null;

		var transform = new Transform( position, rotation );

		// An ordinary item with a prefab of its own comes back as that prefab, with the PickupItem its scene users know.
		if ( !item.IsBatch && !string.IsNullOrEmpty( item.PrefabPath ) && MakeOrdinary( item, transform ) is { } own )
		{
			Launch( own, velocity );
			WorldItemRegistry.Track( own, item, item.PrefabPath, restoreId );
			return own;
		}

		var prefab = ItemCatalog.WorldPrefabOf( item.Kind );
		var go = MakeWorld( prefab, transform, out var world );

		if ( go is null )
		{
			prefab = ItemCatalog.WorldFallbackPrefab;
			go = MakeWorld( prefab, transform, out world );
		}

		if ( go is null )
			return null;

		world.Set( item );
		go.NetworkSpawn();
		Launch( go, velocity );
		WorldItemRegistry.Track( go, item, prefab, restoreId );
		return go;
	}

	static GameObject MakeWorld( string prefab, Transform transform, out WorldItem world )
	{
		world = null;
		var go = CloneOrNull( prefab, transform, true );

		if ( !go.IsValid() )
			return null;

		world = go.Components.Get<WorldItem>( FindMode.EverythingInSelfAndDescendants );

		if ( world.IsValid() )
			return go;

		go.Destroy();
		return null;
	}

	static GameObject MakeOrdinary( ItemData item, Transform transform )
	{
		var go = CloneOrNull( item.PrefabPath, transform, false );

		if ( !go.IsValid() )
			return null;

		if ( go.Components.Get<PickupItem>( FindMode.EverythingInSelfAndDescendants ) is null
		     || go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) is null
		     || go.NetworkMode != NetworkMode.Object )
		{
			go.Destroy();
			return null;
		}

		go.Enabled = true;
		go.NetworkSpawn();
		return go;
	}

	static GameObject CloneOrNull( string prefab, Transform transform, bool enabled )
	{
		try
		{
			return GameObject.Clone( prefab, transform, null, enabled );
		}
		catch ( System.Exception )
		{
			return null;
		}
	}

	static void Launch( GameObject go, Vector3 velocity )
	{
		if ( velocity != Vector3.Zero
		     && go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) is { } body )
			body.Velocity = velocity;
	}

	protected override void OnStart()
	{
		InteractionSetup.EnsureOption( this, PickupId, "Pick up", "back_hand" );
	}

	protected override void OnUpdate()
	{
		// A kind without a model of its own is told apart by its colour.
		if ( tinted || Data is not { } item )
			return;

		tinted = true;

		if ( ItemCatalog.ColorFor( item ) is { Length: > 0 } color && Color.TryParse( color, out var tint )
		     && Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) is { } renderer )
			renderer.Tint = tint;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != PickupId || Data is not { } item )
			return null;

		if ( item.Kind == ItemCatalog.Cash )
			return DescribeCash( e, item );

		var title = item.Count > 1 ? $"Pick up {item.Name} x{item.Count}" : $"Pick up {item.Name}";
		var details = item.IsBatch || string.IsNullOrEmpty( item.Description )
			? ItemCatalog.Describe( item )
			: item.Description;

		if ( e.Interactor?.Components.Get<PlayerInventory>() is not { } inventory )
			return new InteractionInfo( title, "back_hand", details );

		var room = inventory.RoomFor( item );

		if ( room < 1 )
			return new InteractionInfo( title, "back_hand", "Your bag is full.", false );

		return room < item.Count
			? new InteractionInfo( title, "back_hand", $"{details}\nOnly {room} fit in your bag." )
			: new InteractionInfo( title, "back_hand", details );
	}

	/// <summary>
	/// The prompt for a pile of cash: it goes in the wallet, not the bag, so only the wallet's cap can stop it.
	/// </summary>
	static InteractionInfo DescribeCash( InteractionEvent e, ItemData item )
	{
		var title = $"Pick up {CashRules.Format( item.Amount )}";

		if ( e.Interactor?.Components.Get<PlayerWallet>() is not { } wallet )
			return new InteractionInfo( title, "payments", "Cash" );

		var (taken, left) = CashRules.Pickup( item.Amount, wallet.Cash );

		if ( taken < 1 )
			return new InteractionInfo( title, "payments", "Your wallet is full.", false );

		return left > 0
			? new InteractionInfo( title, "payments", $"Only {CashRules.Format( taken )} fit in your wallet." )
			: new InteractionInfo( title, "payments", "Cash" );
	}

	bool IInteractionHandler.CanInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != PickupId || Data is not { } item )
			return true;

		// Cash goes in the wallet, so only its cap can refuse.
		if ( item.Kind == ItemCatalog.Cash )
			return e.Interactor?.Components.Get<PlayerWallet>() is not { } wallet
			       || CashRules.Pickup( item.Amount, wallet.Cash ).Taken > 0;

		return e.Interactor?.Components.Get<PlayerInventory>() is not { } inventory || inventory.RoomFor( item ) > 0;
	}

	/// <summary>
	/// A pile of cash goes into the interactor's wallet (as much as the cap allows), not their bag. All of it: the pile
	/// is taken and destroyed. Part of it: the pile stays with what did not fit. Host only.
	/// </summary>
	void PickUpCash( InteractionEvent e, ItemData item )
	{
		var crime = e.Interactor.Components.Get<PlayerCrime>();

		if ( e.Interactor.Components.Get<PlayerWallet>() is not { } wallet )
			return;

		var total = item.Amount;
		var got = CashDrops.Pickup( wallet, item );

		if ( got < 1 )
		{
			crime?.Tell( CrimeResult.Fail( "Your wallet is full." ) );
			return;
		}

		if ( got >= total )
		{
			// Marked before it is destroyed, so a second pickup in the same frame finds nothing left.
			taken = true;
			WorldItemRegistry.Forget( GameObject );
			GameObject.Destroy();
			WorldItemRegistry.Flush();
			crime?.Tell( CrimeResult.Success( $"Picked up {CashRules.Format( total )}." ) );
			return;
		}

		var rest = item.Clone();
		rest.Amount = total - got;
		Set( rest );
		WorldItemRegistry.SetItem( GameObject, rest );
		WorldItemRegistry.Flush();
		crime?.Tell( CrimeResult.Success(
			$"Picked up {CashRules.Format( got )} of {CashRules.Format( total )}. {CashRules.Format( rest.Amount )} left on the ground." ) );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != PickupId || taken || !GameObject.IsValid() || Data is not { } item )
			return;

		if ( item.Kind == ItemCatalog.Cash )
		{
			PickUpCash( e, item );
			return;
		}

		if ( e.Interactor?.Components.Get<PlayerInventory>() is not { } inventory )
			return;

		var crime = e.Interactor.Components.Get<PlayerCrime>();
		var got = inventory.AddPartial( item );

		if ( got < 1 )
		{
			crime?.Tell( CrimeResult.Fail( inventory.CountOf( item.Kind ) > 0 ? "Nothing more fits." : "Your bag is full." ) );
			return;
		}

		var total = item.Count;

		if ( got >= total )
		{
			// Marked before it is destroyed, so a second pickup in the same frame finds nothing left.
			taken = true;
			WorldItemRegistry.Forget( GameObject );
			GameObject.Destroy();
			crime?.Tell( CrimeResult.Success( total > 1 ? $"Picked up {total} {item.Name}." : $"Picked up {item.Name}." ) );
			return;
		}

		var rest = item.Clone();
		rest.Count = total - got;
		Set( rest );
		WorldItemRegistry.SetItem( GameObject, rest );
		crime?.Tell( CrimeResult.Success( $"Picked up {got} of {total} {item.Name}. {rest.Count} left on the ground." ) );
	}
}
