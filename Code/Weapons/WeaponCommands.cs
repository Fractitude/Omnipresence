namespace Sandbox;

public static class WeaponCommands
{
	/// <summary>
	/// Gives you a weapon by its short name, like <c>give physgun</c>.
	/// </summary>
	[ConCmd( "give" )]
	public static void Give( string weapon )
	{
		var inventory = Game.ActiveScene?.GetAllComponents<WeaponInventory>().FirstOrDefault( x => !x.IsProxy );
		if ( !inventory.IsValid() )
		{
			Log.Warning( "give: no player" );
			return;
		}

		inventory.Pickup( WeaponInventory.PrefabPath( weapon ) );
	}
}
