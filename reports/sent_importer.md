# Sent importer: assets, removal and update

## What was added

- **Remove from project** (`sent_remove`, `Editor/SentRemover.cs`): deletes a sent's prefab folder and its code folder. A code file stays if another import's package needs it and that import's folder lacks it (a shared library, imported once into the first sent's folder).
- **Update imported code** (`sent_import <ident> update`): a menu entry for the existing command.
- **Assets in the project** (`sent_assets`, `Editor/SentAssets.cs`, run by `sent_integrate`): copies a sent's compiled models, materials, textures, sounds and shaders into `Assets/`, so it no longer needs the workshop.

## What went wrong, and why

| Problem | Cause | Fix |
| --- | --- | --- |
| Removal broke the build | The first version guessed which code was shared from type names, and deleted an extension-method file | The test now uses what each other import's package left out |
| Removed sent still showed as integrated | The resource library keeps a deleted prefab until restart | `SentLocal` remembers removals |
| Update broke the build | Files moved within a sent's folder, and a razor panel the game already has, were written again as new | Update matches by file name inside the folder and skips files the game has |
| Cars had no models | Assets came from the workshop, mounted at spawn | Assets are copied into the project |
| Textures and shaders missing | The download folder lacks many textures; a name filter skipped the package's own shaders and default textures | Files are read from the mounted package; only what the engine's `core/` has is skipped |
| Random noise on large textures | The mounted copy of a texture is only its resident part, up to 256 px. The top mips are streamed from the CDN, so a project copy drew them from uninitialised GPU memory | The whole file is taken from `download/web-textures/<hash>.vtex_c.resident` |

Two earlier attempts also copied the engine's own default textures into `Assets/`, which probably caused a GPU crash. The tool now skips anything in the engine's `core/` folder.

## Result

All 12 integrated sents have their models, shaders and full-resolution textures in the project, and all cars render correctly.

## Limits

- The engine has the whole texture file only once it has streamed that texture. If it hasn't, the tool warns that the top mip is missing. Spawn the sent once, then run `sent_assets <ident>` again.
- That warning can fire for a large texture that is already complete.
- Only the Drift District cars, the metrocop and gmk.aura were tested.
