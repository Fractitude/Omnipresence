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

	/// <summary>
	/// The Steam id of every player that has a record. For finding the ones that have work waiting when the index of
	/// those (<see cref="LoadPendingAsync"/>) is missing.
	/// </summary>
	Task<IReadOnlyList<ulong>> ListAsync();

	/// <summary>
	/// The ids that had work waiting on the clock when the index was last saved, or null when there is no index (yet).
	/// </summary>
	Task<IReadOnlyList<ulong>> LoadPendingAsync();

	Task SavePendingAsync( IReadOnlyCollection<ulong> steamIds );
}
