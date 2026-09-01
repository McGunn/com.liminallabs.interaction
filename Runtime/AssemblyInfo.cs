using System.Runtime.CompilerServices;

// The tests drive the interactor a frame at a time through an internal entry point, so the
// pipeline can be pinned without play mode.
[assembly: InternalsVisibleTo("LiminalLabs.Interaction.Tests")]
