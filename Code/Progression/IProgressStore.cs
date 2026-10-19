using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Where player progress is kept. The only part of the game that knows: swap the store and nothing else changes.
/// </summary>
public interface IProgressStore
{
	/// <summary>
	/// The saved record, or null for a player with none yet. Throws when a record exists but can't be read: the
	/// caller must not take that for a new player, or the next save would overwrite the real record.
	/// </summary>
	Task<ProgressRecord> LoadAsync( ulong steamId );

	Task SaveAsync( ulong steamId, ProgressRecord record );
}
