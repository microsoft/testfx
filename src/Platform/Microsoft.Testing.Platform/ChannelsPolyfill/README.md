# ChannelsPolyfill

This folder copies the channels implementation from dotnet/runtime to bring it for netstandard2.0 without us adding extra dependencies.

The implementation was copied from https://github.com/dotnet/runtime/tree/3345865f57f59f4a54e1749c06f5a2782d961a16/src/libraries/System.Threading.Channels/src/System/Threading/Channels.

- BoundedChannel, RendezvousChannel, and UnboundedPriorityChannel are not copied.
- All public types are changed to internal.
- All .netcoreapp.cs files are not copied.
- `#if !NETCOREAPP` is added on top of all copied files.