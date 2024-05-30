# `IBannerMessageOwnerCapability`

An optional [test framework capability](itestframeworkcapability.md) that allows the test framework to provide the banner message to the platform. If the message is null or if the capability is not present, the platform will use its default banner message.

This capability implementation allows to abstract away the various conditions that the test framework may need to consider to decide whether or not the banner message should be displayed.

The platform exposes the [`IPlatformInformation` service](iplatforminformation.md) to provide some information about the platform that could be useful when building your custom banner message.
